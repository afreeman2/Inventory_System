using Inventory_System.Dashboard;
using Inventory_System.Inventory_Windows.owned_Windows;
using Inventory_System.Premises;
using Inventory_System.Archive;
using Inventory_System.Furniture;
using Inventory_System.support_Data;
using MahApps.Metro.IconPacks;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Inventory_System.MainWindows;

public partial class MainWindow
{
    #region قوائم الحصر والوصول السريع
    private PremisesData premisesData = null!;
    private PremisesKind selectedKind;

    private sealed record WorkspaceAction(string Section, string Title, string Description, string Shortcut,
        PackIconMaterialKind Icon, Func<bool> CanOpen, Action Open)
    {
        public bool IsEnabled => CanOpen();
    }

    private sealed record PremisesCard(PremisesSection Section, bool IsEnabled, string Description)
    {
        public string Title => Section.Title;
        public PackIconMaterialKind Icon => Section.Icon;
    }

    private WorkspaceAction[] workspaceActions = [];

    private void InitializeWorkspace()
    {
        var locations = PremisesSection.All[0];
        // نربط زر المستفيدين واختصاره القديم بنفس الإجراء والصلاحية.
        var beneficiaries = new WorkspaceAction("reference", "المستفيدون من المقرات",
            "فتح بيانات المستفيدين من المقرات.", "Alt+P", PackIconMaterialKind.AccountGroupOutline,
            CanManageBeneficiaries, OpenBeneficiaries);
        // تستخدم القوائم والوصول السريع والاختصارات الإجراءات نفسها وصلاحيات الحساب المسجّل.
        workspaceActions =
        [
            new("operations", "حصر المقرات المملوكة", "فتح أقسام حصر المقرات المملوكة.", "Ctrl+D",
                PackIconMaterialKind.OfficeBuildingOutline, () => CanOpenPremises(PremisesKind.Owned), () => OpenPremises(PremisesKind.Owned)),
            new("operations", "حصر المقرات المؤجرة", "فتح أقسام حصر المقرات المؤجرة.", "Ctrl+C",
                PackIconMaterialKind.KeyOutline, () => CanOpenPremises(PremisesKind.Rented), () => OpenPremises(PremisesKind.Rented)),
            // صلاحية forfut القديمة تفتح حصر الأثاث وسجلات أنواعه وفئاته في موقع الدخول.
            new("operations", "حصر الأثاث", "إضافة سجلات الحصر وبنود الأثاث والبحث فيها.", "",
                PackIconMaterialKind.ClipboardListOutline, CanManageFurniture, OpenFurnitureInventory),
            new("reference", "بيانات المقرات المملوكة", "إضافة وتعديل أسماء المقرات المملوكة وعناوينها.", "Ctrl+F",
                PackIconMaterialKind.OfficeBuildingOutline, () => locations.CanOpen(PremisesKind.Owned),
                () => OpenRegister(PremisesKind.Owned, locations)),
            new("reference", "بيانات المقرات المؤجرة", "إضافة وتعديل أسماء المقرات المؤجرة وعناوينها.", "Ctrl+G",
                PackIconMaterialKind.KeyOutline, () => locations.CanOpen(PremisesKind.Rented),
                () => OpenRegister(PremisesKind.Rented, locations)),
            // تفتح البيانات المساعدة نفس واجهتي العقود، بصلاحيات المصدر ونطاق موقع تسجيل الدخول.
            new("reference", "شركات الخدمات", "إضافة وتعديل شركات الخدمات في الفرع والموقع الحاليين.", "Alt+U",
                PackIconMaterialKind.OfficeBuildingPlusOutline, () => CanManageServiceLookup(false), () => OpenServiceLookup(false)),
            new("reference", "أنواع التعاقد", "إضافة وتعديل أنواع عقود شركات الخدمات.", "Alt+V",
                PackIconMaterialKind.FormatListBulletedType, () => CanManageServiceLookup(true), () => OpenServiceLookup(true)),
            beneficiaries,
            // قوائم التأمين مستقلة عن شركات الخدمات وتتبع صلاحيات التأمين وموقع الدخول.
            new("reference", "شركات التأمين", "إضافة وتعديل شركات التأمين في الفرع والموقع الحاليين.", "",
                PackIconMaterialKind.ShieldCheckOutline, () => CanManageInsuranceLookup(false), () => OpenInsuranceLookup(false)),
            new("reference", "أنواع التأمين", "إضافة وتعديل أنواع التأمين في الفرع والموقع الحاليين.", "",
                PackIconMaterialKind.FormatListBulletedType, () => CanManageInsuranceLookup(true), () => OpenInsuranceLookup(true)),
            // نفس صلاحية الإدارات في المصدر القديم للواجهتين المساعدتين.
            new("reference", "الإدارات والأقسام", "إضافة وتعديل الإدارات والأقسام في موقع الدخول.", "",
                PackIconMaterialKind.Domain, CanManageOrganizations, () => OpenOrganizations(false)),
            new("reference", "الأقسام أو الوحدات", "إضافة وتعديل الوحدات التابعة لكل إدارة أو قسم.", "",
                PackIconMaterialKind.FileTree, CanManageOrganizations, () => OpenOrganizations(true)),
            new("reference", "أنواع الأثاث", "إضافة وتعديل أنواع الأثاث وباركوداتها وفئاتها.", "",
                PackIconMaterialKind.ShapeOutline, CanManageFurniture, () => OpenFurnitureCatalog(true)),
            new("reference", "فئات الأثاث", "إضافة وتعديل فئات الأثاث ونسب استهلاكها.", "",
                PackIconMaterialKind.FormatListBulleted, CanManageFurniture, () => OpenFurnitureCatalog(false)),
            new("reports", "استعراض المقرات المملوكة", "عرض سجل المقرات المملوكة وتصفية نتائجه.", "",
                PackIconMaterialKind.FileDocumentOutline, () => PremisesSection.CanReport(PremisesKind.Owned),
                () => OpenRegister(PremisesKind.Owned, locations, report: true)),
            new("reports", "استعراض المقرات المؤجرة", "عرض سجل المقرات المؤجرة وتصفية نتائجه.", "",
                PackIconMaterialKind.FileDocumentOutline, () => PremisesSection.CanReport(PremisesKind.Rented),
                () => OpenRegister(PremisesKind.Rented, locations, report: true)),
            new("settings", "إغلاق المنظومة", "إنهاء العمل وإغلاق المنظومة.", "Alt+F4",
                PackIconMaterialKind.Logout, () => true, Close),
            new("help", "حول المنظومة", "التعريف بمنظومة حصر المقرات.", "F1",
                PackIconMaterialKind.InformationOutline, () => true, ShowPremisesAbout)
        ];
        RegisterPremisesShortcut(workspaceActions[0], Key.D);
        RegisterPremisesShortcut(workspaceActions[1], Key.C);
        RegisterPremisesShortcut(workspaceActions[2], Key.F);
        RegisterPremisesShortcut(workspaceActions[3], Key.G);
        RegisterPremisesShortcut(workspaceActions[4], Key.U, ModifierKeys.Alt);
        RegisterPremisesShortcut(workspaceActions[5], Key.V, ModifierKeys.Alt);
        RegisterPremisesShortcut(beneficiaries, Key.P, ModifierKeys.Alt);
        RefreshWorkspace();
    }

