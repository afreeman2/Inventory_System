using DevExpress.Xpf.Grid;
using DevExpress.XtraReports.UI;
using Inventory_System.converts;
using Inventory_System.Dashboard;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using Inventory_System.Premises;
using Inventory_System.reports;
using Inventory_System.support_Data;
using Inventory_System.viewo_Model;
using MahApps.Metro.Controls;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ComboBox = System.Windows.Controls.ComboBox;
using Control = System.Windows.Controls.Control;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace Inventory_System.Inventory_Windows.owned_Windows;

public partial class owned_Inventory_Phones_Lines_Window : MetroWindow
{
    #region نطاق النافذة والتهيئة وحماية العمليات المتزامنة
    private readonly ServiceScope? scope;
    private List<PhoneLineRow> rows = [];
    private List<T_Section> sections = [];
    private PhoneLineKey? editingKey;
    // هوية المبنى تأتي من الواجهة الأصلية وتثبت للمسودة حتى إغلاقها.
    private int? draftBuildingId;
    private bool updating, busy, closed;
    private int loadVersion;
    // نحتفظ بالتقرير ما دامت المعاينة مفتوحة ونحرر مستنده عند إغلاقها.
    private XtraReport? printReport;
    private static bool CanUse => PremisesSection.User?.forowphon == true;
    private static bool CanManage => PremisesSection.User?.mang_check == true;

