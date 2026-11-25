using DevExpress.Data.Async.Helpers;
using DevExpress.Mvvm.Xpf;
using DevExpress.Office.Drawing;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using Inventory_System.converts;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using Inventory_System.support_Data;
using Inventory_System.viewo_Model;
using MahApps.Metro.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using static DevExpress.Utils.Zip.Internal.SecureZipTrace;
using Control = System.Windows.Controls.Control;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Inventory_System.Inventory_Windows.owned_Windows
{

    public partial class owned_Contracts : MetroWindow
    {
        int optype = 0;
        #region تحميل قوائم المقرات والمستفيدين

        private async Task LoadUsersAsync()
        {
            DbContextHelper.InitOptions(App.ConnectionString);
            await using var db = new Cont_maintContext(DbContextHelper.Options!);
            if (App._listbuilding == null)
            {
                // تبقى آلية تحميل قوائم اختيارات الفلاتر كما هي في كود الواجهة.
                var in_loctions = await db.T_Inventory_Locations
                    .AsNoTracking().Include(k=> k.T_OwnedPremises)
                    .OrderBy(k => k.In_Location)
                    .ToListAsync();
                App._listbuilding = new ObservableCollection<T_Inventory_Location>(in_loctions);
            }

            Combo_Locaton.ItemsSource = App._listbuilding;
            if (App._listbenef == null)
                App._listbenef = new ObservableCollection<T_Benef>(await db.T_Benefs.OrderBy(k => k.Benef).ToListAsync());

            Combo_benef.ItemsSource = App._listbenef;
            Combo_benef1.ItemsSource = App._listbenef; ;

        }


        #endregion

        #region أحداث تحميل النافذة

        private async void MetroWindow_LoadedAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                // تظهر الواجهة أولًا ثم تُحمّل قوائم المقرات والمستفيدين.
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

        #endregion
        #region أحداث أدوات الواجهة والتنقل

        #region اختيار المقر
        private void Combo_Locaton_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Combo_benef.Focus();
                e.Handled = true;
            }
        }

        private void Combo_Locaton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // يتغير عرض العقود تلقائيًا مع المبنى، دون إظهار أخطاء مطلوب أثناء الاختيار.
            ApplySelectedFilters(false);
        }
        #endregion

        #region تفعيل فلتر التاريخ
        private void DateFilter_Click(object sender, RoutedEventArgs e)
        {
            // لا تبقى مدة مخفية مستخدمة بعد تغيير تفعيل تصفية التاريخ.
            date_br_begin.SelectedDate = date_br_end.SelectedDate = null;
            date_br_begin.Text = date_br_end.Text = string.Empty;
            FieldFeedback.Clear(date_br_begin);
            FieldFeedback.Clear(date_br_end);
            if (Date_Check.IsChecked == true) date_br_begin.Focus();
        }
        private void Date_Check_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Date_Check.IsChecked == true)
            {
                date_br_begin.Focus();
            }
            else
            {
                by_metar.Focus();
            }
        }

        #endregion

        #region تاريخ بداية البحث

        // يعالج Enter قبل المحرر الداخلي، وينقل المؤشر مباشرة دون الوقوف على زر التقويم.
        private void ContractDate_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is not DatePicker date) return;
            UIElement? next = date == date_Engry ? Combo_benef1
                : date == date_br_begin ? date_br_end
                : date == date_br_end ? by_metar : null;
            if (next == null) return;
            e.Handled = true;
            date.IsDropDownOpen = false;
            // التركيز على نص التاريخ التالي يتيح الكتابة مباشرة ويثبت تاريخ الخانة السابقة عند مغادرتها.
            if (next is DatePicker nextDate)
            {
                nextDate.ApplyTemplate();
                (nextDate.Template.FindName("PART_TextBox", nextDate) as UIElement ?? nextDate).Focus();
            }
            else next.Focus();
        }

        private void date_br_begin_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // نمسح خطأ الخانة دون استبدال ربط التحقق الموجود على Tag.
            FieldFeedback.Clear(date_br_begin);
        }

        private void date_br_begin_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // نمسح خطأ الخانة دون استبدال ربط التحقق الموجود على Tag.
            FieldFeedback.Clear(date_br_begin);

        }

        private void date_br_begin_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                date_br_end.Focus();
                e.Handled = true;
            }
        }

        #endregion

        #region تاريخ نهاية البحث
        private void date_br_end_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                by_metar.Focus();
            }   
        }

        private void date_br_end_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // يبقى ربط التحقق سليمًا لإعادة فحص مدى التاريخ عند الضغط على عرض.
            FieldFeedback.Clear(date_br_end);    
        }

        private void date_br_end_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // يبقى ربط التحقق سليمًا لإعادة فحص مدى التاريخ عند الضغط على عرض.
            FieldFeedback.Clear(date_br_end);

        }


        #endregion


        #region تفعيل فلتر المستفيد
        private void by_metar_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (by_metar.IsChecked == true)
                {
                    Combo_benef.Focus();
                }

            }
        }
        #endregion


        #endregion

            #region تهيئة تصميم العقود وأوامر النافذة
            // تُنشأ الواجهة بربط ViewModel المعرف في XAML، وتبقى أحداث الأدوات والتنقل في كود النافذة.
        public owned_Contracts()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Contracts_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || !Engry_Windows.IsOpen) return;
            Engry_Windows.IsOpen = false;
            e.Handled = true;
        }
        #endregion

        #region فتح مسودة العقد وإلغاؤها دون حفظ
        private void NewContract_Click(object sender, RoutedEventArgs e)
        {

            if (Combo_Locaton.SelectedIndex < 0)
            {
                Combo_Locaton.Tag = 1;
                ErrorRule.ErrorMessage = "اختار اسم المبني";
                Combo_Locaton.GetBindingExpression(TagProperty).UpdateSource();
                Combo_Locaton.Focus();
                return;
            }
            optype = 1;
            Text_Elec_meter.Focus();
            Engry_Windows.IsOpen = true;
        }

        private void CancelContract_Click(object sender, RoutedEventArgs e) => Engry_Windows.IsOpen = false;

        private void ContractFlyout_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            // الإلغاء أو Escape أو الإغلاق الخارجي يزيل المسودة وأخطاءها دون تعديل الصفوف المعروضة.
            if (!Engry_Windows.IsOpen) ClearContractDraft();
        }

        private void ClearContractDraft()
        {
            Text_Elec_meter.Clear();
            date_Engry.SelectedDate = null;
            date_Engry.Text = string.Empty;
            Combo_benef1.SelectedIndex = -1;
            foreach (Control field in new Control[] { Text_Elec_meter, date_Engry, Combo_benef1 })
                FieldFeedback.Clear(field);
        }
        #endregion

        #region تفعيل فلاتر العرض وتفريغ اختياراتها

        private void Search_Button_Click(object sender, RoutedEventArgs e)
        {
            ApplySelectedFilters(true);
        }

        private void ApplySelectedFilters(bool showValidation)
        {
            // تشترك قائمة المباني وزر عرض في جمع الفلاتر، ويظل الاستعلام في ViewModel الحالي.
            if (DataContext is not Inventory_System.viewo_Model.owned_contrect_viewomodel model) return;
            if (Benef_List == null || Combo_benef == null || date_br_begin == null || date_br_end == null) return;
            foreach (Control field in new Control[] { Combo_Locaton, Combo_benef, date_br_begin, date_br_end })
                FieldFeedback.Clear(field);

            // التغيير التلقائي لا يعرض أخطاء قبل تنفيذ الأمر؛ إذا كانت الفلاتر ناقصة تبقى النتائج فارغة.
            void RejectFilter(Control field, string message)
            {
                if (showValidation) FieldFeedback.Reject(field, message);
                else model.ApplyFilters(null, null, null, null);
            }

            if (Combo_Locaton.SelectedItem is not T_Inventory_Location premise)
            {
                RejectFilter(Combo_Locaton, "اختر المقر لعرض عقوده.");
                return;
            }

            DateTime? from = null, to = null;
            if (Date_Check.IsChecked == true)
            {
                if (!date_br_begin.SelectedDate.HasValue)
                {
                    RejectFilter(date_br_begin, "اختر تاريخ بداية البحث.");
                    return;
                }
                if (!date_br_end.SelectedDate.HasValue)
                {
                    RejectFilter(date_br_end, "اختر تاريخ نهاية البحث.");
                    return;
                }
                from = date_br_begin.SelectedDate.Value.Date;
                to = date_br_end.SelectedDate.Value.Date;
                if (to < from)
                {
                    RejectFilter(date_br_end, "يجب أن يكون تاريخ النهاية مساويًا لتاريخ البداية أو بعده.");
                    return;
                }
            }

            int? beneficiaryId = null;
            if (by_metar.IsChecked == true)
            {
                if (Combo_benef.SelectedItem is not T_Benef beneficiary)
                {
                    RejectFilter(Combo_benef, "اختر المستفيد المطلوب البحث عنه.");
                    return;
                }
                beneficiaryId = beneficiary.Benef_ID;
            }

            // القيم الموجودة في أدوات الفلاتر غير المفعّلة لا تدخل ضمن الاستعلام.
            model.ApplyFilters(premise, from, to, beneficiaryId);
        }


        private void BeneficiaryFilter_Click(object sender, RoutedEventArgs e)
        {
            Combo_benef.SelectedIndex = -1;
            FieldFeedback.Clear(Combo_benef);
            if (by_metar.IsChecked == true) Combo_benef.Focus();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            Combo_Locaton.SelectedIndex = Combo_benef.SelectedIndex = -1;
            Date_Check.IsChecked = by_metar.IsChecked = false;
            DateFilter_Click(sender, e);
            BeneficiaryFilter_Click(sender, e);
            foreach (Control field in new Control[] { Combo_Locaton, Combo_benef, date_br_begin, date_br_end })
                FieldFeedback.Clear(field);
            // يفرغ الإلغاء نتائج المصدر نفسه، دون فقد قوائم الاختيار أو تغيير آلية العرض الفوري.
            if (DataContext is Inventory_System.viewo_Model.owned_contrect_viewomodel model)
                model.ApplyFilters(null, null, null, null);
            Benef_List.SelectedItem = null;
            Combo_Locaton.Focus();
        }








        #endregion

        private void Benef_List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (Benef_List.SelectedItem == null)
                    return;
                T_OwnedPremises_Contrect t = ((T_OwnedPremises_Contrect)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow);
                Text_Elec_meter.Text = t.Cont_data;
                date_Engry.SelectedDate = t.Cont_date.ToDateTime();
                Combo_benef1.SelectedValue = t.Benef_ID;
                Text_Elec_meter.Focus();
                optype = 2;
                Engry_Windows.IsOpen = true;
            }
            catch(Exception ex)
            {
                ThemedMessageBox.Show(
               null,
               title: "منظومة الحصر",
               text: $"خطأ {ex.Message}",
               messageBoxButtons: MessageBoxButton.OK,
               defaultButton: MessageBoxResult.OK,
               icon: MessageBoxImage.Error,
               true);
            }
        }

        private void Add_benef_Click(object sender, RoutedEventArgs e)
        {
            benef_window f = new benef_window();
            f.ShowDialog();
            Combo_benef.ItemsSource = App._listbenef;
            Combo_benef1.ItemsSource = App._listbenef;

        }

        #region حفظ بيان العقد والتحقق من تكراره
        private async void Save_Eng_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                #region قيود ادخال
                if (string.IsNullOrWhiteSpace(Text_Elec_meter.Text))
                {
                    Text_Elec_meter.Tag = 1;
                    ErrorRule.ErrorMessage = "أدخل بيان العقد";
                    Text_Elec_meter.GetBindingExpression(TagProperty).UpdateSource();
                    Text_Elec_meter.Focus();
                    return;
                }

                if (date_Engry.SelectedDate == null)
                {
                    date_Engry.Tag = 1;
                    ErrorRule.ErrorMessage = "أدخل التاريخ";
                    date_Engry.GetBindingExpression(TagProperty).UpdateSource();
                    date_Engry.Focus();
                    return;
                }
                if (Combo_benef1.SelectedIndex < 0)
                {
                    Combo_benef1.Tag = 1;
                    ErrorRule.ErrorMessage = "اختار المستفيد";
                    Combo_benef1.GetBindingExpression(TagProperty).UpdateSource();
                    Combo_benef1.Focus();
                    return;
                }
                #endregion
                DbContextHelper.InitOptions(
                App.ConnectionString
            );

                using var db = new Cont_maintContext(DbContextHelper.Options);

                if (optype == 1)
                {
                    
                    // تمنع المعاملة اعتماد حفظين متزامنين بعد نجاح فحص الشهر في كليهما.
                    await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    var model = (owned_contrect_viewomodel)DataContext;
                    if (await model.HasContractForMonthAsync(db, (DateTime)date_Engry.SelectedDate, ((T_Inventory_Location)Combo_Locaton.SelectedItem).T_OwnedPremises.RentalPremises_ID))
                    {
                        // يظهر التكرار على خانة التاريخ ويمنع الحفظ حتى تصحيح الشهر أو المقر.
                        FieldFeedback.Reject(date_Engry, "تم تسجيل نفس البيان لنفس الشهر");
                        date_Engry.Focus();
                        return;
                    }
                    T_OwnedPremises_Contrect t = new T_OwnedPremises_Contrect
                    {
                        Branch_ID = App.location.Branch_ID,
                        Location_ID = App.location.Location_ID,
                        Location = App.location.Location,
                        Cont_data = Text_Elec_meter.Text,
                        Cont_date = date_Engry.SelectedDate.Value.ToDateOnly(),
                        RentalPremises_ID = ((T_Inventory_Location)Combo_Locaton.SelectedItem).T_OwnedPremises.RentalPremises_ID,
                        Benef_ID = ((T_Benef)Combo_benef1.SelectedItem).Benef_ID,



                    };
                    db.T_OwnedPremises_Contrects.Add(t);
                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                if(optype==2)
                {
                    // نسترجع العقد بمفتاحه الكامل، ويجري فحص التاريخ الجديد وحفظه ضمن المعاملة نفسها.
                    var selected = (T_OwnedPremises_Contrect)((ReadonlyThreadSafeProxyForObjectFromAnotherThread)Benef_List.SelectedItem).OriginalRow;
                    await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    var t = await db.T_OwnedPremises_Contrects.SingleAsync(k => k.ID == selected.ID
                        && k.Branch_ID == selected.Branch_ID && k.Location_ID == selected.Location_ID
                        && k.Location == selected.Location);
                    // هذه التغييرات داخل الذاكرة فقط؛ لا تُحفظ إن كان الشهر الجديد مستخدمًا بسجل آخر.
                    t.Cont_data = Text_Elec_meter.Text;
                    t.Cont_date = date_Engry.SelectedDate.Value.ToDateOnly();
                    t.RentalPremises_ID = ((T_Inventory_Location)Combo_Locaton.SelectedItem).T_OwnedPremises.RentalPremises_ID;
                    t.Benef_ID = ((T_Benef)Combo_benef1.SelectedItem).Benef_ID;
                    var model = (owned_contrect_viewomodel)DataContext;
                    t.Cont_date= date_Engry.SelectedDate.Value.ToDateOnly();
                    if (await model.HasContractForMonth_updateAsync(db, t))
                    {
                        // يظهر التكرار على خانة التاريخ ويمنع الحفظ حتى تصحيح الشهر أو المقر.
                        FieldFeedback.Reject(date_Engry, "تم تسجيل نفس البيان لنفس الشهر");
                        date_Engry.Focus();
                        return;
                    }
                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();

                }

                ((owned_contrect_viewomodel)this.DataContext).DataSourceRefresh(new DataSourceRefreshArgs());
                ApplicationThemeHelper.ApplicationThemeName = "Win11Light";

                ThemedMessageBox.Show(
               null,
               title: "منظومة الحصر",
               text: $"تم حفظ بيان التعاقد",
               messageBoxButtons: MessageBoxButton.OK,
               defaultButton: MessageBoxResult.OK,
               icon: MessageBoxImage.Information,
               true);
                ApplicationThemeHelper.ApplicationThemeName = "None";

            }
            catch (Exception ex)
            {
                ApplicationThemeHelper.ApplicationThemeName = "Win11Light";

                ThemedMessageBox.Show(
               null,
               title: "منظومة الحصر",
               text: $"خطأ {ex.Message}",
               messageBoxButtons: MessageBoxButton.OK,
               defaultButton: MessageBoxResult.OK,
               icon: MessageBoxImage.Error,
               true);
                ApplicationThemeHelper.ApplicationThemeName = "None";

            }

        }
        #endregion
      
    }

}
