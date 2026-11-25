using DevExpress.Mvvm.Xpf;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using Inventory_System.converts;
using Inventory_System.Dashboard;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using Inventory_System.support_Data;
using Inventory_System.viewo_Model;
using MahApps.Metro.Controls;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Control = System.Windows.Controls.Control;

namespace Inventory_System.Inventory_Windows.owned_Windows;

public partial class owned_Insurance_Window : MetroWindow
{
    #region تهيئة الشاشة وهوية العقد والقوائم
    // تحفظ هوية السجل الأصلية حتى لا ينتقل التعديل إلى مبنى آخر عند تغير تحديد الجدول.
    private sealed record ServiceKey(long Id, long PremiseId, ServiceScope Scope);
    private ServiceKey? editingService;
    private int optype;
    private bool savingService;
    private List<T_insu_Compny> companies = [];
    private List<T_instu_Type> types = [];
    // لا يتضمن سجل التأمين رقم عقد؛ تقتصر المسودة على الشركة والنوع والقيمة والتواريخ.
    private Control[] DraftFields => [_combo_ins_copmpny, _combo_ins_type, Text_ins_Value, date_ins, date_ins_from, date_ins_to];
    private Control[] FilterFields => [Combo_Locaton, date_INS_begin, date_INS_end, _combo_ins_copmpny_SERASH, _combo_ins_type_SEARCH];
    public owned_Insurance_Window()
    {
        InitializeComponent();
        // تتبع الأزرار صلاحيات قوائم التأمين نفسها المستخدمة في الواجهة الرئيسية.
        AddServiceCompany.IsEnabled = ManageInsuranceCompanies.IsEnabled = CanManageInsuranceLookup(false);
        AddServiceContractType.IsEnabled = ManageInsuranceTypes.IsEnabled = CanManageInsuranceLookup(true);
    }
    private static bool CanManageInsuranceLookup(bool isType) =>
        Premises.PremisesSection.User is { } user && (isType ? user.sectype : user.sec_benef);
    private ServiceScope? SelectedScope => Combo_Locaton.SelectedItem is T_Inventory_Location row
        ? new(row.Branch_ID, row.Location_ID, row.Location) : null;
    private static bool SameScope(int branch, int locationId, string location, ServiceScope scope) =>
        branch == scope.BranchId && locationId == scope.LocationId && location == scope.Location;
    #endregion

