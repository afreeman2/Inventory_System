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

public partial class ServiceLookupWindow : MetroWindow
{
    #region تهيئة نافذة الشركات أو أنواع التعاقد
    private readonly ServiceScope scope;
    private readonly bool isType;
    private int? editingId;
    private bool busy;
    public ServiceLookupWindow(ServiceScope scope, bool isType)
    {
        InitializeComponent(); this.scope = scope; this.isType = isType;
        // واجهة واحدة للشركة والنوع بنفس ترتيب المستفيد؛ تختلف العناوين والأيقونات ومصدر البيانات فقط.
        Title = WindowHeading.Text = isType ? "إدارة أنواع التعاقد" : "إدارة شركات الخدمات";
        NameLabel.Text = EditNameLabel.Text = isType ? "نوع التعاقد:" : "الشركة:";
        NameColumn.Header = isType ? "أنواع التعاقد" : "شركات الخدمات";
        EditHeading.Text = isType ? "تعديل نوع التعاقد" : "تعديل شركة الخدمات";
        AddName.NullText = isType ? "أدخل نوع التعاقد" : "أدخل شركة الخدمات";
        // يأخذ بحث الجدول النص الإرشادي من View عند ربطه، فنحافظ على ظهوره بالعربية بعد التحميل.
        LookupView.SearchPanelNullText = AddName.NullText;
        AutomationProperties.SetName(AddName, isType ? "نوع التعاقد والبحث" : "اسم شركة الخدمات والبحث");
        AutomationProperties.SetName(EditName, isType ? "تعديل نوع التعاقد" : "تعديل اسم شركة الخدمات");
        HeaderIcon.Kind = EditHeaderIcon.Kind = isType ? PackIconMaterialKind.FormatListBulletedType : PackIconMaterialKind.OfficeBuildingOutline;
        // أيقونة الخانة تعبر عن الشركة أو النوع، وتبقى داخل الإطار عند ظهور تنبيه التحقق.
        NameFieldIcon.Kind = EditFieldIcon.Kind = HeaderIcon.Kind;
        Closing += (_, e) => { if (busy) e.Cancel = true; };
    }
    private static Cont_maintContext OpenContext()
    {
        DbContextHelper.InitOptions(App.ConnectionString); return new Cont_maintContext(DbContextHelper.Options!);
    }
    private async Task ReloadAsync(Cont_maintContext db)
    {
        // البيانات المساعدة صغيرة؛ يظهر الاسم في جدول قابل للبحث مع تعطيل التحرير المباشر.
        LookupGrid.ItemsSource = await ServiceLookupStore.LoadAsync(db, scope, isType);
    }
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try { using var db = OpenContext(); await ReloadAsync(db); AddName.Focus(); }
        catch (Exception ex) { UiMessages.Show(this, "تعذر تحميل القائمة", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    #endregion

    #region البحث العربي بنفس آلية واجهة المستفيد
    private void View_SearchStringToFilterCriteria(object sender, SearchStringToFilterCriteriaEventArgs e) =>
        ArabicGridSearch.ApplyLookup(e);
    #endregion

    #region إضافة الاسم والتحقق منه
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        FieldFeedback.Clear(AddName);
        // نأخذ نص البحث نفسه للحفظ مثل المستفيد، مع إبقاء حد الاسم الأصلي في مخزن الخدمات.
        var name = AddName.SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(name)) { FieldFeedback.Reject(AddName, "أدخل الاسم أولًا."); return; }
        if (name.Length > 50) { FieldFeedback.Reject(AddName, "أدخل الاسم بما لا يتجاوز 50 حرفًا."); return; }
        busy = true;
        try
        {
            using var db = OpenContext();
            var error = await ServiceLookupStore.SaveAsync(db, scope, isType, null, name);
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
        if (busy || LookupGrid.SelectedItem is not ServiceLookupEntry item) return;
        editingId = item.Id; EditName.Text = item.Name; FieldFeedback.Clear(EditName); EditFlyout.IsOpen = true;
        EditName.Focus();
    }
    private async void SaveEdit_Click(object sender, RoutedEventArgs e)
    {
        if (busy || !editingId.HasValue) return;
        FieldFeedback.Clear(EditName);
        if (string.IsNullOrWhiteSpace(EditName.Text)) { FieldFeedback.Reject(EditName, "أدخل الاسم أولًا."); return; }
        busy = true;
        try
        {
            using var db = OpenContext();
            var error = await ServiceLookupStore.SaveAsync(db, scope, isType, editingId, EditName.Text);
            if (error != null) { FieldFeedback.Reject(EditName, error); return; }
            EditFlyout.IsOpen = false; await ReloadAsync(db);
        }
        catch (Exception ex) { UiMessages.Show(this, "تعذر تعديل البيان", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; }
    }
    private void CancelEdit_Click(object sender, RoutedEventArgs e) { if (!busy) EditFlyout.IsOpen = false; }
    private void EditFlyout_Changed(object sender, RoutedEventArgs e)
    {
        if (!EditFlyout.IsOpen) { EditName.Clear(); editingId = null; FieldFeedback.Clear(EditName); AddName.Focus(); }
    }
    #endregion

    #region حذف البيان بعد التأكيد وحماية العقود المرتبطة
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (busy || (sender as FrameworkElement)?.DataContext is not EditGridCellData cell || cell.RowData.Row is not ServiceLookupEntry item) return;
        await DeleteEntryAsync(item);
    }
    private async Task DeleteEntryAsync(ServiceLookupEntry item)
    {
        // زر الصف وDelete يستخدمان التأكيد والحماية نفسها؛ لا نعتمد صفًا آخر عند الضغط على الزر.
        if (busy) return;
        busy = true;
        try
        {
            // اعرض سبب المنع قبل طلب التأكيد، وأعد فحص الارتباط عند تنفيذ الحذف.
            using var db = OpenContext();
            var reason = await ServiceLookupStore.GetDeleteBlockReasonAsync(db, scope, isType, item.Id);
            if (reason is not null)
            {
                UiMessages.Show(this, "تعذر حذف البيان", reason, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (UiMessages.Show(this, "حذف البيان", "هل تريد حذف البيان المحدد؟",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var error = await ServiceLookupStore.DeleteAsync(db, scope, isType, item.Id);
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
        else if (e.Key == Key.Delete && LookupGrid.IsKeyboardFocusWithin && LookupGrid.SelectedItem is ServiceLookupEntry item)
        { _ = DeleteEntryAsync(item); e.Handled = true; }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control && !EditFlyout.IsOpen)
        { if (!busy) { AddName.SearchText = null; FieldFeedback.Clear(AddName); AddName.Focus(); } e.Handled = true; }
    }
    #endregion
}
