using DevExpress.Xpf.Grid;
using Inventory_System.converts;
using Inventory_System.Dashboard;
using Inventory_System.Models;
using MahApps.Metro.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Inventory_System.Premises;

public partial class PremisesWindow : MetroWindow
{
    #region تهيئة نافذة الحصر وحالة التحميل
    private readonly PremisesData data;
    private readonly PremisesKind kind;
    private readonly PremisesSection section;
    private readonly bool report;
    private readonly CancellationTokenSource lifetime = new();
    private T_Inventory_Location? editRecord;
    private bool busy;
    private bool replacingPickerItems;

    internal PremisesWindow(PremisesData data, PremisesKind kind, PremisesSection section, bool report)
    {
        this.data = data;
        this.kind = kind;
        this.section = section;
        this.report = report;
        InitializeComponent();

        bool locations = section.Register == PremisesRegister.Locations;
        Title = report ? "استعراض " + PremisesSection.KindName(kind) : section.Title;
        Heading.Text = Title;
        HeadingIcon.Kind = section.Icon;
        Subheading.Text = report ? "عرض سجلات المقرات" : PremisesSection.KindName(kind);
        AddPanel.Visibility = EditButton.Visibility = locations && !report ? Visibility.Visible : Visibility.Collapsed;
        LocationsGrid.Visibility = locations ? Visibility.Visible : Visibility.Collapsed;
        DetailsPanel.Visibility = DetailsGrid.Visibility = locations ? Visibility.Collapsed : Visibility.Visible;
        (DetailReference.Header, DetailDescription.Header) = section.Register switch
        {
            PremisesRegister.PhoneBills => ("رقم الفاتورة", "رقم الهاتف"),
            PremisesRegister.Power or PremisesRegister.Water => ("رقم الفاتورة", "رقم العداد"),
            PremisesRegister.Insurance => ("رمز نوع التأمين", "رمز الشركة"),
            PremisesRegister.Services => ("رقم العقد", "رمز الشركة"),
            PremisesRegister.Contracts => ("رقم السجل", "بيانات العقد"),
            PremisesRegister.PhoneNumbers => ("رقم الهاتف", "اسم المستخدم"),
            _ => ("الرقم", "البيان")
        };
        DetailValue.Visible = section.Register is not (PremisesRegister.Contracts or PremisesRegister.PhoneNumbers);
    }

    // صلاحية العرض تُفحص عند الفتح والتحميل، وصلاحية إدارة بيانات المقر تُفحص أيضًا عند الحفظ.
    private bool CanOpen => report ? PremisesSection.CanReport(kind) : section.CanOpen(kind);
    private bool CanEdit => !report && section.Register == PremisesRegister.Locations && section.CanOpen(kind);

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await ReloadAsync();
    private async void Reload_Click(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (busy || lifetime.IsCancellationRequested || !CanOpen) return;
        busy = true;
        AddPanel.IsEnabled = EditButton.IsEnabled = EditFields.IsEnabled = PremisePicker.IsEnabled = ReloadButton.IsEnabled = false;
        try
        {
            await operation(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!lifetime.IsCancellationRequested)
            {
                Status.Text = "تعذّر إكمال العملية. يمكنك التحديث وإعادة المحاولة.";
                UiMessages.Show(this, Title,
                    ex is InvalidOperationException && ex.Message.StartsWith("لم يُعرّف")
                        ? ex.Message : "تعذّر إكمال العملية في قاعدة بيانات الحصر.",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            busy = false;
            AddPanel.IsEnabled = EditButton.IsEnabled = EditFields.IsEnabled = PremisePicker.IsEnabled = ReloadButton.IsEnabled = true;
        }
    }

    private Task ReloadAsync() => RunAsync(LoadAsync);

    private async Task LoadAsync(CancellationToken token)
    {
        Status.Text = "جارٍ تحميل السجلات…";
        var selected = PremisePicker.SelectedItem as T_Inventory_Location;
        var locations = await data.LoadLocationsAsync(kind, token);
        token.ThrowIfCancellationRequested();
        if (section.Register == PremisesRegister.Locations)
        {
            LocationsGrid.ItemsSource = locations;
            Status.Text = locations.Count == 0 ? "لا توجد مقرات مسجّلة." : $"عدد المقرات: {locations.Count:N0}";
            return;
        }

        // يُحافظ التحديث على المقر المختار إذا بقي موجودًا في القائمة.
        replacingPickerItems = true;
        try
        {
            PremisePicker.ItemsSource = locations;
            PremisePicker.SelectedItem = locations.FirstOrDefault(p => p.In_Location_ID == selected?.In_Location_ID);
        }
        finally { replacingPickerItems = false; }
        await ShowDetailsAsync(token);
    }

    private async void PremisePicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!replacingPickerItems && IsLoaded)
            await RunAsync(ShowDetailsAsync);
    }