    #region تحميل المباني وشركات التامين وأنواع التأمين
    private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Task.Yield();
            DbContextHelper.InitOptions(App.ConnectionString);
            await using var db = new Cont_maintContext(DbContextHelper.Options!);
            if (App._listbuilding == null)
                App._listbuilding = new ObservableCollection<T_Inventory_Location>(await db.T_Inventory_Locations.AsNoTracking()
                    .Include(item => item.T_OwnedPremises).OrderBy(item => item.In_Location).ToListAsync());
            Combo_Locaton.ItemsSource = App._listbuilding;
            await LoadLookupsAsync(db);
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر تحميل عقود التامين", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private async Task LoadLookupsAsync(Cont_maintContext db)
    {
        // تُحمّل البيانات أولًا، ثم تُفلتر بنطاق المبنى أو موقع الدخول؛ قد تتكرر الأرقام بين الفروع.
        companies = await db.T_insu_Compnies.AsNoTracking().OrderBy(item => item.insu_Compny).ToListAsync();
        types = await db.T_instu_Types.AsNoTracking().OrderBy(item => item.instu_Type).ToListAsync();
        PopulateLookups();
    }
    private void PopulateLookups()
    {
        if (_combo_ins_type_SEARCH == null) return;
        // تبدأ القوائم بنطاق موقع تسجيل الدخول قبل اختيار المبنى، ثم تتبع نطاق المبنى المحدد.
        // لا نعرض بيانات فروع أخرى عند غياب النطاق، ويظل الحفظ مشروطًا باختيار مبنى له ملكية.
        var scope = SelectedScope ?? (App.location is { } location
            ? new ServiceScope(location.Branch_ID, location.Location_ID, location.Location) : null);
        _combo_ins_copmpny_SERASH.ItemsSource = scope == null ? Array.Empty<T_insu_Compny>()
            : companies.Where(item => SameScope(item.Branch_ID, item.Location_ID, item.Location, scope)).ToArray();
        _combo_ins_type_SEARCH.ItemsSource = scope == null ? Array.Empty<T_instu_Type>()
            : types.Where(item => SameScope(item.Branch_ID, item.Location_ID, item.Location, scope)).ToArray();
        PopulateDraftLookups(editingService?.Scope ?? scope);
    }
    private void PopulateDraftLookups(ServiceScope? scope)
    {
        _combo_ins_copmpny.ItemsSource = scope == null ? Array.Empty<T_insu_Compny>()
            : companies.Where(item => SameScope(item.Branch_ID, item.Location_ID, item.Location, scope)).ToArray();
        _combo_ins_type.ItemsSource = scope == null ? Array.Empty<T_instu_Type>()
            : types.Where(item => SameScope(item.Branch_ID, item.Location_ID, item.Location, scope)).ToArray();
    }
    private void Combo_Locaton_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PopulateLookups();
        ApplySelectedFilters(false);
    }
    #endregion

    #region البحث وتفريغ الفلاتر
    private void DateFilter_Click(object sender, RoutedEventArgs e)
    {
        foreach (var date in new[] { date_INS_begin, date_INS_end }) { date.SelectedDate = null; date.Text = ""; FieldFeedback.Clear(date); }
        if (Date_Check.IsChecked == true) date_INS_begin.Focus();
    }
    private void CompanyFilter_Click(object sender, RoutedEventArgs e)
    {
        _combo_ins_copmpny_SERASH.SelectedIndex = -1; FieldFeedback.Clear(_combo_ins_copmpny_SERASH);
        if (by_INS_COMPNY.IsChecked == true) _combo_ins_copmpny_SERASH.Focus();
    }
    private void TypeFilter_Click(object sender, RoutedEventArgs e)
    {
        _combo_ins_type_SEARCH.SelectedIndex = -1; FieldFeedback.Clear(_combo_ins_type_SEARCH);
        if (by_INS_TYPE.IsChecked == true) _combo_ins_type_SEARCH.Focus();
    }
    private void SearchService_Click(object sender, RoutedEventArgs e) => ApplySelectedFilters(true);
    private void ApplySelectedFilters(bool showValidation)
    {
        if (DataContext is not owned_ins_Viewomodel model || _combo_ins_type_SEARCH == null) return;
        foreach (var field in FilterFields) FieldFeedback.Clear(field);
        void Reject(Control field, string text)
        {
            if (showValidation) FieldFeedback.Reject(field, text);
            else model.ApplyFilters(null, null, null, null, null);
        }
        if (Combo_Locaton.SelectedItem is not T_Inventory_Location premise)
        { Reject(Combo_Locaton, "اختر المبنى لعرض عقود شركات التامين."); return; }
        DateTime? from = null, to = null;
        if (Date_Check.IsChecked == true)
        {
            if (!date_INS_begin.SelectedDate.HasValue) { Reject(date_INS_begin, "اختر تاريخ بداية البحث."); return; }
            if (!date_INS_end.SelectedDate.HasValue) { Reject(date_INS_end, "اختر تاريخ نهاية البحث."); return; }
            from = date_INS_begin.SelectedDate; to = date_INS_end.SelectedDate;
            if (to < from) { Reject(date_INS_end, "تاريخ النهاية يجب ألا يسبق البداية."); return; }
        }
        int? company = null, type = null;
        if (by_INS_COMPNY.IsChecked == true)
        {
            if (_combo_ins_copmpny_SERASH.SelectedItem is not T_insu_Compny item) { Reject(_combo_ins_copmpny_SERASH, "اختر الشركة للبحث."); return; }
            company = item.insu_Compny_ID;
        }
        if (by_INS_TYPE.IsChecked == true)
        {
            if (_combo_ins_type_SEARCH.SelectedItem is not T_instu_Type item) { Reject(_combo_ins_type_SEARCH, "اختر نوع التأمين للبحث."); return; }
            type = item.instu_Type_ID;
        }
        model.ApplyFilters(premise, from, to, company, type);
    }
    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        Combo_Locaton.SelectedIndex = -1;
        Date_Check.IsChecked = by_INS_COMPNY.IsChecked = by_INS_TYPE.IsChecked = false;
        DateFilter_Click(sender, e); CompanyFilter_Click(sender, e); TypeFilter_Click(sender, e);
        insu_List.SelectedItem = null;
        foreach (var field in FilterFields) FieldFeedback.Clear(field);
        (DataContext as owned_ins_Viewomodel)?.ApplyFilters(null, null, null, null, null);
        Combo_Locaton.Focus();
    }
    #endregion

    #region فتح مسودة العقد وإلغاؤها دون حفظ
    private void NewService_Click(object sender, RoutedEventArgs e)
    {
        if (savingService) return;
        if (Combo_Locaton.SelectedItem is not T_Inventory_Location { T_OwnedPremises: not null })
        { FieldFeedback.Reject(Combo_Locaton, "اختر مبنى له بيانات ملكية أولًا."); return; }
        ClearDraft(); PopulateDraftLookups(SelectedScope); optype = 1;
        ServiceDraftTitle.Text = "بيانات عقد شركة التامين"; Service_Windows.IsOpen = true; _combo_ins_copmpny.Focus();
    }
    private void ServiceRow_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (savingService) return;
        var handle = ((TableView)insu_List.View).FocusedRowHandle;
        if (handle < 0 || insu_List.SelectedItem == null) return;
        ClearDraft();
        object Value(string field) => insu_List.GetCellValue(handle, field);
        editingService = new(Convert.ToInt64(Value("ID")), Convert.ToInt64(Value("RentalPremises_ID")),
            new(Convert.ToInt32(Value("Branch_ID")), Convert.ToInt32(Value("Location_ID")), Convert.ToString(Value("Location"))!));
        PopulateDraftLookups(editingService.Scope);
        _combo_ins_copmpny.SelectedValue = Value("insu_Compny_ID"); _combo_ins_type.SelectedValue = Value("INS_TYPE_ID");
        Text_ins_Value.EditValue = Value("INS_Value");
        date_ins.SelectedDate = ToDate(Value("Date_Input")); date_ins_from.SelectedDate = ToDate(Value("From_Date")); date_ins_to.SelectedDate = ToDate(Value("To_Date"));
        optype = 2; ServiceDraftTitle.Text = "تعديل عقد شركة التامين"; Service_Windows.IsOpen = true;
        foreach (var field in DraftFields) FieldFeedback.Clear(field);
    }
    private static DateTime? ToDate(object value) => value switch { DateOnly date => date.ToDateTime(TimeOnly.MinValue), DateTime date => date, _ => null };
    private void ClearDraft()
    {
        editingService = null; optype = 0; _combo_ins_copmpny.SelectedIndex = _combo_ins_type.SelectedIndex = -1;
        Text_ins_Value.EditValue = null;
        foreach (var date in new[] { date_ins, date_ins_from, date_ins_to }) { date.SelectedDate = null; date.Text = ""; }
        foreach (var field in DraftFields) FieldFeedback.Clear(field);
    }
    private void CancelDraft_Click(object sender, RoutedEventArgs e) { if (!savingService) Service_Windows.IsOpen = false; }
    private void ServiceFlyout_Changed(object sender, RoutedEventArgs e) { if (!Service_Windows.IsOpen) ClearDraft(); }
    #endregion

    #region التحقق وحفظ الإضافة والتعديل
    private bool ValidateServiceDraft(out decimal amount)
    {
        amount = 0;
        foreach (var field in DraftFields) FieldFeedback.Clear(field);
        var scope = editingService?.Scope ?? SelectedScope;
        if (scope == null || (optype == 1 && Combo_Locaton.SelectedItem is not T_Inventory_Location { T_OwnedPremises: not null }))
            return FieldFeedback.Reject(Combo_Locaton, "اختر مبنى له بيانات ملكية.");
        if (_combo_ins_copmpny.SelectedItem is not T_insu_Compny company || !SameScope(company.Branch_ID, company.Location_ID, company.Location, scope))
            return FieldFeedback.Reject(_combo_ins_copmpny, "اختر شركة التامين التابعة لنطاق المبنى.");
        if (_combo_ins_type.SelectedItem is not T_instu_Type type || !SameScope(type.Branch_ID, type.Location_ID, type.Location, scope))
            return FieldFeedback.Reject(_combo_ins_type, "اختر نوع التأمين التابع لنطاق المبنى.");
        // الصفر قيمة صالحة؛ نحافظ على حدود numeric(18,3) قبل إرسال القيمة لقاعدة البيانات.
        if (Text_ins_Value.EditValue == null || !decimal.TryParse(Convert.ToString(Text_ins_Value.EditValue, CultureInfo.CurrentCulture), NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
            return FieldFeedback.Reject(Text_ins_Value, "أدخل قيمة رقمية للعقد.");
        if (amount <= -1000000000000000m || amount >= 1000000000000000m || decimal.Round(amount, 3) != amount)
            return FieldFeedback.Reject(Text_ins_Value, "القيمة لا تتجاوز 15 رقمًا صحيحًا وثلاث منازل عشرية.");
        if (!date_ins.SelectedDate.HasValue) return FieldFeedback.Reject(date_ins, "أدخل تاريخ العقد.");
        if (!date_ins_from.SelectedDate.HasValue) return FieldFeedback.Reject(date_ins_from, "أدخل بداية فترة التعاقد.");
        if (!date_ins_to.SelectedDate.HasValue) return FieldFeedback.Reject(date_ins_to, "أدخل نهاية فترة التعاقد.");
        return date_ins_to.SelectedDate >= date_ins_from.SelectedDate || FieldFeedback.Reject(date_ins_to, "تاريخ النهاية يجب ألا يسبق البداية.");
    }
    private async Task<bool> SaveServiceAsync(Cont_maintContext db)
    {
        if (DataContext is not owned_ins_Viewomodel model || optype is not (1 or 2) || !ValidateServiceDraft(out var amount)) return false;
        // تُؤخذ لقطة القيم والمفتاح قبل أول انتظار؛ لا تؤثر الكتابة اللاحقة في عملية الحفظ الجارية.
        var key = editingService; bool insert = optype == 1;
        var scope = key?.Scope ?? SelectedScope!;
        var candidate = new T_OwnedPremises_Insuc
        {
            ID = insert ? 0 : key!.Id,
            RentalPremises_ID = insert ? ((T_Inventory_Location)Combo_Locaton.SelectedItem).T_OwnedPremises.RentalPremises_ID : key!.PremiseId,
            Branch_ID = scope.BranchId,
            Location_ID = scope.LocationId,
            Location = scope.Location,
            insu_Compny_ID = ((T_insu_Compny)_combo_ins_copmpny.SelectedItem).insu_Compny_ID,
            INS_TYPE_ID = ((T_instu_Type)_combo_ins_type.SelectedItem).instu_Type_ID,
            INS_Value = amount,
             Date_Input = DateOnly.FromDateTime(date_ins.SelectedDate!.Value),
            From_Date = DateOnly.FromDateTime(date_ins_from.SelectedDate!.Value),
            To_Date = DateOnly.FromDateTime(date_ins_to.SelectedDate!.Value),
            Date_Modify = DateOnly.FromDateTime(DateTime.Now)
        };
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (await model.HasContractForMonthAsync(db, candidate)) return FieldFeedback.Reject(date_ins, "تم تسجيل عقد للشركة ونوع التأمين نفسيهما في الشهر والسنة نفسيهما.");
        if (await model.HasOverlappingContractAsync(db, candidate)) return FieldFeedback.Reject(date_ins_from, "فترة التعاقد تتداخل مع عقد آخر للشركة والنوع نفسيهما داخل المبنى.");
        T_OwnedPremises_Insuc contract;
        if (insert) { contract = candidate; db.T_OwnedPremises_Insucs.Add(contract); }
        else
        {
            contract = await db.T_OwnedPremises_Insucs.SingleAsync(item => item.ID == candidate.ID && item.Branch_ID == candidate.Branch_ID
                && item.Location_ID == candidate.Location_ID && item.Location == candidate.Location && item.RentalPremises_ID == candidate.RentalPremises_ID);
            db.Entry(contract).CurrentValues.SetValues(candidate);
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        editingService = new(contract.ID, contract.RentalPremises_ID, scope); optype = 2;
        ServiceDraftTitle.Text = "تعديل عقد شركة التامين"; model.DataSourceRefresh(new DataSourceRefreshArgs()); return true;
    }
    private async void SaveService_Click(object sender, RoutedEventArgs e)
    {
        if (savingService) return;
        savingService = true; save_ins.IsEnabled = false;
        try
        {
            DbContextHelper.InitOptions(App.ConnectionString); await using var db = new Cont_maintContext(DbContextHelper.Options!);
            if (await SaveServiceAsync(db)) UiMessages.Show(this, "عقود شركات التامين", "تم حفظ العقد بنجاح.", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر حفظ العقد", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { savingService = false; save_ins.IsEnabled = true; }
    }
    #endregion

    #region إدارة القوائم المساعدة وتحديثها دون فقدان المسودة
    private void ManageCompanies_Click(object sender, RoutedEventArgs e) => ManageLookup(false);
    private void ManageTypes_Click(object sender, RoutedEventArgs e) => ManageLookup(true);
    private async void ManageLookup(bool isType)
    {
        // نطاق السجل الجاري تعديله أولًا، ثم المبنى، ثم موقع الدخول عند فتح القائمة من البحث.
        if (!CanManageInsuranceLookup(isType)) return;
        var scope = editingService?.Scope ?? SelectedScope ?? (App.location is { } location
            ? new ServiceScope(location.Branch_ID, location.Location_ID, location.Location) : null);
        if (scope == null) { FieldFeedback.Reject(Combo_Locaton, "اختر المبنى أولًا لتحديد نطاق القائمة."); return; }
        if (savingService) return;
        // نعيد تحميل القوائم بعد الإغلاق مع حفظ اختيارات البحث والمسودة إن بقيت سجلاتها موجودة.
        var company = _combo_ins_copmpny.SelectedValue; var type = _combo_ins_type.SelectedValue;
        var filterCompany = _combo_ins_copmpny_SERASH.SelectedValue; var filterType = _combo_ins_type_SEARCH.SelectedValue;
        new InsuranceLookupWindow(scope, isType) { Owner = this }.ShowDialog();
        try
        {
            DbContextHelper.InitOptions(App.ConnectionString); await using var db = new Cont_maintContext(DbContextHelper.Options!);
            await LoadLookupsAsync(db);
            _combo_ins_copmpny.SelectedValue = company; _combo_ins_type.SelectedValue = type;
            _combo_ins_copmpny_SERASH.SelectedValue = filterCompany; _combo_ins_type_SEARCH.SelectedValue = filterType;
            // تعرض أعمدة الجدول أسماء الشركات والأنواع الجديدة بعد تعديل القائمة المساعدة.
            (DataContext as owned_ins_Viewomodel)?.DataSourceRefresh(new DataSourceRefreshArgs());
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر تحديث القوائم", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    #endregion

    #region اختصارات النافذة والتنقل بين الخانات
    private void Close_Click(object sender, RoutedEventArgs e) { if (!savingService) Close(); }
    private void ServiceWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Service_Windows.IsOpen) { if (!savingService) Service_Windows.IsOpen = false; e.Handled = true; }
        else if (e.Key == Key.F5 && !Service_Windows.IsOpen) { ApplySelectedFilters(true); e.Handled = true; }
    }
    private void Field_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not UIElement field) return;
        if (field is System.Windows.Controls.ComboBox { IsDropDownOpen: true }) return;
        UIElement? next = field == date_ins ? date_ins_from : field == date_ins_from ? date_ins_to
            : field == date_ins_to ? save_ins : field == date_INS_begin ? date_INS_end
            : field == date_INS_end ? (by_INS_COMPNY.IsChecked == true ? _combo_ins_copmpny_SERASH : Search_Button) : null;
        e.Handled = true;
        if (field is DatePicker date) date.IsDropDownOpen = false;
        // ندخل إلى نص التاريخ التالي مباشرة بدل الوقوف داخل زر التقويم.
        if (next is DatePicker nextDate) { nextDate.ApplyTemplate(); (nextDate.Template.FindName("PART_TextBox", nextDate) as UIElement ?? nextDate).Focus(); }
        else if (next != null) next.Focus();
        else field.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }
    #endregion
}