    public owned_Inventory_Phones_Lines_Window()
    {
        InitializeComponent();
        if (App.location is { } location) scope = new(location.Branch_ID, location.Location_ID, location.Location);
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Closed += (_, _) => { closed = true; ++loadVersion; ReleasePrint(); };
        ManageOrganizations.IsEnabled = ManageSections.IsEnabled = CanManage;
    }
    private static Cont_maintContext OpenContext()
    {
        DbContextHelper.InitOptions(App.ConnectionString); return new Cont_maintContext(DbContextHelper.Options!);
    }
    private async void Window_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        if (scope is null || !CanUse) { ResultStatus.Text = "يتطلب العرض موقع دخول وصلاحية أرقام الهواتف المملوكة."; return; }
        try { await using var db = OpenContext(); await ReloadAsync(db); }
        catch (Exception ex) { if (!closed) UiMessages.Show(this, "تعذر تحميل خطوط الهاتف", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private async Task ReloadAsync(Cont_maintContext db)
    {
        if (scope is null) return;
        var version = ++loadVersion;
        // ننشر النتائج معًا بعد اكتمال القراءة؛ لا تعود نتائج طلب قديم بعد الإغلاق أو تحديث أحدث.
        var buildings = await PhoneLineStore.Buildings(db, scope).AsNoTracking().OrderBy(x => x.In_Location).ToListAsync();
        var managers = await OrganizationLookupStore.Managers(db, scope).AsNoTracking().OrderBy(x => x.Manag).ToListAsync();
        var freshSections = await OrganizationLookupStore.Sections(db, scope).AsNoTracking().OrderBy(x => x.Secti).ToListAsync();
        var freshRows = await PhoneLineStore.LoadAsync(db, scope);
        if (closed || version != loadVersion) return;
        var building = FilterBuilding.SelectedValue; var manager = FilterManagement.SelectedValue; var section = FilterSection.SelectedValue;
        var reportBuilding = ReportBuilding.SelectedValue; var reportManagement = ReportManagement.SelectedValue;
        var draftManager = DraftManagement.SelectedValue; var draftSection = DraftSection.SelectedValue;
        updating = true;
        try
        {
            rows = freshRows; sections = freshSections;
            FilterBuilding.ItemsSource = buildings;
            ReportBuilding.ItemsSource = buildings;
            FilterManagement.ItemsSource = DraftManagement.ItemsSource = managers;
            ReportManagement.ItemsSource = managers;
            FilterBuilding.SelectedValue = building; FilterManagement.SelectedValue = manager;
            ReportBuilding.SelectedValue = reportBuilding; ReportManagement.SelectedValue = reportManagement;
            DraftManagement.SelectedValue = draftManager;
            PopulateSections(FilterManagement, FilterSection, true); FilterSection.SelectedValue = section;
            PopulateSections(DraftManagement, DraftSection, false); DraftSection.SelectedValue = draftSection;
        }
        finally { updating = false; }
        ApplyFilters();
    }
    #endregion

    #region البحث المباشر بالرقم والإدارة والقسم أو الوحدة
    private void PopulateSections(ComboBox management, ComboBox section, bool allowAll)
    {
        // عند البحث دون إدارة تظهر كل الوحدات؛ في المسودة يلزم اختيار الإدارة أولًا.
        section.ItemsSource = management.SelectedValue is short id ? sections.Where(x => x.Manag_ID == id).ToList()
            : allowAll ? sections : new List<T_Section>();
        section.SelectedIndex = -1;
    }
    private void FilterManagement_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (updating || FilterSection is null) return;
        updating = true; PopulateSections(FilterManagement, FilterSection, true); updating = false; ApplyFilters();
    }
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (!updating) ApplyFilters(); }
    private void PhoneSearch_Changed(object sender, TextChangedEventArgs e) { if (!updating) ApplyFilters(); }
    private void OptionalFilter_Click(object sender, RoutedEventArgs e)
    {
        if (updating) return;
        // إلغاء فلتر الإدارة يلغي اختياره وتبعية قائمة الوحدات له؛ كل فلتر يعمل بمفتاحه المستقل.
        updating = true;
        if (ByManagement.IsChecked != true)
        {
            FilterManagement.SelectedIndex = -1;
            PopulateSections(FilterManagement, FilterSection, true);
        }
        if (BySection.IsChecked != true) FilterSection.SelectedIndex = -1;
        updating = false; ApplyFilters();
    }
    private void ApplyFilters()
    {
        if (LinesGrid is null || ByManagement is null || BySection is null) return;
        // اختيار المقر شرط العرض؛ الرقم لا يبحث في مخزون بقية المقرات ولا يعرضه عند التفريغ.
        var buildingRows = FilterBuilding.SelectedValue is int building
            ? rows.Where(x => x.Key.BuildingId == building).ToList() : new List<PhoneLineRow>();
        PhoneSearch.IsEnabled = buildingRows.Count > 0;
        FilterManagement.IsEnabled = buildingRows.Count > 0 && ByManagement.IsChecked == true;
        FilterSection.IsEnabled = buildingRows.Count > 0 && BySection.IsChecked == true;
        IEnumerable<PhoneLineRow> filtered = buildingRows;
        if (ByManagement.IsChecked == true && FilterManagement.SelectedValue is short management) filtered = filtered.Where(x => x.ManagementId == management);
        if (BySection.IsChecked == true && FilterSection.SelectedValue is short section) filtered = filtered.Where(x => x.SectionId == section);
        var number = PhoneSearch.Text.Trim();
        if (number.Length > 0) filtered = filtered.Where(x => x.Phone.Contains(number, StringComparison.OrdinalIgnoreCase));
        // البحث في رقم الهاتف وحده؛ لا يطابق اسم المستخدم أو اسم الإدارة بالخطأ.
        var result = filtered.ToList(); LinesGrid.ItemsSource = result;
        ResultStatus.Text = FilterBuilding.SelectedValue is not int ? "اختر المقر لعرض أرقام الهواتف."
            : result.Count == 0 ? "لا توجد أرقام مطابقة للبحث في المقر المحدد."
            : $"عدد أرقام الهواتف: {result.Count:N0} من {buildingRows.Count:N0} — انقر مرتين للتعديل.";
    }
    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        updating = true;
        ByManagement.IsChecked = BySection.IsChecked = false;
        FilterBuilding.SelectedIndex = FilterManagement.SelectedIndex = -1; PhoneSearch.Clear();
        PopulateSections(FilterManagement, FilterSection, true); LinesGrid.SelectedItem = null;
        foreach (var field in new Control[] { FilterBuilding, FilterManagement, FilterSection, PhoneSearch }) FieldFeedback.Clear(field);
        updating = false; ApplyFilters(); FilterBuilding.Focus();
    }
    private void CancelCommand_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        // الإلغاء يعيد الشاشة لحالة البداية ويمنع نشر قراءة قديمة فوق الحالة الملغاة.
        ++loadVersion; LineFlyout.IsOpen = PrintFlyout.IsOpen = ReportFlyout.IsOpen = false; ClearDraft(); ClearFilters_Click(sender, e);
    }
    #endregion

    #region إضافة وتعديل المسودة داخل لوحة سفلية وإلغاء تغييراتها
    private Control[] Fields => [DraftPhone, DraftManagement, DraftSection, DraftUser];
    private void NewLine_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanUse || scope is null) return;
        if (FilterBuilding.SelectedValue is not int building)
        { FieldFeedback.Reject(FilterBuilding, "اختر المبنى المستهدف قبل إضافة خط الهاتف."); return; }
        ClearDraft(); draftBuildingId = building;
        DraftTitle.Text = "إضافة خط هاتف"; LineFlyout.IsOpen = true; DraftPhone.Focus();
    }
    private void LineRow_DoubleClick(object sender, RowDoubleClickEventArgs e)
    {
        // نأخذ الصف المنقور نفسه؛ النقر على مجموعة أو رأس عمود لا يفتح سجل التحديد السابق.
        var handle = e.HitInfo.RowHandle;
        if (handle >= 0 && LinesGrid.GetRow(handle) is PhoneLineRow row) OpenDraft(row);
    }
    private void OpenDraft(PhoneLineRow row)
    {
        if (busy || !CanUse || FilterBuilding.SelectedValue is not int building || row.Key.BuildingId != building) return;
        ClearDraft(); editingKey = row.Key;
        draftBuildingId = building;
        DraftPhone.Text = row.Phone; DraftUser.Text = row.UserName ?? string.Empty;
        DraftManagement.SelectedValue = row.ManagementId is >= short.MinValue and <= short.MaxValue ? (short?)row.ManagementId : null;
        DraftSection.SelectedValue = row.SectionId;
        foreach (var field in Fields) FieldFeedback.Clear(field);
        DraftTitle.Text = "تعديل خط الهاتف"; LineFlyout.IsOpen = true; DraftPhone.Focus();
    }
    private void DraftManagement_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!updating && DraftSection != null) PopulateSections(DraftManagement, DraftSection, false);
    }
    private void ClearDraft()
    {
        editingKey = null; draftBuildingId = null; DraftPhone.Clear(); DraftUser.Clear();
        DraftManagement.SelectedIndex = DraftSection.SelectedIndex = -1;
        foreach (var field in Fields) FieldFeedback.Clear(field);
    }
    private void CancelDraft_Click(object sender, RoutedEventArgs e) { if (!busy) LineFlyout.IsOpen = false; }
    private void LineFlyout_Changed(object sender, RoutedEventArgs e) { if (!LineFlyout.IsOpen) ClearDraft(); }
    private async Task<bool> SaveDraftAsync(Cont_maintContext db)
    {
        if (scope is null || !CanUse) return false;
        foreach (var field in Fields) FieldFeedback.Clear(field);
        if (draftBuildingId is not int building || FilterBuilding.SelectedValue is not int selected || selected != building)
            return RejectDraft(FilterBuilding, "اختر المبنى المستهدف من الواجهة الأصلية ثم افتح المسودة مجددًا.");
        if (string.IsNullOrWhiteSpace(DraftPhone.Text) || DraftPhone.Text.Trim().Length > 50) return RejectDraft(DraftPhone, "أدخل رقم الهاتف بما لا يتجاوز 50 حرفًا.");
        if (DraftManagement.SelectedValue is not short management) return RejectDraft(DraftManagement, "اختر الإدارة أو القسم.");
        if (DraftSection.SelectedValue is not short section) return RejectDraft(DraftSection, "اختر القسم أو الوحدة.");
        // ننسخ المسودة قبل أول انتظار؛ يبقى مفتاح التعديل ثابتًا حتى لو تغير تحديد الجدول.
        var draft = new PhoneLineDraft(building, DraftPhone.Text, management, section, DraftUser.Text);
        var result = await PhoneLineStore.SaveAsync(db, scope, editingKey, draft);
        if (result.Error != null) return RejectDraft(result.Field switch
            { "Building" => FilterBuilding, "Management" => DraftManagement, "Section" => DraftSection, "User" => DraftUser, _ => DraftPhone }, result.Error);
        editingKey = result.Key; DraftTitle.Text = "تعديل خط الهاتف"; return true;
    }
    private bool RejectDraft(Control field, string message)
    {
        // نعيد تفعيل المسودة قبل توجيه التركيز إلى أول خطأ، حتى عند رفض الحفظ بعد انتظار قاعدة البيانات.
        DraftFields.IsEnabled = true; return FieldFeedback.Reject(field, message);
    }
    private async void SaveLine_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !LineFlyout.IsOpen || !CanUse) return;
        busy = true; DraftFields.IsEnabled = false;
        try
        {
            await using var db = OpenContext(); if (!await SaveDraftAsync(db)) return;
            await ReloadAsync(db); LineFlyout.IsOpen = false;
            UiMessages.Show(this, "خطوط الهاتف", "تم حفظ خط الهاتف بنجاح.", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر حفظ الخط", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; DraftFields.IsEnabled = true; }
    }
    #endregion

    #region حذف السجل المحدد وتحديث البيانات المساعدة
    private async void DeleteLine_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is EditGridCellData { RowData.Row: PhoneLineRow row }) await DeleteAsync(row);
    }
    private async Task DeleteAsync(PhoneLineRow row)
    {
        if (busy || !CanUse || LineFlyout.IsOpen || PrintFlyout.IsOpen || ReportFlyout.IsOpen) return;
        if (UiMessages.Show(this, "حذف خط الهاتف", $"هل تريد حذف رقم الهاتف {row.Phone}؟", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        busy = true;
        try { await using var db = OpenContext(); await PhoneLineStore.DeleteAsync(db, row.Key); await ReloadAsync(db); }
        catch (Exception ex) { UiMessages.Show(this, "تعذر حذف الخط", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; }
    }
    private void ManageOrganizations_Click(object sender, RoutedEventArgs e) => ManageLookup(false);
    private void ManageSections_Click(object sender, RoutedEventArgs e) => ManageLookup(true);
    private async void ManageLookup(bool isSection)
    {
        if (busy || scope is null || !CanManage) return;
        new OrganizationLookupWindow(scope, isSection, DraftManagement.SelectedValue as short?) { Owner = this }.ShowDialog();
        // إعادة القراءة تحافظ على نص الرقم والمستخدم والاختيارات التي لا تزال موجودة.
        await RefreshAsync();
    }
    #endregion

    #region معاينة الطباعة والاختصارات والتنقل
    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanUse) return;
        if (PrintMode.SelectedItem?.ToString() == "طباعة تقرير")
        {
            // يملأ التقرير بالمقر المختار حاليًا لتسهيل البدء، مع بقاء إعداداته مستقلة عن فلاتر الكشف.
            if (ReportBuilding.SelectedIndex < 0 && FilterBuilding.SelectedValue is int building)
                ReportBuilding.SelectedValue = building;
            ReportByManagement.IsChecked = ByManagement.IsChecked == true;
            ReportManagement.IsEnabled = ReportByManagement.IsChecked == true;
            if (ReportByManagement.IsChecked == true && ReportManagement.SelectedIndex < 0)
                ReportManagement.SelectedValue = FilterManagement.SelectedValue;
            ReportFlyout.IsOpen = true;
            ReportBuilding.Focus();
            return;
        }
        // تشمل هذه القائمة تصفية أعمدة الجدول وترتيبها أيضًا، ولا تتأثر بطي مجموعات الإدارات.
        var currentRows = LinesGrid.VisibleItems.OfType<PhoneLineRow>().ToList();
        if (currentRows.Count == 0) return;
        OpenPrintPreview(currentRows, "خطوط الهاتف للمقرات المملوكة", false);
    }

    private void ReportManagementFilter_Click(object sender, RoutedEventArgs e)
    {
        // تظل الإدارة اختيارية؛ إلغاء الشرط يمسح الإدارة السابقة حتى لا تؤثر خفيةً على التقرير.
        ReportManagement.IsEnabled = ReportByManagement.IsChecked == true;
        if (!ReportManagement.IsEnabled) ReportManagement.SelectedIndex = -1;
    }

    private void CancelReport_Click(object sender, RoutedEventArgs e) => ReportFlyout.IsOpen = false;

    private void PrintReport_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanUse || !ReportFlyout.IsOpen) return;
        FieldFeedback.Clear(ReportTitle);
        FieldFeedback.Clear(ReportBuilding);
        FieldFeedback.Clear(ReportManagement);
        if (string.IsNullOrWhiteSpace(ReportTitle.Text))
        { FieldFeedback.Reject(ReportTitle, "أدخل عنوان التقرير."); return; }
        if (ReportBuilding.SelectedValue is not int building)
        { FieldFeedback.Reject(ReportBuilding, "اختر المقر الذي تريد إعداد تقريره."); return; }
        if (ReportByManagement.IsChecked == true && ReportManagement.SelectedValue is not short)
        { FieldFeedback.Reject(ReportManagement, "اختر الإدارة أو ألغِ خيار التقرير حسب الإدارة."); return; }

        // يتجاهل التقرير بحث الرقم والقسم الحاليين، ويعتمد فقط المقر والإدارة المحددين في هذه اللوحة.
        IEnumerable<PhoneLineRow> reportRows = rows.Where(x => x.Key.BuildingId == building);
        if (ReportByManagement.IsChecked == true && ReportManagement.SelectedValue is short management)
            reportRows = reportRows.Where(x => x.ManagementId == management);
        var result = reportRows.ToList();
        if (result.Count == 0)
        {
            UiMessages.Show(this, "تقرير حصر الهواتف", "لا توجد أرقام هواتف للمقر والمعايير المحددة.", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (OpenPrintPreview(result, ReportTitle.Text.Trim(), true)) ReportFlyout.IsOpen = false;
    }

    private bool OpenPrintPreview(List<PhoneLineRow> reportRows, string reportTitle, bool grouped)
    {
        try
        {

            ReleasePrint();
            // نمرر لقالب الكشف الصفوف المصفاة، ولقالب التقرير نتائج معاييره المستقلة دون تبديل مصدر الجدول.
            printReport = PhoneLineReport.Create(reportRows, reportTitle, grouped, App.location?.Branch?.Branch ?? string.Empty);
            printReport.CreateDocument();
            PrintPreview.DocumentSource = printReport;
            PrintFlyout.IsOpen = true;
            return true;
        }
        catch (Exception ex) { ReleasePrint(); UiMessages.Show(this, "تعذر إعداد الطباعة", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }
    private void ReleasePrint() { PrintPreview.DocumentSource = null; printReport?.Dispose(); printReport = null; }
    private void ClosePrint_Click(object sender, RoutedEventArgs e) => PrintFlyout.IsOpen = false;
    private void PrintFlyout_Changed(object sender, RoutedEventArgs e) { if (!PrintFlyout.IsOpen) ReleasePrint(); }
    private void Close_Click(object sender, RoutedEventArgs e) { if (!busy) Close(); }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (busy) return;
        if (e.Key == Key.Escape)
        {
            if (LineFlyout.IsOpen) LineFlyout.IsOpen = false;
            else if (ReportFlyout.IsOpen) ReportFlyout.IsOpen = false;
            else if (PrintFlyout.IsOpen) PrintFlyout.IsOpen = false;
            else Close();
            e.Handled = true;
        }
        else if (!LineFlyout.IsOpen && !PrintFlyout.IsOpen && !ReportFlyout.IsOpen)
        {
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { NewLine_Click(sender, e); e.Handled = true; }
            else if (e.Key == Key.Delete && LinesGrid.IsKeyboardFocusWithin && LinesGrid.SelectedItem is PhoneLineRow row) { _ = DeleteAsync(row); e.Handled = true; }
        }
    }
    private void Draft_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DraftManagement.IsDropDownOpen || DraftSection.IsDropDownOpen) return;
        // تبقى أزرار الحفظ والإلغاء قابلة للتنفيذ بـEnter؛ التنقل مخصص لمحررات المسودة فقط.
        if (Keyboard.FocusedElement is TextBox or ComboBox && Keyboard.FocusedElement is UIElement element)
        { element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); e.Handled = true; }
    }
    #endregion
}
