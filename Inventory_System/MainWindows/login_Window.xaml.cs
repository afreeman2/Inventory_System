
using MahApps.Metro.Controls;
using Inventory_System.converts;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Inventory_System.MainWindows
{
    /// <summary>
    /// Interaction logic for Login_Windows.xaml
    /// </summary>
    public partial class login_Window : MetroWindow
    {
        #region تهيئة نافذة الدخول وتحميل المستخدمين
        public login_Window()
        {
            InitializeComponent();
            Width = Math.Min(Width, SystemParameters.WorkArea.Width - 24);
            Height = Math.Min(Height, SystemParameters.WorkArea.Height - 24);
            MinHeight = Math.Min(MinHeight, Height);
        }
        // تحمّل حسابات الدخول النشطة واسم الفرع قبل عرض القائمة.
        private async Task LoadUsersAsync()
        {
            DbContextHelper.InitOptions(App.ConnectionString);
            await using var db = new Cont_maintContext(DbContextHelper.Options!);

            // يحتاج الدخول إلى بيانات الحساب المختار للتحقق من كلمة المرور والصلاحيات.
            var users = await db.T_SystemUsers
                .AsNoTracking()
                .Where(user => user.st == true)
                .ToListAsync();

            // نفّذ الاستعلامين بالتتابع على السياق نفسه واسترجع اسم الفرع فقط.
            var branchName = await db.T_Branches
                .Where(branch => branch.st == true)
                .Select(branch => branch.Branch)
                .FirstOrDefaultAsync();

            App.Branch_Name = branchName ?? string.Empty;
            cboUserFullName.ItemsSource = new ObservableCollection<T_SystemUser>(users);
            cboUserFullName.Focus();
        }
        private void LoginSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBlock || e.OriginalSource is Border || e.OriginalSource is System.Windows.Controls.Image)
                if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        #endregion
        #region تأكيد إغلاق شاشة الدخول
        private void out_Click(object sender, RoutedEventArgs e)
        {
            // يستخدم التأكيد أسماء الأزرار العربية ويجعل متابعة العمل الخيار الافتراضي.
            if (Inventory_System.Dashboard.UiMessages.Confirm(this, "هل تريد إغلاق منظومة الخدمات والصيانة؟", closing: true))
                Application.Current.Shutdown();
        }
        #endregion
        #region التحقق من بيانات الدخول وفتح الرئيسية
        T_SystemUser this_us = null;
        private void BNEXT_Click(object sender, RoutedEventArgs e)
        {


            FieldFeedback.Clear(cboUserFullName); FieldFeedback.Clear(txbPassword);

            // تحقق المطلوب عند تنفيذ الأمر، ثم ركّز أول خانة ناقصة قبل فك بيانات الحساب.
            if (cboUserFullName.SelectedItem is not T_SystemUser)
            {
                FieldFeedback.Reject(cboUserFullName, "اختر المستخدم.");
                return;
            }
            if (string.IsNullOrEmpty(txbPassword.Password))
            {
                FieldFeedback.Reject(txbPassword, "أدخل كلمة المرور.");
                return;
            }
            try
            {

                this_us = (T_SystemUser)cboUserFullName.SelectedItem;



                if (App.decrypt(this_us?.PassWord) != txbPassword.Password)
                {
                    FieldFeedback.Reject(txbPassword, "كلمة المرور غير صحيحة أعد المحاولة.");
                    return;
                }

            }
            catch (Exception ex)
            {

                Inventory_System.Dashboard.UiMessages.Show(this, "تعذّر تسجيل الدخول", $"خطأ {ex.Message}", MessageBoxButton.OK, MessageBoxImage.Error);


                return;
            }


            App.usname = cboUserFullName.Text;
            App.login_user = this_us;
            App.appuser = this_us;

            //***********************************
            MainWindow mi = new MainWindow();

            this.Hide();
            this.ShowInTaskbar = false;
            mi.ShowDialog();

        }

        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // امنح الواجهة فرصة للظهور قبل بدء تحميل الحسابات.
                await Task.Yield();
                await LoadUsersAsync();
            }
            catch (SqlException ex) when (ex.Number == 4060)
            {
                // يعني الخطأ 4060 أن الخادم تعذّر عليه فتح القاعدة المطلوبة للحساب الحالي.
                Inventory_System.Dashboard.UiMessages.Show(this, "تعذّر تحميل المستخدمين",
                    "تعذّر فتح قاعدة البيانات المحددة في إعدادات الاتصال بحساب المنظومة. تحقق من اسم القاعدة وصلاحية الوصول إليها (SQL Server 4060).",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Inventory_System.Dashboard.UiMessages.Show(this, "تعذّر تحميل المستخدمين",
                    ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // لا يستبدل Tag حتى يبقى ربط التحقق متصلًا بعد اختيار المستخدم أو تغييره.
        private void cboUserFullName_SelectionChanged(object sender, RoutedEventArgs e)
        {
            FieldFeedback.Clear(cboUserFullName);
            FieldFeedback.Clear(txbPassword);
        }
        private void cboUserFullName_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //if (e.Key == Key.Enter)
            //    txbPassword.Focus();
        }

        #endregion
        #region تنبيه لوحة المفاتيح وإظهار كلمة المرور

        // يعيد إخفاء الكلمة عند مغادرة الخانة وزر العين معًا.
        private void PasswordArea_FocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is false && PasswordVisibilityButton is not null)
                PasswordVisibilityButton.IsChecked = false;
        }

        // يستخدم محرر DevExpress نفسه كي تبقى القيمة والربط والتحقق في الخانة الأصلية.
        private void PasswordVisibility_Changed(object sender, RoutedEventArgs e)
        {
            if (txbPassword is null) return;

            if (((ToggleButton)sender).IsChecked == true)
                txbPassword.ShowPassword();
            else
                txbPassword.HidePassword();
        }

        // يقرأ حالة المفتاح عند دخول الخانة ويخفي التنبيه عند مغادرتها.
        private void Password_FocusChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdatePasswordCapsLockWarning();

        private void UpdatePasswordCapsLockWarning()
        {
            if (PasswordCapsLockWarning is null) return;

            PasswordCapsLockWarning.Visibility = txbPassword.IsKeyboardFocusWithin && Keyboard.IsKeyToggled(Key.CapsLock)
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        private void txbPassword_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //حدّث التنبيه بعد تبديل المفتاح مع إبقاء اختصار Enter للدخول.
            if (e.Key == Key.CapsLock)
                UpdatePasswordCapsLockWarning();

            if (e.Key == Key.Enter)
            {
                if (BNEXT.IsEnabled == true)
                    BNEXT_Click(BNEXT, new RoutedEventArgs());
                else
                    cboUserFullName.Focus();
            }
        }

        #endregion

        #region تحديث تحقق كلمة المرور بعد تغيير القيمة
        // يصل الحدث بعد اعتماد القيمة، فيمسح الخطأ فور الكتابة أو الحذف أو اللصق.
        private void txbPassword_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        {
            FieldFeedback.Clear(txbPassword);
            txbPassword.SetValue(FieldFeedback.HasValueProperty, !string.IsNullOrEmpty(txbPassword.Password));
            if (string.IsNullOrEmpty(txbPassword.Password) && PasswordVisibilityButton is not null)
                PasswordVisibilityButton.IsChecked = false;
        }
        #endregion
    }
}

