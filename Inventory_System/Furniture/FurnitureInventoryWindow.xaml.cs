using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DevExpress.Data.Filtering;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using Inventory_System.converts;
using Inventory_System.Dashboard;
using Inventory_System.dbContext;
using Inventory_System.DatabContext;
using Inventory_System.Models;
using Inventory_System.Premises;
using Inventory_System.support_Data;
using MahApps.Metro.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Inventory_System.Furniture;

public partial class FurnitureInventoryWindow : MetroWindow
{
    #region حالة النافذة والنطاق ومسودة الحصر
    private readonly Func<Cont_maintContext> contextFactory;
    private readonly FurnitureDataStore store;
    private readonly List<FurnitureLocationChoice> locations = [];
    private readonly List<T_Manage> managements = [];
    private readonly List<FurnitureTypeChoice> types = [];
    private readonly ObservableCollection<FurnitureDetailDraft> details = [];
    private long? editingId;
    private int sectionLoadVersion;
    // يمنع طلب بحث أقدم من استبدال نتائج المقر الذي اختاره المستخدم لاحقًا.
    private int recordsLoadVersion;
    private bool loadingChoices;
    private bool busy;
    private bool closed;

    private bool CanManage => !closed && PremisesSection.User?.forfut == true && App.location is not null;

    public FurnitureInventoryWindow() : this(OpenContext) { }

    internal FurnitureInventoryWindow(Func<Cont_maintContext> createContext)
    {
        contextFactory = createContext;
        var location = App.location ?? throw new InvalidOperationException("اختر موقع الدخول أولًا.");
        store = new FurnitureDataStore(contextFactory, new ServiceScope(location.Branch_ID, location.Location_ID, location.Location));
        InitializeComponent();
        DetailsGrid.ItemsSource = details;
        AddTypeButton.IsEnabled = CanManage;
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Closed += (_, _) => { closed = true; ++sectionLoadVersion; };
    }

    private static Cont_maintContext OpenContext()
    {
        DbContextHelper.InitOptions(App.ConnectionString);
        return new Cont_maintContext(DbContextHelper.Options ?? throw new InvalidOperationException("إعداد الاتصال غير جاهز."));
    }
    #endregion

