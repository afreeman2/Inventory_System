using System.Windows;
using System.Windows.Input;
using DevExpress.Data.Filtering;
using DevExpress.Xpf.Grid;
using Inventory_System.converts;
using Inventory_System.Dashboard;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Furniture;
using Inventory_System.Premises;
using MahApps.Metro.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Inventory_System.support_Data;

public partial class FurnitureTypeWindow : MetroWindow
{
    #region تهيئة النافذة ونطاق البيانات
    private readonly FurnitureDataStore store;
    private int? editingId;
    private bool busy;
    private List<FurnitureCatalogRow> rows = [];

    private bool CanManage => PremisesSection.User?.forfut == true && App.location is not null;

    public FurnitureTypeWindow()
    {
        // تستخدم النافذة الفرع والموقع المسجلين للدخول وصلاحية إدارة الأثاث.
        var location = App.location ?? throw new InvalidOperationException("اختر موقع الدخول أولًا.");
        store = new FurnitureDataStore(OpenContext,
            new ServiceScope(location.Branch_ID, location.Location_ID, location.Location));
        InitializeComponent();
        AddButton.IsEnabled = CanManage;
        Closing += (_, e) => e.Cancel |= busy;
    }

    private static Cont_maintContext OpenContext()
    {
        DbContextHelper.InitOptions(App.ConnectionString);
        return new Cont_maintContext(DbContextHelper.Options ?? throw new InvalidOperationException("إعداد الاتصال غير جاهز."));
    }
    #endregion

