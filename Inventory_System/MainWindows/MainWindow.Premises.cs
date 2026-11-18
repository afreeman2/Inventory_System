using Inventory_System.Dashboard;
using Inventory_System.Premises;
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
        // تستخدم القوائم والوصول السريع والاختصارات الإجراءات نفسها وصلاحيات الحساب المسجّل.
        workspaceActions =
        [
            new("operations", "حصر المقرات المملوكة", "فتح أقسام حصر المقرات المملوكة.", "Ctrl+D",
                PackIconMaterialKind.OfficeBuildingOutline, () => CanOpenPremises(PremisesKind.Owned), () => OpenPremises(PremisesKind.Owned)),
            new("operations", "حصر المقرات المؤجرة", "فتح أقسام حصر المقرات المؤجرة.", "Ctrl+C",
                PackIconMaterialKind.KeyOutline, () => CanOpenPremises(PremisesKind.Rented), () => OpenPremises(PremisesKind.Rented)),
            new("reference", "بيانات المقرات المملوكة", "إضافة وتعديل أسماء المقرات المملوكة وعناوينها.", "Ctrl+F",
                PackIconMaterialKind.OfficeBuildingOutline, () => locations.CanOpen(PremisesKind.Owned),
                () => OpenRegister(PremisesKind.Owned, locations)),
            new("reference", "بيانات المقرات المؤجرة", "إضافة وتعديل أسماء المقرات المؤجرة وعناوينها.", "Ctrl+G",
                PackIconMaterialKind.KeyOutline, () => locations.CanOpen(PremisesKind.Rented),
                () => OpenRegister(PremisesKind.Rented, locations)),
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
        RefreshWorkspace();
    }

    private void RegisterPremisesShortcut(WorkspaceAction action, Key key)
    {
        var command = new RoutedCommand();
        CommandBindings.Add(new CommandBinding(command, (_, _) => ExecuteWorkspaceAction(action),
            (_, args) => { args.CanExecute = !PremisesFlyout.IsOpen && action.IsEnabled; args.Handled = true; }));
        InputBindings.Add(new KeyBinding(command, key, ModifierKeys.Control));
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

    private static bool CanOpenPremises(PremisesKind kind) =>
        PremisesSection.All.Any(section => section.Register != PremisesRegister.Archive && section.CanOpen(kind));

    private void OpenPremises(PremisesKind kind)
    {
        if (!CanOpenPremises(kind)) return;
        selectedKind = kind;
        PremisesFlyoutTitle.Text = "حصر " + PremisesSection.KindName(kind);
        PremisesChoices.ItemsSource = PremisesSection.All.Select(section => new PremisesCard(section,
            section.Register != PremisesRegister.Archive && section.CanOpen(kind),
            section.Register == PremisesRegister.Archive ? "أرشيف المستندات غير متاح في قاعدة بيانات المنظومة الحالية." : section.Title)).ToArray();
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
        if (report ? !PremisesSection.CanReport(kind) : !section.CanOpen(kind)) return;
        var window = new PremisesWindow(premisesData, kind, section, report) { Owner = this };
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