    private async Task ShowDetailsAsync(CancellationToken token)
    {
        DetailsGrid.ItemsSource = null;
        if (PremisePicker.SelectedItem is not T_Inventory_Location selected)
        {
            Status.Text = "اختر مقرًا لعرض سجلاته.";
            return;
        }
        Status.Text = "جارٍ تحميل السجلات…";
        var records = await data.LoadDetailsAsync(kind, section.Register, selected, token);
        token.ThrowIfCancellationRequested();
        DetailsGrid.ItemsSource = records;
        Status.Text = records.Count == 0 ? "لا توجد سجلات لهذا المقر." : $"عدد السجلات: {records.Count:N0}";
    }
    #endregion

    #region إضافة المقر وتعديل بياناته
    private async void Add_Click(object sender, RoutedEventArgs e) => await SaveAsync(false);
    private async void SaveEdit_Click(object sender, RoutedEventArgs e) => await SaveAsync(true);

    private async Task SaveAsync(bool editing)
    {
        if (!CanEdit || busy || (editing && (!EditFlyout.IsOpen || editRecord is null))) return;
        var nameField = editing ? EditName : NewName;
        var addressField = editing ? EditAddress : NewAddress;
        var original = editing ? editRecord : null;
        string name = nameField.Text.Trim();
        if (name.Length == 0)
        {
            FieldFeedback.Reject(nameField, "أدخل اسم المقر.");
            return;
        }

        bool duplicate = false;
        bool saved = false;
        await RunAsync(async token =>
        {
            duplicate = !await data.SaveLocationAsync(kind, original, name, addressField.Text.Trim(), token);
            token.ThrowIfCancellationRequested();
            if (duplicate) return;
            saved = true;
            if (editing) EditFlyout.IsOpen = false;
            else ClearAdd();
            await LoadAsync(token);
        });
        if (duplicate)
            FieldFeedback.Reject(nameField, "اسم المقر مسجّل مسبقًا ضمن هذا النوع.");
        else if (saved && !lifetime.IsCancellationRequested)
            UiMessages.Show(this, Title, editing ? "تم حفظ تعديل المقر." : "تمت إضافة المقر.",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ClearAdd_Click(object sender, RoutedEventArgs e)
    {
        ClearAdd();
        NewName.Focus();
    }

    private void ClearAdd()
    {
        NewName.Clear();
        NewAddress.Clear();
        FieldFeedback.Clear(NewName);
        FieldFeedback.Clear(NewAddress);
    }

    private void BeginEdit_Click(object sender, RoutedEventArgs e) => BeginEdit(LocationsGrid.SelectedItem as T_Inventory_Location);

    // يُفتح الصف الذي نُقر دون تشغيل التعديل عند النقر المزدوج على عنوان عمود أو صف التصفية.
    private void LocationsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LocationsGrid.View is TableView view && view.CalcHitInfo(e.OriginalSource as DependencyObject) is { InRow: true } hit)
            BeginEdit(LocationsGrid.GetRow(hit.RowHandle) as T_Inventory_Location);
    }

    private void BeginEdit(T_Inventory_Location? record)
    {
        if (!CanEdit || busy || record is null) return;
        editRecord = record;
        EditName.Text = record.In_Location;
        EditAddress.Text = record.Address;
        FieldFeedback.Clear(EditName);
        FieldFeedback.Clear(EditAddress);
        EditFlyout.IsOpen = true;
        Dispatcher.InvokeAsync(() => EditName.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        if (!busy) EditFlyout.IsOpen = false;
    }

    private void EditFlyout_Changed(object sender, RoutedEventArgs e)
    {
        if (EditFlyout is null || EditName is null || EditAddress is null || EditFlyout.IsOpen) return;
        editRecord = null;
        EditName.Clear();
        EditAddress.Clear();
        FieldFeedback.Clear(EditName);
        FieldFeedback.Clear(EditAddress);
    }
    #endregion

    #region اختصارات الخانات وإغلاق النافذة
    private void MoveToAddress_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        (EditFlyout.IsOpen ? EditAddress : NewAddress).Focus();
        e.Handled = true;
    }

    private async void SaveOnEnter_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await SaveAsync(EditFlyout.IsOpen);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || PremisePicker.IsDropDownOpen) return;
        e.Handled = true;
        if (busy) return;
        if (EditFlyout.IsOpen) EditFlyout.IsOpen = false;
        else Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        // تلغى الاستعلامات عند الإغلاق، وتُرفض أي نتائج تعود بعده.
        lifetime.Cancel();
        lifetime.Dispose();
        base.OnClosed(e);
    }
    #endregion
}