    private void RegisterPremisesShortcut(WorkspaceAction action, Key key, ModifierKeys modifiers = ModifierKeys.Control)
    {
        var command = new RoutedCommand();
        CommandBindings.Add(new CommandBinding(command, (_, _) => ExecuteWorkspaceAction(action),
            (_, args) => { args.CanExecute = !PremisesFlyout.IsOpen && action.IsEnabled; args.Handled = true; }));
        InputBindings.Add(new KeyBinding(command, key, modifiers));
    }

    private void RefreshWorkspace()
    {
        if (SectionCommands is null || QuickAccessCommands is null) return;
        SectionCommands.ItemsSource = workspaceActions.Where(action => action.Section == workspaceSection).ToArray();
        QuickAccessCommands.ItemsSource = workspaceActions.Where(action => action.Section is "operations" or "reference").ToArray();
    }

    private void WorkspaceSection_Checked(object sender, RoutedEventArgs e)
    {
        workspaceSection = (string)((System.Windows.Controls.RadioButton)sender).Tag;
        RefreshWorkspace();
    }

    private void QuickAccess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WorkspaceAction action })
            ExecuteWorkspaceAction(action);
    }

    private void ExecuteWorkspaceAction(WorkspaceAction action)
    {
        if (!PremisesFlyout.IsOpen && action.IsEnabled) action.Open();
    }

    #region فتح بيانات المستفيدين من المقرات
    private static bool CanManageBeneficiaries() => PremisesSection.User?.benef_check == true && App.location is not null;

    private void OpenBeneficiaries()
    {
        // يعاد فحص الصلاحية عند التنفيذ قبل فتح نافذة المستفيدين الحالية.
        if (!CanManageBeneficiaries()) return;
        new benef_window { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }
    #endregion

    #region فتح شاشات الأثاث بصلاحية الحساب وموقع الدخول
    private static bool CanManageFurniture() => PremisesSection.User?.forfut == true && App.location is not null;

    private void OpenFurnitureInventory()
    {
        if (!CanManageFurniture()) return;
        new FurnitureInventoryWindow { Owner = this }.ShowDialog();
    }

    private void OpenFurnitureCatalog(bool types)
    {
        if (!CanManageFurniture()) return;
        // لكل بيان مساعد نافذته المستقلة المملوكة للرئيسية.
        if (types) new FurnitureTypeWindow { Owner = this }.ShowDialog();
        else new FurnitureClassWindow { Owner = this }.ShowDialog();
    }
    #endregion

    private static bool CanOpenPremises(PremisesKind kind) =>
        PremisesSection.All.Any(section => section.CanOpen(kind));

    private void OpenPremises(PremisesKind kind)
    {
        if (!CanOpenPremises(kind)) return;
        selectedKind = kind;
        PremisesFlyoutTitle.Text = "حصر " + PremisesSection.KindName(kind);
        PremisesChoices.ItemsSource = PremisesSection.All.Select(section => new PremisesCard(section,
            section.CanOpen(kind), section.Title)).ToArray();
        PremisesFlyout.IsOpen = true;
        Dispatcher.InvokeAsync(() => ClosePremisesFlyoutButton.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void PremisesCard_Click(object sender, RoutedEventArgs e)
    {
        if (PremisesFlyout.IsOpen && sender is FrameworkElement { DataContext: PremisesCard { IsEnabled: true } card })
            OpenRegister(selectedKind, card.Section);
    }

    private void OpenRegister(PremisesKind kind, PremisesSection section, bool report = false)
    {
        // تُفحص صلاحية القسم أو التقرير قبل إنشاء النافذة المستهدفة.
        if (report ? !PremisesSection.CanReport(kind) : !section.CanOpen(kind)) return;

        Window window;
        // نوع الأرشيف يعزل جداول المملوك والمؤجر مع صلاحية كل نوع وموقع الدخول الحالي.
        if (!report && section.Register == PremisesRegister.Archive)
            window = new ArchiveWindow((int)kind);
        // يفتح كرت بيانات المقرات المملوكة الواجهة المخصصة التي أضافها المستخدم.
        else if (!report && kind == PremisesKind.Owned && section.Register == PremisesRegister.Locations)
            window = new owned_Building_Window();
        // يفتح زر عقود المباني المملوكة واجهة العقود المخصصة بعد اجتياز فحص الصلاحية أعلاه.
        else if (!report && kind == PremisesKind.Owned && section.Register == PremisesRegister.Contracts)
            window = new owned_Contracts();
        // يفتح زر فواتير كهرباء المباني المملوكة واجهة العدادات الجديدة بعد التحقق من صلاحية الكهرباء.
        else if (!report && kind == PremisesKind.Owned && section.Register == PremisesRegister.Power)
            window = new owned_Power_Window();
        // يفتح زر فواتير مياه المباني المملوكة واجهة العدادات الجديدة بعد التحقق من صلاحية المياه.
        else if (!report && kind == PremisesKind.Owned && section.Register == PremisesRegister.Water)
            window = new owned_Water_Window();
        // تفتح فواتير الهاتف بالشاشة المنقولة، مع بقاء فحص الصلاحية والملكية المشتركين.
        else if (!report && kind == PremisesKind.Owned && section.Register == PremisesRegister.PhoneBills)
            window = new owned_Phone_Window();
        // تعرض بطاقة الخدمات العقود بالشاشة المخصصة بعد اجتياز فحص صلاحية شركات الخدمات.
        else if (!report && kind == PremisesKind.Owned && section.Register == PremisesRegister.Services)
            window = new owned_Service_Window();
        // تفتح بطاقة التأمين للمملوك النافذة المخصصة بعد اجتياز فحص صلاحية عقود التأمين.
        else if (!report && kind == PremisesKind.Owned && section.Register == PremisesRegister.Insurance)
            window = new owned_Insurance_Window();
        // أرقام الهواتف مستقلة عن فواتير الهاتف وتستخدم صلاحية الحصر المخصصة لها.
        else if (!report && kind == PremisesKind.Owned && section.Register == PremisesRegister.PhoneNumbers)
            window = new owned_Inventory_Phones_Lines_Window();
        else
            window = new PremisesWindow(premisesData, kind, section, report);

        // نغلق لوحة الاختيار المعيارية قبل عرض النافذة؛ بقاؤها مفتوحة قد يحجب نافذة الأرشيف المالكة.
        PremisesFlyout.IsOpen = false;
        // تتبع النافذة الرئيسية في الملكية والعرض المشروط.
        window.Owner = this;
        window.ShowDialog();
    }

    private void ClosePremisesFlyout_Click(object sender, RoutedEventArgs e) => PremisesFlyout.IsOpen = false;

    private void PremisesPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !PremisesFlyout.IsOpen) return;
        PremisesFlyout.IsOpen = false;
        e.Handled = true;
    }

    private void ShowPremisesAbout() => UiMessages.Show(this, "حول المنظومة",
        $"منظومة حصر المقرات\nصندوق الضمان الاجتماعي\nالإصدار {DisplayVersion}",
        MessageBoxButton.OK, MessageBoxImage.Information);
    #endregion

    #region فتح شركات الخدمات وأنواع التعاقد من البيانات المساعدة
    private static bool CanManageServiceLookup(bool isType) =>
        PremisesSection.User is { } user && App.location is not null && (isType ? user.servtype : user.serv_benf);

    private void OpenServiceLookup(bool isType)
    {
        // نعيد فحص الصلاحية قبل الإنشاء؛ لا نحتاج اختيار مبنى لفتح قوائم الفرع والموقع الحاليين.
        var location = App.location;
        if (location is null || !CanManageServiceLookup(isType)) return;
        var scope = new ServiceScope(location.Branch_ID, location.Location_ID, location.Location);
        new ServiceLookupWindow(scope, isType) { Owner = this }.ShowDialog();
    }
    #endregion

    #region فتح شركات التأمين وأنواعه من البيانات المساعدة
    private static bool CanManageInsuranceLookup(bool isType) =>
        PremisesSection.User is { } user && App.location is not null && (isType ? user.sectype : user.sec_benef);

    private void OpenInsuranceLookup(bool isType)
    {
        // يفتح المسار نفسه من القائمة والوصول السريع، دون اشتراط تحديد مبنى.
        var location = App.location;
        if (location is null || !CanManageInsuranceLookup(isType)) return;
        var scope = new ServiceScope(location.Branch_ID, location.Location_ID, location.Location);
        new InsuranceLookupWindow(scope, isType) { Owner = this }.ShowDialog();
    }
    #endregion

    #region فتح الإدارات والأقسام والوحدات من البيانات المساعدة
    private static bool CanManageOrganizations() => PremisesSection.User?.mang_check == true && App.location != null;
    private void OpenOrganizations(bool sections)
    {
        if (!CanManageOrganizations() || App.location is not { } location) return;
        new OrganizationLookupWindow(new ServiceScope(location.Branch_ID, location.Location_ID, location.Location), sections)
            { Owner = this }.ShowDialog();
    }
    #endregion

    #region تحديث مؤشرات الحصر السنوية
    private async Task RefreshPremisesStatisticsAsync()
    {
        if (statisticsLoading || statisticsCancellation.IsCancellationRequested) return;
        var owned = PremisesSection.CanReport(PremisesKind.Owned);
        var rented = PremisesSection.CanReport(PremisesKind.Rented);
        OwnedCount.Text = RentedCount.Text = "—";
        if (!owned && !rented)
        {
            StatisticsStatus.Text = "إحصاءات الحصر متاحة للحسابات المخوّلة بعرض التقارير.";
            RefreshStatisticsButton.IsEnabled = false;
            return;
        }

        if (StatisticsYear.EditValue is not int year || year is < 1900 or > 9999)
        {
            StatisticsStatus.Text = "أدخل سنة صحيحة لتحديث الإحصاءات.";
            return;
        }

        statisticsLoading = true;
        StatisticsYear.IsEnabled = RefreshStatisticsButton.IsEnabled = false;
        StatisticsStatus.Text = "جارٍ تحديث الإحصاءات…";
        try
        {
            var token = statisticsCancellation.Token;
            var counts = await premisesData.CountAsync(year, owned, rented, token);
            token.ThrowIfCancellationRequested();
            OwnedCount.Text = counts.Owned?.ToString("N0") ?? "—";
            RentedCount.Text = counts.Rented?.ToString("N0") ?? "—";
            StatisticsStatus.Text = $"إحصاءات سنة {year} بحسب تاريخ تسجيل المقر.";
        }
        catch (OperationCanceledException) when (statisticsCancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            StatisticsStatus.Text = "تعذّر تحميل الإحصاءات. حاول التحديث مجددًا.";
        }
        finally
        {
            statisticsLoading = false;
            StatisticsYear.IsEnabled = RefreshStatisticsButton.IsEnabled = true;
        }
    }
    #endregion
}
