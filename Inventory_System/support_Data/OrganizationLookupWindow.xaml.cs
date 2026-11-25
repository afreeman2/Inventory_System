using Microsoft.EntityFrameworkCore;
using DevExpress.Xpf.Grid;
using Inventory_System.converts;
using System.Windows.Automation;
using Inventory_System.Dashboard;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Inventory_System.support_Data;

public partial class OrganizationLookupWindow : MetroWindow
{
    #region تهيئة نافذتي الإدارات والأقسام والوحدات
    private readonly ServiceScope scope;
    private readonly bool isType;
    private short? editingId;
    private bool busy;
    // يمنع تحديث عناصر نافذة أُغلقت أثناء انتظار تحميل القوائم.
    private bool closed;
    private int loadVersion;
    public OrganizationLookupWindow(ServiceScope scope, bool isType, short? initialManagement = null)
    {
        InitializeComponent(); this.scope = scope; this.isType = isType;
        // نفس أسلوب البيانات المساعدة؛ القسم يحتفظ بإدارته الأصلية عند تعديل الاسم.
        Title = WindowHeading.Text = isType ? "الأقسام أو الوحدات" : "الإدارات والأقسام";
        NameLabel.Text = EditNameLabel.Text = isType ? "القسم أو الوحدة:" : "الإدارة أو القسم:";
        NameColumn.Header = isType ? "القسم أو الوحدة" : "الإدارة أو القسم";
        EditHeading.Text = isType ? "تعديل قسم أو وحدة" : "تعديل إدارة أو قسم";
        AddName.NullText = LookupView.SearchPanelNullText = "أدخل الاسم للبحث أو الإضافة";
        HeaderIcon.Kind = EditHeaderIcon.Kind = NameFieldIcon.Kind = EditFieldIcon.Kind = isType ? PackIconMaterialKind.FileTree : PackIconMaterialKind.Domain;
        AddManagementGroup.Visibility = EditManagementGroup.Visibility = isType ? Visibility.Visible : Visibility.Collapsed;
        ManagementColumn.Visible = isType; this.initialManagement = initialManagement;
        AddButton.IsEnabled = CanManage;
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Closed += (_, _) => { closed = true; ++loadVersion; };
    }
    private static Cont_maintContext OpenContext()
    {
        DbContextHelper.InitOptions(App.ConnectionString); return new Cont_maintContext(DbContextHelper.Options!);
    }
    private async Task ReloadAsync(Cont_maintContext db)
    {
        var version = ++loadVersion;
        // نحتفظ بتحديد الإدارة خلال التحديث، ويظل البحث النصي مربوطًا بالجدول نفسه.
        var manager = AddManagement.SelectedValue ?? initialManagement;
        var managers = await OrganizationLookupStore.Managers(db, scope).AsNoTracking().OrderBy(x => x.Manag).ToListAsync();
        var entries = await OrganizationLookupStore.LoadAsync(db, scope, isType);
        if (closed || version != loadVersion) return;
        allEntries = entries;
        AddManagement.ItemsSource = EditManagement.ItemsSource = managers;
        AddManagement.SelectedValue = manager; ApplyManagementFilter();
    }
    private readonly short? initialManagement;
    private List<OrganizationEntry> allEntries = [];
    private static bool CanManage => Inventory_System.Premises.PremisesSection.User?.mang_check == true;
    private void ApplyManagementFilter() => LookupGrid.ItemsSource = isType && AddManagement.SelectedValue is short id
        ? allEntries.Where(x => x.ManagementId == id).ToList() : allEntries;
    private void Management_Changed(object sender, SelectionChangedEventArgs e) { if (LookupGrid != null) ApplyManagementFilter(); }
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!CanManage) return;
        try { using var db = OpenContext(); await ReloadAsync(db); AddName.Focus(); }
        catch (Exception ex) { if (!closed) UiMessages.Show(this, "تعذر تحميل القائمة", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    #endregion

    #region البحث العربي بنفس آلية واجهة المستفيد
    private void View_SearchStringToFilterCriteria(object sender, SearchStringToFilterCriteriaEventArgs e) =>
        ArabicGridSearch.ApplyLookup(e);
    #endregion

    #region إضافة الاسم والتحقق منه
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanManage) return;
        FieldFeedback.Clear(AddName); FieldFeedback.Clear(AddManagement);
        if (isType && AddManagement.SelectedValue is not short) { FieldFeedback.Reject(AddManagement, "اختر الإدارة أو القسم أولًا."); return; }
        // نأخذ نص البحث نفسه للحفظ مثل المستفيد، مع إبقاء حد الاسم الأصلي في مخزن الإدارات والأقسام.
        var name = AddName.SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(name)) { FieldFeedback.Reject(AddName, "أدخل الاسم أولًا."); return; }
        if (name.Length > 50) { FieldFeedback.Reject(AddName, "أدخل الاسم بما لا يتجاوز 50 حرفًا."); return; }
        busy = true;
        try
        {
            using var db = OpenContext();
            var error = await OrganizationLookupStore.SaveAsync(db, scope, isType, null, name, AddManagement.SelectedValue as short?);
            if (error != null) { FieldFeedback.Reject(AddName, error); return; }
            AddName.SearchText = null; await ReloadAsync(db); AddName.Focus();
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر إضافة البيان", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; }
    }
    #endregion

    #region تعديل الاسم داخل اللوحة وإلغاؤه دون حفظ
    private void EditRow_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (busy || !CanManage || LookupGrid.SelectedItem is not OrganizationEntry item) return;
        editingId = item.Id; EditManagement.SelectedValue = item.ManagementId; EditName.Text = item.Name; FieldFeedback.Clear(EditName); EditFlyout.IsOpen = true;
        EditName.Focus();
    }
    private async void SaveEdit_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanManage || !editingId.HasValue) return;
        FieldFeedback.Clear(EditName);
        if (string.IsNullOrWhiteSpace(EditName.Text)) { FieldFeedback.Reject(EditName, "أدخل الاسم أولًا."); return; }
        busy = true;
        try
        {
            using var db = OpenContext();
            var error = await OrganizationLookupStore.SaveAsync(db, scope, isType, editingId, EditName.Text, EditManagement.SelectedValue as short?);
            if (error != null) { FieldFeedback.Reject(EditName, error); return; }
            EditFlyout.IsOpen = false; await ReloadAsync(db);
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر تعديل البيان", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; }
    }
    private void CancelEdit_Click(object sender, RoutedEventArgs e) { if (!busy) EditFlyout.IsOpen = false; }
    private void EditFlyout_Changed(object sender, RoutedEventArgs e)
    {
        if (!EditFlyout.IsOpen) { EditName.Clear(); EditManagement.SelectedIndex = -1; editingId = null; FieldFeedback.Clear(EditName); AddName.Focus(); }
    }
    #endregion

    #region حذف البيان بعد التأكيد وحماية الخطوط والأقسام والأثاث المرتبط
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !CanManage || (sender as FrameworkElement)?.DataContext is not EditGridCellData cell || cell.RowData.Row is not OrganizationEntry item) return;
        await DeleteEntryAsync(item);
    }
    private async Task DeleteEntryAsync(OrganizationEntry item)
    {
        // زر الصف وDelete يستخدمان التأكيد والحماية نفسها؛ لا نعتمد صفًا آخر عند الضغط على الزر.
        if (busy || !CanManage) return;
        busy = true;
        try
        {
            // اعرض سبب المنع قبل طلب التأكيد، وأعد فحص الارتباط عند تنفيذ الحذف.
            using var db = OpenContext();
            var reason = await OrganizationLookupStore.GetDeleteBlockReasonAsync(db, scope, isType, item.Id);
            if (reason is not null)
            {
                UiMessages.Show(this, "تعذر حذف البيان", reason, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!CanManage) return;
            if (UiMessages.Show(this, "حذف البيان", "هل تريد حذف البيان المحدد؟",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var error = await OrganizationLookupStore.DeleteAsync(db, scope, isType, item.Id);
            if (error != null) { UiMessages.Show(this, "تعذر حذف البيان", error, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            await ReloadAsync(db);
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر حذف البيان", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) { if (!busy) Close(); }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && EditFlyout.IsOpen) { if (!busy) EditFlyout.IsOpen = false; e.Handled = true; }
        else if (e.Key == Key.Escape) { if (!busy) Close(); e.Handled = true; }
        else if (e.Key == Key.Enter && AddName.IsKeyboardFocusWithin) { Add_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.Enter && EditName.IsKeyboardFocusWithin) { SaveEdit_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        // اختصارات الجدول مثل المستفيد، والتعديل يبقى داخل Flyout بدل التحرير المباشر.
        else if (e.Key == Key.F2 && LookupGrid.IsKeyboardFocusWithin) { EditRow_DoubleClick(LookupGrid, null!); e.Handled = true; }
        else if (e.Key == Key.Delete && LookupGrid.IsKeyboardFocusWithin && LookupGrid.SelectedItem is OrganizationEntry item)
        { _ = DeleteEntryAsync(item); e.Handled = true; }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control && !EditFlyout.IsOpen)
        { if (!busy) { AddName.SearchText = null; FieldFeedback.Clear(AddName); AddName.Focus(); } e.Handled = true; }
    }
    #endregion
}


