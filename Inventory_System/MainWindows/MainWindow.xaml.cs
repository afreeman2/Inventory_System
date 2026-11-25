using RadioButton = System.Windows.Controls.RadioButton;
#nullable disable
using MenuItem = System.Windows.Controls.MenuItem;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Brushes = System.Windows.Media.Brushes;

using Inventory_System.Models;
using Inventory_System.DatabContext;
using Inventory_System.converts;
using Inventory_System.viewo_Model;
using Inventory_System.MainWindows;
using Microsoft.EntityFrameworkCore;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using System.ComponentModel;
using Microsoft.Win32;
using System.Globalization;
using Inventory_System.Dashboard;
using Inventory_System.Premises;
using System.Threading;
using DevExpress.Xpf.Core;

namespace Inventory_System.MainWindows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>



    /// <summary>
    /// Interaction logic for Main.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public const string DisplayVersion = "3.0.0.0";
        public static readonly RoutedUICommand AboutSystemCommand = new RoutedUICommand("حول النظام", nameof(AboutSystemCommand),
            typeof(MainWindow), new InputGestureCollection { new KeyGesture(Key.F1) });
        public static readonly RoutedUICommand OpenMaintenanceOrderCommand = new RoutedUICommand(
            "تكليف الصيانة", nameof(OpenMaintenanceOrderCommand), typeof(MainWindow),
            new InputGestureCollection { new KeyGesture(Key.F7) });

        #region أمر إدارة المستخدمين
        // يُعرّف الأمر على نوع النافذة الفعلي لتتمكن الواجهة من الرجوع إليه عبر x:Static.
        public static readonly RoutedUICommand OpenUsersCommand =
            new("المستخدمون والصلاحيات", nameof(OpenUsersCommand), typeof(MainWindow));
        #endregion

       


      
        #region حالة القائمة والإحصاءات
        // تُلغى إحصاءات الحصر عند إغلاق الرئيسية؛ يحتفظ التبويب المختار بمعرّف مجموعته.
        private readonly CancellationTokenSource statisticsCancellation = new();
        private bool statisticsLoading;
        private string workspaceSection = "operations";
        #endregion

        private bool confirmedClose;

        /// الخاصة بالنسخ الأحتياطي والأسترجاع Try متغيرات خاصه بحفظ رسالة الخطأمن جملة
        private bool cath = false;
        private string ms;
        // ************************
        private byte clo = 0;

        // *******************
        // متغيير لتفعيل الاختصارات بالشاشة
        public static RoutedCommand SaveCommand_AltS = new RoutedCommand();
        // *********************
        // مؤقت لعرض الوقت في الشاشة الرئيسية
        DispatcherTimer timer = new DispatcherTimer();
        // *************************
        // متغيير لمعرفة مسار مجلد المنظومة
        private string AppPath = System.IO.Directory.GetCurrentDirectory();
        //var color = ThemeManager.Accents;


        #region تهيئة الرئيسية ومصدر بيانات الإشعارات
        public MainWindow() : this(null) { }

        // مصدر مستقل اختياري لفحص الربط اللحظي والنوافذ على قاعدة معزولة.
        public MainWindow(Func<Inventory_System.dbContext.Cont_maintContext> contextFactory, Action playNotificationSound = null)
        {
           
            // يستخدم الحصر اتصال EF Core المهيّأ عند الدخول، ويمكن تمرير سياق معزول للتحقق.
            premisesData = new PremisesData(contextFactory ?? PremisesData.CreateContext);
            InitializeComponent();
            StatisticsYear.EditValue = DateTime.Today.Year;
           
            CommandBindings.Add(new CommandBinding(AboutSystemCommand, AboutSystem_Click));
            // تسجيل الاختصار على الرئيسية صراحة يضمن عمله مهما كان عنصر التركيز الحالي.
            InputBindings.Add(new KeyBinding(AboutSystemCommand, Key.F1, ModifierKeys.None));

            //InitializeAccountCommands();
            CommandBindings.Add(new CommandBinding(OpenMaintenanceOrderCommand, bt_inout_par_Click,
                (_, args) => { args.CanExecute = inout_Menu.IsEnabled; args.Handled = true; }));
            WindowWorkArea.Attach(this);
            InitializeWorkspace();
            //InitializeWorkspaceShortcuts();
            timer_Tick(this, EventArgs.Empty);
        }

        #endregion

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        private void CloseWindow_Click(object sender, RoutedEventArgs e) { Close(); }
        #region تعريف المنظومة وتحريك النافذة
        // يستعمل الاختصار والزر التعريف نفسه بصندوق رسائل المنظومة.
        private void AboutSystem_Click(object sender, RoutedEventArgs e) => ShowPremisesAbout();

        private void TitleSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) MaximizeWindow_Click(sender, e);
            else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
        #endregion

        private void CloseQuickAccess_Click(object sender, RoutedEventArgs e)
        {
            QuickAccessPanel.Visibility = Visibility.Collapsed;
            ShowQuickAccessButton.Visibility = Visibility.Visible;
            ShowQuickAccessButton.Focus();
        }

        private void ShowQuickAccess_Click(object sender, RoutedEventArgs e)
        {
            QuickAccessPanel.Visibility = Visibility.Visible;
            ShowQuickAccessButton.Visibility = Visibility.Collapsed;
        }

        #region عرض إحصاءات الحصر وإغلاقها
        private void CloseStatistics_Click(object sender, RoutedEventArgs e)
        {
            StatisticsCard.Visibility = Visibility.Collapsed;
            ShowStatisticsButton.Visibility = Visibility.Visible;
            ShowStatisticsButton.Focus();
        }

        private async void ShowStatistics_Click(object sender, RoutedEventArgs e)
        {
            StatisticsCard.Visibility = Visibility.Visible;
            ShowStatisticsButton.Visibility = Visibility.Collapsed;
            StatisticsYear.Focus();
            await RefreshPremisesStatisticsAsync();
        }

        private async void RefreshStatistics_Click(object sender, RoutedEventArgs e)
        {
            await RefreshPremisesStatisticsAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            statisticsCancellation.Cancel();
            timer.Stop();
            statisticsCancellation.Dispose();
            base.OnClosed(e);
        }

        #endregion

   
        private void timer_Tick(object sender, EventArgs e)
        {
            ltim.Text = DateTime.Now.ToString("hh:mm:ss");
            LDATE.Text = DateTime.Now.ToString("dd-MM-yyyy");

        }

        private async void close_Click(object sender, RoutedEventArgs e)
        {
           
            Close();



        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (confirmedClose) return;

            if (!UiMessages.Confirm(this, "هل تريد إنهاء العمل وإغلاق منظومة حصر المقرات؟", true))
            {
                e.Cancel = true;
                return;
            }

            confirmedClose = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(() => Application.Current.Shutdown()));
        }




        private void credite_op_SubmenuClosed(object sender, RoutedEventArgs e)
        {
            credite_op.Foreground = Brushes.White;

        }

        private void credite_op_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            credite_op.Foreground = Brushes.Black;

        }
        private void xcredite_op_SubmenuClosed(object sender, RoutedEventArgs e)
        {
            Supports_Data.Foreground = Brushes.White;

        }

        private void xcredite_op_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            Supports_Data.Foreground = Brushes.Black;

        }


        private void REPORT_MNUE_SubmenuClosed(object sender, RoutedEventArgs e)
        {
            REPORT_MNUE.Foreground = Brushes.White;

        }

        private void REPORT_MNUE_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            REPORT_MNUE.Foreground = Brushes.Black;

        }

        private void mng_SubmenuClosed(object sender, RoutedEventArgs e)
        {
            //mng.Foreground = Brushes.White;

        }

        private void mng_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            //mng.Foreground = Brushes.Black;

        }



        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeSessionAsync();
        }

        private async Task InitializeSessionAsync()
        {



            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("ar-LY");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("ar-LY");
            //'*********************
            //'عرض إصدار المنظومة
            vesion.Text = "الإصدار " + DisplayVersion;

     
           
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += timer_Tick;
            timer.Start();
            //'*****************
            //'عرض التوقيت في الشاشة

            //timer= new DispatcherTimer();

            //'***************
            //'عرض إسم المستخدم
            lus.Text = App.usname;


            //الصلاحيات
            //============================================

            //متابعة الصكوك
            //inout_Menu.IsEnabled = (App.appuser?.forinout == true);

            //========================================

            //متابعة الصكوك
            //work_his_Menu.IsEnabled = (App.appuser?.for_work_his == true);

            //========================================


            //البيانات المساعدة


            // فروع البنوك
            //Bank_mnue.IsEnabled = (App.appuser?.forbanks == true);
            //Branch_Bank_mnue.IsEnabled = (App.appuser?.forbranch == true);

            //Branch_Bank_mnue.IsEnabled = (App.appuser?.forbranch_bank == true);
            //rank_mnue.IsEnabled = (App.appuser?.forranks == true);
            //rce_mnue.IsEnabled = (App.appuser?.forres == true);
            //ref_mnue.IsEnabled = (App.appuser?.for_ref == true);
            //deb_mnue.IsEnabled = (App.appuser?.for_deb == true);
            //nat_mnue.IsEnabled = (App.appuser?.fornat == true);
            //dis_place_mnue.IsEnabled = (App.appuser?.fordebplace == true);

            //========================================

            //========================================


            //التقارير
            // نقارير الصكوك
            //report_mnue.IsEnabled = (App.appuser?.for_report == true);
            //viewo_mnue.IsEnabled = (App.appuser?.for_virwo == true);
            RefreshWorkspace();
            // تحمّل الرئيسية مؤشرات السنة المختارة حسب صلاحية التقارير لكل نوع من المقرات.
            await RefreshPremisesStatisticsAsync();



            //========================================.
        }




        private void instrument_Menu_Click(object sender, RoutedEventArgs e)
        {

            //f.Top = 140;
            //f.Left = 15;


        }

        private void TMNADD_CREDITE_Click(object sender, RoutedEventArgs e)
        {
            //inout f = new inout();
            //f.ShowDialog();
            //f.Top = 160;
            //f.Left = 80;

        }

        private void Ins_Reports_Click(object sender, RoutedEventArgs e)
        {


        }

        private void sw_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
            //System.Windows.Forms.Application.Restart();
        }

        private void Owned_Building_report_Click(object sender, RoutedEventArgs e)
        {
            //Rports f = new Rports();
            //f.ShowDialog();
        }

        private void Bank_mnue_Click(object sender, RoutedEventArgs e)
        {
            //Bank f = new Bank();
            //f.ShowDialog();
        }

        private void Branch_Bank_mnue_Click(object sender, RoutedEventArgs e)
        {
            //App.is_button = false;
            //Bank_Branch f = new Bank_Branch();
            //f.ShowDialog();
        }

        private void sk_types_Click(object sender, RoutedEventArgs e)
        {

        }

        private void out_type_Click(object sender, RoutedEventArgs e)
        {

        }

        private void minimiz_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void bt_inout_par_Click(object sender, RoutedEventArgs e)
        {
            //    if (!inout_Menu.IsEnabled) return;
            //    if (maintenanceOrderWindow != null && maintenanceOrderWindow.IsVisible)
            //    {
            //        maintenanceOrderWindow.Activate();
            //        return;
            //    }
            //    try
            //    {
            //        maintenanceOrderWindow = new orders_Windows.maintenance_Order
            //        {
            //            Owner = this,
            //            WindowState = WindowState.Maximized
            //        };
            //        maintenanceOrderWindow.ShowDialog();
            //    }
            //    catch (Exception ex)
            //    {
            //        try
            //        {
            //            string logDirectory = System.IO.Path.Combine(
            //                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Inventory_System", "Logs");
            //            System.IO.Directory.CreateDirectory(logDirectory);
            //            System.IO.File.AppendAllText(System.IO.Path.Combine(logDirectory, "order-window.log"),
            //                DateTimeOffset.Now.ToString("O") + Environment.NewLine + ex + Environment.NewLine);
            //        }
            //        catch (System.IO.IOException) { }
            //        catch (UnauthorizedAccessException) { }
            //        Inventory_System.Dashboard.UiMessages.Show(this, "تكليف الصيانة", "تعذّر فتح واجهة تكليف الصيانة.\n" + ex.GetBaseException().Message, MessageBoxButton.OK, MessageBoxImage.Error);
            //    }
            //    finally
            //    {
            //        maintenanceOrderWindow = null;
            //        // إعادة القراءة بعد إغلاق التكليف تُظهر الإضافات والحذف المحلي دون انتظار المؤقت.
            //        _ = RefreshMaintenanceNotificationsAsync();
            //    }
        }

        private void work_his_Menu_Click(object sender, RoutedEventArgs e)
        {
            work_his_par_Click(sender, e);
        }

        private void work_his_par_Click(object sender, RoutedEventArgs e)
        {
            //inclusion_statement_Window c = new inclusion_statement_Window();
            //c.ShowDialog();
        }

        private void rank_mnue_Click(object sender, RoutedEventArgs e)
        {
            //rank_windows r = new rank_windows();
            //r.ShowDialog();
        }

        private void ref_mnue_Click(object sender, RoutedEventArgs e)
        {
            //ref_Window r = new ref_Window();
            //r.ShowDialog();
        }

        private void rce_mnue_Click(object sender, RoutedEventArgs e)
        {
            //rce_window r = new rce_window();
            //r.ShowDialog();
        }

        private void nat_mnue_Click(object sender, RoutedEventArgs e)
        {
            //nat_windows c = new nat_windows();
            //c.ShowDialog();
        }

        private void deb_mnue_Click(object sender, RoutedEventArgs e)
        {
            //deb_windows f = new deb_windows();
            //f.ShowDialog();
        }

        #region فتح نوافذ التقارير والإحصائية
        private void DailyInspection_Click(object sender, RoutedEventArgs e)
        {
            //if (!DailyInspectionMenu.IsEnabled) return;
            //if (dailyInspectionWindow is { IsLoaded: true })
            //{
            //    if (dailyInspectionWindow.WindowState == WindowState.Minimized)
            //        dailyInspectionWindow.WindowState = WindowState.Maximized;
            //    dailyInspectionWindow.Activate();
            //    dailyInspectionWindow.Focus();
            //    return;
            //}
            //dailyInspectionWindow = new Daly_Print_Windowxaml(notificationContextFactory) { Owner = this };
            //dailyInspectionWindow.Closed += (_, _) => dailyInspectionWindow = null;
            //// يطابق فتح النموذج حواريًا سلوك المنظومة القديمة ويبقي الرئيسية معطّلة حتى إغلاقه.
            //dailyInspectionWindow.ShowDialog();
        }

        private void report_mnue_Click(object sender, RoutedEventArgs e)
        {
            //if (!report_mnue.IsEnabled) return;
            //// نافذة مستقلة للتقارير وتُجلب للمقدمة إن كانت مفتوحة بالفعل.
            //if (maintenanceReportsWindow is { IsLoaded: true })
            //{
            //    BringReportWindowToFront(maintenanceReportsWindow);
            //    return;
            //}

            //maintenanceReportsWindow = new MaintenanceReportsWindow(notificationContextFactory) { Owner = this };
            //maintenanceReportsWindow.Closed += (_, _) => maintenanceReportsWindow = null;
            //maintenanceReportsWindow.Show();
            //BringReportWindowToFront(maintenanceReportsWindow);
        }

        // صلاحية تقارير الفروع مستقلة، وتُفحص أيضًا عند تنفيذ الأمر من بطاقة الوصول السريع.
        private void BranchReportsMenu_Click(object sender, RoutedEventArgs e)
        {
            //if (!BranchReportsMenu.IsEnabled) return;
            //if (branchReportsWindow is { IsLoaded: true })
            //{
            //    BringReportWindowToFront(branchReportsWindow);
            //    return;
            //}

            //branchReportsWindow = new MaintenanceReportsWindow(dbContext_Aggregated.DbContextHelper.CreateContext, true) { Owner = this };
            //branchReportsWindow.Closed += (_, _) => branchReportsWindow = null;
            //branchReportsWindow.Show();
            //BringReportWindowToFront(branchReportsWindow);
        }

        private void statistics_mnue_Click(object sender, RoutedEventArgs e)
        {
            //if (!statistics_mnue.IsEnabled) return;
            //// نافذة مستقلة للإحصائية وتُجلب للمقدمة إن كانت مفتوحة بالفعل.
            //if (maintenanceStatisticsWindow is { IsLoaded: true })
            //{
            //    BringReportWindowToFront(maintenanceStatisticsWindow);
            //    return;
            //}

            //maintenanceStatisticsWindow = new MaintenanceStatisticsWindow(notificationContextFactory) { Owner = this };
            //maintenanceStatisticsWindow.Closed += (_, _) => maintenanceStatisticsWindow = null;
            //maintenanceStatisticsWindow.Show();
            //BringReportWindowToFront(maintenanceStatisticsWindow);
        }

        private static void BringReportWindowToFront(Window reportWindow)
        {
            //// يُعاد تنشيط النافذة بعد إظهارها حتى لا تفتح خلف الرئيسية أو خلف نافذة أخرى.
            //if (reportWindow is Inventory_System.reports.MaintenanceReportsWindow)
            //{
            //    reportWindow.WindowState = WindowState.Maximized;
            //}
            //else
            //{
            //    // باقي النوافذ تفتح بالحجم المحدد في XAML
            //    reportWindow.WindowState = WindowState.Normal;
            //}
            //reportWindow.Activate();
            //reportWindow.Focus();
        }
        #endregion

        #region فتح واجهة البيانات المساعدة للصيانة
        // لكل بند نافذة مستقلة بخانات إدخال خاصة به؛ لا تُجمع المصادر في تبويبات نافذة واحدة.
        private void SupportBuildingsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (SupportBuildingsMenu.IsEnabled) OpenMaintenanceSupportDatabuild();
        }

        private void OpenMaintenanceSupportDatabuild()
        {
            //buildings f = new buildings();
            //f.ShowDialog();
        }

        private void SupportServicesMenu_Click(object sender, RoutedEventArgs e)
        {
            if (SupportServicesMenu.IsEnabled) OpenMaintenanceSupportDatax();
        }
        private void SupportSectionsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (SupportSectionsMenu.IsEnabled) OpenMaintenanceSupportDatasection();
        }

        private void OpenMaintenanceSupportDatasection()
        {
            //sections f = new sections();
            //f.ShowDialog();
        }

        private void SupportEmployeesMenu_Click(object sender, RoutedEventArgs e)
        {
            if (SupportEmployeesMenu.IsEnabled) OpenMaintenanceSupportDataemployey();
        }

        private void OpenMaintenanceSupportDataemployey()
        {
            //employeys f = new employeys();
            //f.ShowDialog();
        }

        private void SupportManagementsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (SupportManagementsMenu.IsEnabled) OpenMaintenanceSupportDatamang();
        }

        private void OpenMaintenanceSupportDatamang()
        {
            //mangments f = new mangments();
            //f.ShowDialog();
        }

        private void OpenMaintenanceSupportDatax()
        {
            //servic_types f = new servic_types();
            //f.ShowDialog();
        }
        private void OpenMaintenanceSupportData(int tab)
        {
            //if (maintenanceSupportWindows.TryGetValue(tab, out var openedWindow) && openedWindow.IsLoaded)
            //{
            //    if (openedWindow.WindowState == WindowState.Minimized) openedWindow.WindowState = WindowState.Normal;
            //    openedWindow.Activate();
            //    return;
            //}

            //var supportWindow = new MaintenanceSupportWindow(notificationContextFactory, tab) { Owner = this };
            //maintenanceSupportWindows[tab] = supportWindow;
            //supportWindow.Closed += (_, _) => maintenanceSupportWindows.Remove(tab);
            //supportWindow.Show();
            //supportWindow.Activate();
        }
        #endregion

        private void viewo_mnue_Click(object sender, RoutedEventArgs e)
        {

        }

        private void inout_Menu_Click(object sender, RoutedEventArgs e)
        {
            bt_inout_par_Click(sender, e);
        }

        private void man_mnue_Click(object sender, RoutedEventArgs e)
        {

        }

        private void dis_place_mnue_Click(object sender, RoutedEventArgs e)
        {
            //dis_place_window f = new dis_place_window();
            //f.ShowDialog();
        }

        private void INFOOK(object sender, RoutedEventArgs e)
        {

        }

        private void CloseSec_OnClick(object sender, RoutedEventArgs e)
        {


        }

        private void OkButtonOnClick(object sender, RoutedEventArgs e)
        {
            confirmedClose = true;
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Application.Current.Shutdown();
        }

        private void DailyInspectionSERVICEMenu_Click(object sender, RoutedEventArgs e)
        {
            //if (!DailyInspectionSERVICEMenu.IsEnabled) return;

            //if (dailyInspectionserviceWindow is { IsLoaded: true })
            //{
            //    if (dailyInspectionserviceWindow.WindowState == WindowState.Minimized)
            //        dailyInspectionserviceWindow.WindowState = WindowState.Maximized;
            //    dailyInspectionserviceWindow.Activate();
            //    dailyInspectionserviceWindow.Focus();
            //    return;
            //}
            //dailyInspectionserviceWindow = new daliy_services_window(notificationContextFactory) { Owner = this };
            //dailyInspectionserviceWindow.Closed += (_, _) => dailyInspectionserviceWindow = null;
            //// يطابق فتح النموذج حواريًا سلوك المنظومة القديمة ويبقي الرئيسية معطّلة حتى إغلاقه.
            //dailyInspectionserviceWindow.ShowDialog();
        }

        private void CompletionMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void NotificationBell_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