    #region تحميل القوائم وسجلات الحصر وتصفية التاريخ
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!CanManage)
        {
            UiMessages.Show(this, "صلاحية الأثاث", "لا تملك صلاحية فتح حصر الأثاث.", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
            return;
        }
        await ReloadChoicesAsync();
        RecordsGrid.ItemsSource = Array.Empty<FurnitureRecordRow>();
        StatusText.Text = "اختر المقر لعرض سجلات الحصر وبنودها.";
    }

    private async Task ReloadChoicesAsync()
    {
        loadingChoices = true;
        try
        {
            int? selectedBuildingId = FilterBuilding?.SelectedValue is int selectedId ? selectedId : null;
            locations.Clear(); locations.AddRange(await store.LoadLocationsAsync());
            managements.Clear(); managements.AddRange(await store.LoadManagementsAsync());
            types.Clear(); types.AddRange(await store.LoadTypesAsync());
            FilterBuilding!.ItemsSource = EditorBuilding.ItemsSource = locations;
            if (selectedBuildingId is int existingId && locations.Any(x => x.Id == existingId))
                FilterBuilding.SelectedValue = existingId;
            ManagementChoice.ItemsSource = managements;
            TypeChoice.ItemsSource = types;
            SectionChoice.ItemsSource = Array.Empty<T_Section>();
            AddTypeButton.IsEnabled = CanManage;
        }
        catch (Exception ex)
        {
            if (!closed) UiMessages.Show(this, "تعذر تحميل خيارات الأثاث", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { loadingChoices = false; }
    }

    private async Task ReloadRecordsAsync()
    {
        if (!CanManage) return;
        int requestVersion = ++recordsLoadVersion;
        if (FilterBuilding.SelectedValue is not int buildingId)
        {
            RecordsGrid.ItemsSource = Array.Empty<FurnitureRecordRow>();
            RecordsGrid.SelectedItem = null;
            StatusText.Text = "اختر المقر لعرض سجلات الحصر وبنودها.";
            return;
        }
        RecordsGrid.ItemsSource = Array.Empty<FurnitureRecordRow>();
        RecordsGrid.SelectedItem = null;
        if (FromDate.SelectedDate is DateTime start && ToDate.SelectedDate is DateTime end && start.Date > end.Date)
        {
            FieldFeedback.Reject(FromDate, "تاريخ بداية البحث يجب أن يسبق تاريخ النهاية.");
            StatusText.Text = "تحقق من ترتيب تاريخ بداية البحث ونهايته.";
            return;
        }
        StatusText.Text = "جارٍ تحميل سجلات المقر المختار...";
        try
        {
            DateOnly? from = FromDate.SelectedDate is DateTime fromDate ? DateOnly.FromDateTime(fromDate) : null;
            DateOnly? to = ToDate.SelectedDate is DateTime toDate ? DateOnly.FromDateTime(toDate) : null;
            var records = await store.SearchAsync(buildingId, from, to);
            if (closed || requestVersion != recordsLoadVersion || FilterBuilding.SelectedValue is not int currentBuilding || currentBuilding != buildingId) return;
            RecordsGrid.ItemsSource = records;
            // تُفتح تفاصيل السجلات المحملة ليشاهد المستخدم أصنافها مجمعة من أول عرض.
            await Dispatcher.InvokeAsync(() =>
            {
                for (int index = 0; index < records.Count; index++)
                {
                    if (records[index].Details.Count == 0) continue;
                    int rowHandle = RecordsGrid.GetRowHandleByListIndex(index);
                    if (rowHandle >= 0) RecordsGrid.ExpandMasterRow(rowHandle);
                }
            }, DispatcherPriority.Loaded);
            if (closed || requestVersion != recordsLoadVersion) return;
            StatusText.Text = records.Count == 0
                ? "لا توجد سجلات حصر لهذا المقر ضمن التواريخ المحددة."
                : $"{records.Count:N0} سجل حصر • انقر مرتين على الرأس لفتحه وتعديله";
        }
        catch (Exception ex)
        {
            if (!closed && requestVersion == recordsLoadVersion) UiMessages.Show(this, "تعذر تحميل سجلات الحصر", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        if (FilterBuilding.SelectedValue is not int) FieldFeedback.Reject(FilterBuilding, "اختر المقر أولًا لعرض سجلات الحصر.");
        await ReloadRecordsAsync();
    }
    private async void FilterBuilding_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loadingChoices || FilterBuilding.SelectedValue is not int) { if (!loadingChoices) await ReloadRecordsAsync(); return; }
        FieldFeedback.Clear(FilterBuilding);
        await ReloadRecordsAsync();
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ReloadChoicesAsync();
        await ReloadRecordsAsync();
    }

    private async void Management_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (loadingChoices || SectionChoice is null) return;
        SectionChoice.ItemsSource = Array.Empty<T_Section>();
        if (ManagementChoice.SelectedValue is not short managerId) return;
        var version = ++sectionLoadVersion;
        try
        {
            var available = await store.LoadSectionsAsync(managerId);
            if (closed || version != sectionLoadVersion || ManagementChoice.SelectedValue is not short current || current != managerId) return;
            SectionChoice.ItemsSource = available;
        }
        catch (Exception ex) { if (!closed) UiMessages.Show(this, "تعذر تحميل الأقسام", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // يوجّه البحث الموحّد إلى بيانات الرأس وبنود الأثاث بعد تطبيع اختلافات الحروف العربية.
    private void Records_SearchStringToFilterCriteria(object sender, SearchStringToFilterCriteriaEventArgs e) =>
        ArabicGridSearch.ApplyLookup(e, FunctionOperatorType.Contains, ("SearchContent", true));
    #endregion

    #region إنشاء مسودة الحصر وفتح سجل للتعديل
    private void NewRecord_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanManage) return;
        editingId = null;
        ClearEditor();
        EditorHeading.Text = "حصر أثاث جديد";
        if (FilterBuilding.SelectedValue is int buildingId) EditorBuilding.SelectedValue = buildingId;
        EditorDate.SelectedDate = DateTime.Today;
        EditorFlyout.IsOpen = true;
        EditorBuilding.Focus();
    }

    private async void Record_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (busy || !CanManage || RecordsGrid.SelectedItem is not FurnitureRecordRow selected) return;
        busy = true;
        try
        {
            var record = await store.LoadRecordAsync(selected.Id);
            if (record is null) { UiMessages.Show(this, "سجل الحصر", "لم يعد السجل موجودًا؛ حدّث القائمة.", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            editingId = record.Value.Header.Id;
            ClearEditor();
            EditorHeading.Text = "تعديل حصر الأثاث";
            EditorBuilding.SelectedValue = record.Value.Header.BuildingId;
            EditorDate.SelectedDate = record.Value.Header.Date?.ToDateTime(TimeOnly.MinValue);
            DescriptionInput.Text = record.Value.Header.Description;
            foreach (var row in record.Value.Details)
                details.Add(new FurnitureDetailDraft { Id = row.Id, TypeId = row.TypeId, ManagementId = row.ManagementId,
                    SectionId = row.SectionId, Type = row.Type, Management = row.Management, Section = row.Section,
                    Count = row.Count, Notes = row.Notes });
            EditorFlyout.IsOpen = true;
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر فتح سجل الحصر", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; }
    }
    #endregion

    #region إضافة الأصناف إلى المسودة والبحث بالباركود
    private void AddDetail_Click(object sender, RoutedEventArgs e) => AddSelectedType(QuantityInput.EditValue);

    private void AddSelectedType(object? quantityValue)
    {
        FieldFeedback.Clear(EditorBuilding);
        if (EditorBuilding.SelectedValue is not int)
        { FieldFeedback.Reject(EditorBuilding, "اختر المقر قبل إضافة أصناف الحصر."); return; }
        FieldFeedback.Clear(ManagementChoice); FieldFeedback.Clear(SectionChoice);
        FieldFeedback.Clear(TypeChoice); FieldFeedback.Clear(QuantityInput);
        if (ManagementChoice.SelectedItem is not T_Manage manager)
        { FieldFeedback.Reject(ManagementChoice, "اختر الإدارة أولًا."); return; }
        if (SectionChoice.SelectedItem is not T_Section section)
        { FieldFeedback.Reject(SectionChoice, "اختر القسم أو الوحدة."); return; }
        if (TypeChoice.SelectedItem is not FurnitureTypeChoice type)
        { FieldFeedback.Reject(TypeChoice, "اختر نوع الأثاث."); return; }
        if (!long.TryParse(Convert.ToString(quantityValue), out long quantity) || quantity <= 0)
        { FieldFeedback.Reject(QuantityInput, "أدخل كمية صحيحة أكبر من صفر."); return; }

        var existing = details.FirstOrDefault(x => x.TypeId == type.Id && x.ManagementId == manager.Manag_ID && x.SectionId == section.Secti_ID);
        if (existing is null)
        {
            details.Add(new FurnitureDetailDraft { TypeId = type.Id, ManagementId = manager.Manag_ID,
                SectionId = section.Secti_ID, Type = type.Name, Management = manager.Manag,
                Section = section.Secti, Count = quantity, Notes = NotesInput.Text?.Trim() ?? "" });
        }
        else
        {
            existing.Count += quantity;
            if (!string.IsNullOrWhiteSpace(NotesInput.Text)) existing.Notes = NotesInput.Text.Trim();
        }
        QuantityInput.EditValue = 1;
        NotesInput.Clear();
        BarcodeInput.Clear();
        TypeChoice.Focus();
    }

    private void Barcode_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var barcode = BarcodeInput.Text?.Trim();
        var type = types.FirstOrDefault(x => !string.IsNullOrWhiteSpace(barcode)
            && string.Equals(x.Barcode?.Trim(), barcode, StringComparison.OrdinalIgnoreCase));
        if (type is null) { FieldFeedback.Reject(BarcodeInput, "لم يُعثر على نوع أثاث بهذا الباركود."); return; }
        TypeChoice.SelectedValue = type.Id;
        QuantityInput.EditValue = 1;
        AddSelectedType(1L);
    }

    private void RemoveDetail_Click(object sender, RoutedEventArgs e)
    {
        if (GetCellRow<FurnitureDetailDraft>(sender) is FurnitureDetailDraft row) details.Remove(row);
    }

    private async void AddType_Click(object sender, RoutedEventArgs e)
    {
        if (!CanManage || busy) return;
        // زر الإضافة من الحصر يفتح نفس نافذة أنواع الأثاث الموجودة في البيانات المساعدة.
        new FurnitureTypeWindow { Owner = this }.ShowDialog();
        types.Clear(); types.AddRange(await store.LoadTypesAsync());
        TypeChoice.ItemsSource = types;
    }
    #endregion

    #region حفظ سجل الحصر والتحقق من مدخلاته
    private async void SaveEditor_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanManage) return;
        FieldFeedback.Clear(EditorBuilding); FieldFeedback.Clear(EditorDate); FieldFeedback.Clear(DescriptionInput);
        if (EditorBuilding.SelectedValue is not int buildingId) { FieldFeedback.Reject(EditorBuilding, "اختر المقر."); return; }
        if (EditorDate.SelectedDate is not DateTime date) { FieldFeedback.Reject(EditorDate, "اختر تاريخ الحصر."); return; }
        if (string.IsNullOrWhiteSpace(DescriptionInput.Text)) { FieldFeedback.Reject(DescriptionInput, "أدخل بيان الحصر."); return; }
        if (details.Count == 0) { UiMessages.Show(this, "بيان الحصر", "أضف صنفًا واحدًا على الأقل قبل الحفظ.", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        busy = true; SaveButton.IsEnabled = false;
        try
        {
            var draft = new FurnitureRecordDraft(editingId, buildingId, DateOnly.FromDateTime(date), DescriptionInput.Text,
                details.ToArray());
            await store.SaveRecordAsync(draft);
            EditorFlyout.IsOpen = false;
            await ReloadRecordsAsync();
            StatusText.Text = "تم حفظ سجل الحصر وبنوده.";
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر حفظ الحصر", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; SaveButton.IsEnabled = true; }
    }

    private async void DeleteRecord_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanManage || GetCellRow<FurnitureRecordRow>(sender) is not FurnitureRecordRow row) return;
        if (UiMessages.Show(this, "حذف حصر الأثاث", $"هل تريد حذف الحصر رقم {row.Id} وبنوده؟",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        busy = true;
        try { await store.DeleteRecordAsync(row.Id); await ReloadRecordsAsync(); }
        catch (Exception ex) { UiMessages.Show(this, "تعذر حذف الحصر", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; }
    }
    #endregion

    #region قراءة سجل الصف من سياق خلية DevExpress
    // سياق قالب الخلية هو بيانات الخلية؛ نقرأ الصف الفعلي مع دعم الاستدعاء المباشر أيضًا.
    private static T? GetCellRow<T>(object? sender) where T : class
    {
        var dataContext = (sender as FrameworkElement)?.DataContext;
        return dataContext is EditGridCellData cell ? cell.RowData.Row as T : dataContext as T;
    }
    #endregion

    #region تصفية أعمدة تفاصيل الحصر من شريط رؤوس القروب
    // لأن العناوين أصبحت ضمن صف المجموعة، نصل إلى الجدول المالك لفتح محرر التصفية المعتاد للعمود.
    private void GroupColumnFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not GridColumn column) return;

        DependencyObject? parent = element;
        while (parent is not null && parent is not GridControl)
            parent = VisualTreeHelper.GetParent(parent);

        if (parent is GridControl grid && grid.View is TableView view)
            view.ShowFilterEditor(column);
    }
    #endregion

    #region إغلاق اللوحة ومسح المسودة والنافذة
    private void EditorFlyout_Changed(object sender, RoutedEventArgs e)
    {
        if (EditorFlyout.IsOpen) return;
        editingId = null;
        ClearEditor();
    }

    private void ClearEditor()
    {
        details.Clear();
        EditorBuilding.SelectedItem = null; EditorDate.SelectedDate = null; DescriptionInput.Clear();
        ManagementChoice.SelectedItem = null; SectionChoice.ItemsSource = Array.Empty<T_Section>();
        TypeChoice.SelectedItem = null; QuantityInput.EditValue = 1; NotesInput.Clear(); BarcodeInput.Clear();
        ClearEditorErrors();
    }

    private void ClearEditorErrors()
    {
        FieldFeedback.Clear(EditorBuilding); FieldFeedback.Clear(EditorDate); FieldFeedback.Clear(DescriptionInput);
        FieldFeedback.Clear(ManagementChoice); FieldFeedback.Clear(SectionChoice); FieldFeedback.Clear(TypeChoice);
        FieldFeedback.Clear(QuantityInput); FieldFeedback.Clear(NotesInput); FieldFeedback.Clear(BarcodeInput);
    }

    private void CancelEditor_Click(object sender, RoutedEventArgs e) => EditorFlyout.IsOpen = false;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && EditorFlyout.IsOpen) { EditorFlyout.IsOpen = false; e.Handled = true; }
    }
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (busy) e.Cancel = true;
        else { closed = true; ++sectionLoadVersion; EditorFlyout.IsOpen = false; }
    }
    #endregion
}