    #region تحميل الجدول والبحث
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!CanManage) return;
        await RunAsync(ReloadAsync, "تعذر تحميل بيانات الأثاث", MessageBoxImage.Error);
        AddName.Focus();
    }

    private async Task ReloadAsync()
    {
        // نستعيد تحديد الفئة بمعرّفها عند إعادة تحميل القوائم بعد الحفظ أو الحذف.
        var selectedClass = AddClass.SelectedValue;
        var classes = await store.LoadClassesAsync();
        rows = await store.LoadCatalogAsync(true);
        EditClass.ItemsSource = AddClass.ItemsSource = classes;
        AddClass.SelectedValue = selectedClass;
        ApplyClassFilter();
    }

    private void View_SearchStringToFilterCriteria(object sender, SearchStringToFilterCriteriaEventArgs e) =>
        ArabicGridSearch.ApplyLookup(e, FunctionOperatorType.Contains,
            ("SearchClassName", true), ("Barcode", false));

    private void Class_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LookupGrid is not null) ApplyClassFilter();
    }

    private void ApplyClassFilter()
    {
        LookupGrid.ItemsSource = AddClass.SelectedValue is int id
            ? rows.Where(row => row.ClassId == id).ToList()
            : rows;
    }

    private void ClearClass_Click(object sender, RoutedEventArgs e)
    {
        if (!busy) AddClass.SelectedIndex = -1;
    }
    #endregion

    #region الإضافة وتعديل السجل داخل اللوحة
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanManage || EditFlyout.IsOpen) return;
        // خانة الاسم تستخدم للبحث أثناء الكتابة وللإضافة عند تنفيذ الحفظ.
        await SaveAsync(null, AddName, AddName.SearchText, AddBarcode, AddClass);
    }

    private void EditRow_DoubleClick(object sender, MouseButtonEventArgs e) => OpenSelectedEdit();

    private void OpenSelectedEdit()
    {
        if (busy || !CanManage || EditFlyout.IsOpen || LookupGrid.SelectedItem is not FurnitureCatalogRow row) return;
        editingId = row.TypeId;
        EditName.Text = row.Name;
        EditBarcode.Text = row.Barcode;
        EditClass.SelectedValue = row.ClassId;
        EditFlyout.IsOpen = true;
        EditName.Focus();
    }

    private async void SaveEdit_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanManage || editingId is not int id) return;
        await SaveAsync(id, EditName, EditName.Text, EditBarcode, EditClass);
    }

    private async Task SaveAsync(int? id, System.Windows.Controls.Control nameEditor, string? enteredName,
        System.Windows.Controls.TextBox barcodeEditor, System.Windows.Controls.ComboBox classEditor)
    {
        // التحقق عند الحفظ يحدد أول خانة تحتاج تصحيحًا قبل استدعاء مخزن البيانات.
        FieldFeedback.Clear(nameEditor);
        FieldFeedback.Clear(barcodeEditor);
        FieldFeedback.Clear(classEditor);
        var name = enteredName?.Trim() ?? "";
        if (!id.HasValue && classEditor.SelectedValue is not int)
        {
            FieldFeedback.Reject(classEditor, "اختر فئة الأثاث أولًا.");
            return;
        }
        if (name.Length is 0 or > 50)
        {
            FieldFeedback.Reject(nameEditor, "أدخل اسم نوع الأثاث بما لا يتجاوز 50 حرفًا.");
            return;
        }
        var barcode = barcodeEditor.Text.Trim();
        if (barcode.Length == 0)
        {
            FieldFeedback.Reject(barcodeEditor, "أدخل رقم الباركود.");
            return;
        }
        if (classEditor.SelectedValue is not int classId)
        {
            FieldFeedback.Reject(classEditor, "اختر فئة الأثاث.");
            return;
        }

        await RunAsync(async () =>
        {
            await store.SaveTypeAsync(id, name, barcode, classId);
            if (id.HasValue) EditFlyout.IsOpen = false;
            else ClearAddFields();
            await ReloadAsync();
            AddName.Focus();
        }, "تعذر حفظ البيان", MessageBoxImage.Error);
    }

    private void ClearAddFields()
    {
        AddName.SearchText = null;
        FieldFeedback.Clear(AddName);
        AddBarcode.Clear();
        FieldFeedback.Clear(AddBarcode);
        FieldFeedback.Clear(AddClass);
    }

    private void EditFlyout_Changed(object sender, RoutedEventArgs e)
    {
        // التخلص من المسودة عند الإلغاء أو الإغلاق يترك بيانات الجدول كما هي.
        if (EditFlyout.IsOpen) return;
        editingId = null;
        EditName.Clear();
        FieldFeedback.Clear(EditName);
        EditBarcode.Clear();
        EditClass.SelectedIndex = -1;
        FieldFeedback.Clear(EditBarcode);
        FieldFeedback.Clear(EditClass);
        AddName.Focus();
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        if (!busy) EditFlyout.IsOpen = false;
    }
    #endregion

    #region الحذف وتنفيذ العمليات واختصارات النافذة
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        // سياق زر الحذف يشير إلى صفه حتى لو كان هناك صف آخر محدد.
        if ((sender as FrameworkElement)?.DataContext is EditGridCellData cell &&
            cell.RowData.Row is FurnitureCatalogRow row)
            await DeleteAsync(row);
    }

    private async Task DeleteAsync(FurnitureCatalogRow row)
    {
        if (busy || !CanManage || EditFlyout.IsOpen) return;
        await RunAsync(async () =>
        {
            // السجل المرتبط يعرض سبب المنع مباشرة قبل سؤال التأكيد.
            var reason = await store.GetTypeDeleteBlockReasonAsync(row.TypeId);
            if (reason is not null)
            {
                UiMessages.Show(this, "تعذر حذف البيان", reason, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!CanManage) return;
            if (UiMessages.Show(this, "حذف نوع الأثاث", $"هل تريد حذف «{row.Name}»؟",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            await store.DeleteTypeAsync(row.TypeId);
            await ReloadAsync();
        }, "تعذر حذف البيان", MessageBoxImage.Warning);
    }

    private async Task RunAsync(Func<Task> operation, string errorTitle, MessageBoxImage icon)
    {
        // لا يبدأ أمر آخر أو تُغلق النافذة بينما يعمل الحفظ أو التحميل.
        busy = true;
        AddButton.IsEnabled = false;
        try { await operation(); }
        catch (Exception ex) { UiMessages.Show(this, errorTitle, ex.Message, MessageBoxButton.OK, icon); }
        finally { busy = false; AddButton.IsEnabled = CanManage; }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (!busy) Close();
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && EditFlyout.IsOpen)
        {
            e.Handled = true;
            CancelEdit_Click(sender, e);
        }
        else if (e.Key == Key.Escape && !AddClass.IsDropDownOpen && !AddName.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            Close_Click(sender, e);
        }
        else if (e.Key == Key.Enter && AddName.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            Add_Click(sender, e);
        }
        else if (e.Key == Key.Enter && EditName.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            SaveEdit_Click(sender, e);
        }
        else if (e.Key == Key.F2 && LookupGrid.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            OpenSelectedEdit();
        }
        else if (e.Key == Key.Delete && LookupGrid.IsKeyboardFocusWithin && LookupGrid.SelectedItem is FurnitureCatalogRow row)
        {
            e.Handled = true;
            await DeleteAsync(row);
        }
    }
    #endregion
}
