using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Grid;
using DevExpress.Mvvm.Xpf;
using Inventory_System.converts;
using Inventory_System.Dashboard;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using Inventory_System.viewo_Model;
using MahApps.Metro.Controls;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ComboBox = System.Windows.Controls.ComboBox;
using Control = System.Windows.Controls.Control;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Inventory_System.Inventory_Windows.owned_Windows
{
    public partial class owned_Water_Window : MetroWindow
    {
        #region تهيئة النافذة وتحديد خاناتها
        // نحفظ هوية الفاتورة كاملة عند فتح التعديل، فلا يتغير السجل المستهدف بتغير تحديد الجدول.
        private sealed record PowerKey(long Id, int BranchId, int LocationId, string Location, long PremiseId);
        private PowerKey? editingPower;
        private bool savingPower;
        private int optype;
        // تحفظ أسماء عناصر المصدر لتسهيل ربط منطق البيانات بهذه الواجهة لاحقًا.
        public owned_Water_Window()
        {
            InitializeComponent();
        }

        private Control[] DraftFields => [Text_Elec_meter, Text_engry_Read, Text_Electric_Value,
            date_Engry, Text_Engry_inv, Text_engry_Debts, date_Eng_from, date_Eng_to];
        private Control[] FilterFields => [Combo_Locaton, date_br_begin, date_br_end, txt_metar];
        #endregion

        #region فتح بيانات العداد وإلغاء المسودة
        private void NewPower_Click(object sender, RoutedEventArgs e)
        {
            if (savingPower) return;
            if (Combo_Locaton.SelectedItem is null)
            {
                FieldFeedback.Reject(Combo_Locaton, "اختر المقر أولًا.");
                return;
            }
            ClearDraft();
            optype = 1;
            PowerDraftTitle.Text = "بيانات عدادات المياه";
            Engry_Windows.IsOpen = true;
            Text_Elec_meter.Focus();
        }

        private void PowerRow_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (savingPower) return;
            var rowHandle = ((TableView)engry_List.View).FocusedRowHandle;
            if (engry_List.SelectedItem is null || rowHandle < 0) return;
            ClearDraft();
            // نقرأ حقول الصف من الجدول نفسه لدعم الكيان المباشر أو مصدر العرض الفوري دون تحويل نوع مجهول.
            object? Value(string name) => engry_List.GetCellValue(rowHandle, name);
            Text_Elec_meter.Text = Convert.ToString(Value("R_Water"), CultureInfo.CurrentCulture);
            Text_engry_Read.EditValue = Value("Water_Read");
            Text_Electric_Value.EditValue = Value("R_WaterVal");
            Text_engry_Debts.EditValue = Value("debts_value");
            Text_Engry_inv.Text = Convert.ToString(Value("Water_inv"), CultureInfo.CurrentCulture);
            date_Engry.SelectedDate = ToDate(Value("Date_Input"));
            date_Eng_from.SelectedDate = ToDate(Value("From_Date"));
            date_Eng_to.SelectedDate = ToDate(Value("To_Date"));
            // تبقى هوية الصف في المسودة، مع نسخة مفتاح مستقلة للحفظ دون تعديل السجل بمجرد فتحه.
            PowerFields.Tag = engry_List.SelectedItem;
            editingPower = new PowerKey(System.Convert.ToInt64(Value("ID")),
                System.Convert.ToInt32(Value("Branch_ID")), System.Convert.ToInt32(Value("Location_ID")),
                System.Convert.ToString(Value("Location"), CultureInfo.CurrentCulture)!,
                System.Convert.ToInt64(Value("RentalPremises_ID")));
            optype = 2;
            PowerDraftTitle.Text = "تعديل بيانات عداد المياه";
            foreach (var field in DraftFields) FieldFeedback.Clear(field);
            Engry_Windows.IsOpen = true;
            Text_Elec_meter.Focus();
        }

        private static DateTime? ToDate(object? value) => value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            DateTime date => date,
            _ => null
        };

        private void CancelDraft_Click(object sender, RoutedEventArgs e)
        {
            // لا نغلق مسودة جارٍ اعتمادها قبل معرفة نتيجة الحفظ.
            if (!savingPower) Engry_Windows.IsOpen = false;
        }

        private void PowerFlyout_Changed(object sender, RoutedEventArgs e)
        {
            // الإغلاق والإلغاء وEscape يزيلون المسودة وأخطاءها دون تنفيذ أمر الحفظ.
            if (!Engry_Windows.IsOpen) ClearDraft();
        }

        private void ClearDraft()
        {
            Text_Elec_meter.Clear();
            Text_Engry_inv.Clear();
            Text_engry_Read.EditValue = Text_Electric_Value.EditValue = Text_engry_Debts.EditValue = null;
            foreach (var date in new[] { date_Engry, date_Eng_from, date_Eng_to })
            {
                date.SelectedDate = null;
                date.Text = string.Empty;
            }
            PowerFields.Tag = null;
            editingPower = null;
            optype = 0;
            foreach (var field in DraftFields) FieldFeedback.Clear(field);
        }
        #endregion

        #region تفعيل فلاتر البحث وتفريغها
        private void DateFilter_Click(object sender, RoutedEventArgs e)
        {
            date_br_begin.SelectedDate = date_br_end.SelectedDate = null;
            date_br_begin.Text = date_br_end.Text = string.Empty;
            FieldFeedback.Clear(date_br_begin);
            FieldFeedback.Clear(date_br_end);
            if (Date_Check.IsChecked == true) date_br_begin.Focus();
        }

        private void MeterFilter_Click(object sender, RoutedEventArgs e)
        {
            txt_metar.Clear();
            FieldFeedback.Clear(txt_metar);
            if (by_metar.IsChecked == true) txt_metar.Focus();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            Combo_Locaton.SelectedIndex = -1;
            Date_Check.IsChecked = by_metar.IsChecked = false;
            DateFilter_Click(sender, e);
            MeterFilter_Click(sender, e);
            engry_List.SelectedItem = null;
            foreach (var field in FilterFields) FieldFeedback.Clear(field);
            // يتولى أمر البيانات الخارجي تفريغ النتائج إن توفر، ولا نستبدل ربط ItemsSource محليًا.
            ExecuteCommand(But_Cancl.Tag as ICommand, PowerFilters);
            // نفرغ المصدر نفسه عند الإلغاء مع الاحتفاظ بقوائم المباني وربط الجدول.
            if (DataContext is Inventory_System.viewo_Model.owned_Water_Viewomodel model)
                model.ApplyFilters(null, null, null, null);
            Combo_Locaton.Focus();
        }

        private void SearchPower_Click(object sender, RoutedEventArgs e)
        {
            // زر العرض وتغيير المبنى يستعملان تجميع الفلاتر الموجود في كود المستخدم نفسه.
            if (DataContext is Inventory_System.viewo_Model.owned_Water_Viewomodel)
            {
                ApplySelectedFilters(true);
                return;
            }
            if (Search_Button.Tag is not ICommand command) return;
            foreach (var field in FilterFields) FieldFeedback.Clear(field);
            if (Combo_Locaton.SelectedItem is null)
            {
                FieldFeedback.Reject(Combo_Locaton, "اختر المقر لعرض بيانات المياه."); return;
            }
            if (Date_Check.IsChecked == true)
            {
                if (!RequireDate(date_br_begin, "اختر تاريخ بداية البحث.")) return;
                if (!RequireDate(date_br_end, "اختر تاريخ نهاية البحث.")) return;
                if (date_br_end.SelectedDate < date_br_begin.SelectedDate)
                {
                    FieldFeedback.Reject(date_br_end, "تاريخ النهاية يجب أن يكون مساويًا للبداية أو بعدها."); return;
                }
            }
            if (by_metar.IsChecked == true && !long.TryParse(txt_metar.Text, out _))
            {
                FieldFeedback.Reject(txt_metar, "أدخل رقم عداد صحيحًا."); return;
            }
            ExecuteCommand(command, PowerFilters);
        }
        #endregion

        #region حفظ الإضافة والتعديل والتحقق من المدخلات
        private async void SavePower_Click(object sender, RoutedEventArgs e)
        {
            // لا يبدأ حفظ ثانٍ قبل انتهاء الأول حتى مع النقر المتكرر أو اختصار لوحة المفاتيح.
            if (savingPower) return;
            savingPower = true;
            Save_Eng.IsEnabled = false;
            try
            {
                DbContextHelper.InitOptions(App.ConnectionString);
                await using var db = new Cont_maintContext(DbContextHelper.Options!);
                if (!await SavePowerAsync(db)) return;
                UiMessages.Show(this, "بيانات عدادات المياه", "تم حفظ فاتورة المياه.", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                UiMessages.Show(this, "تعذّر حفظ فاتورة المياه", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                savingPower = false;
                Save_Eng.IsEnabled = true;
            }
        }

        private bool ValidatePowerDraft(out long meter, out decimal reading, out decimal amount, out decimal debt)
        {
            meter = 0; reading = amount = debt = 0;
            foreach (var field in DraftFields) FieldFeedback.Clear(field);
            if (optype == 1 && Combo_Locaton.SelectedItem is not T_Inventory_Location { T_OwnedPremises: not null })
                return FieldFeedback.Reject(Combo_Locaton, "اختر مبنى له بيانات ملكية أولًا.");
            if (!long.TryParse(Text_Elec_meter.Text, out meter))
            {
                return FieldFeedback.Reject(Text_Elec_meter, "أدخل رقم عداد صحيحًا.");
            }
            if (!ReadNumber(Text_engry_Read, "أدخل قيمة رقمية للقراءة الحالية.", out reading)) return false;
            if (!ReadNumber(Text_Electric_Value, "أدخل قيمة رقمية للفاتورة.", out amount)) return false;
            if (!RequireDate(date_Engry, "أدخل تاريخ الفاتورة.")) return false;
            if (string.IsNullOrWhiteSpace(Text_Engry_inv.Text))
            {
                return FieldFeedback.Reject(Text_Engry_inv, "أدخل رقم الفاتورة.");
            }
            if (Text_Engry_inv.Text.Trim().Length > 50)
                return FieldFeedback.Reject(Text_Engry_inv, "رقم الفاتورة يجب ألا يتجاوز 50 حرفًا.");
            if (!ReadNumber(Text_engry_Debts, "أدخل قيمة رقمية للدين الحالي.", out debt)) return false;
            if (!RequireDate(date_Eng_from, "أدخل بداية فترة السداد.")) return false;
            if (!RequireDate(date_Eng_to, "أدخل نهاية فترة السداد.")) return false;
            if (date_Eng_to.SelectedDate < date_Eng_from.SelectedDate)
            {
                return FieldFeedback.Reject(date_Eng_to, "نهاية فترة السداد يجب أن تكون مساوية للبداية أو بعدها.");
            }
            return true;
        }

        private async Task<bool> SavePowerAsync(Cont_maintContext db)
        {
            if (DataContext is not owned_Water_Viewomodel model || optype is not (1 or 2)) return false;
            if (!ValidatePowerDraft(out var meter, out var reading, out var amount, out var debt)) return false;

            // نأخذ نسخة القيم والهوية قبل أول انتظار، حتى لا يغير تحرير خانة أثناء الحفظ البيانات المرسلة.
            var key = editingPower;
            var isInsert = optype == 1;
            var premise = Combo_Locaton.SelectedItem as T_Inventory_Location;
            var candidate = new T_OwnedPremises_Water
            {
                ID = isInsert ? 0 : key!.Id,
                RentalPremises_ID = isInsert ? premise!.T_OwnedPremises.RentalPremises_ID : key!.PremiseId,
                Branch_ID = isInsert ? premise!.Branch_ID : key!.BranchId,
                Location_ID = isInsert ? premise!.Location_ID : key!.LocationId,
                Location = isInsert ? premise!.Location : key!.Location
            };
            FillPowerInvoice(candidate, meter, reading, amount, debt);

            // الفحص والإدراج أو التعديل ضمن معاملة واحدة مثل شاشة العقود.
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            T_OwnedPremises_Water invoice;
            if (isInsert)
            {
                if (await model.HasContractForMonthAsync(db, candidate.From_Date.ToDateTime(TimeOnly.MinValue),
                    candidate.To_Date.ToDateTime(TimeOnly.MinValue), meter, candidate.RentalPremises_ID))
                    return FieldFeedback.Reject(date_Eng_from, "فترة السداد تتداخل مع فاتورة مسجلة سابقًا لنفس المبنى والعداد.");
                invoice = candidate;
                db.T_OwnedPremises_Waters.Add(invoice);
            }
            else
            {
                invoice = await db.T_OwnedPremises_Waters.SingleAsync(item => item.ID == candidate.ID
                    && item.Branch_ID == candidate.Branch_ID && item.Location_ID == candidate.Location_ID && item.Location == candidate.Location);
                // نطبق الفترة المعدلة في الذاكرة قبل فحصها؛ الرفض لا يستدعي SaveChanges.
                db.Entry(invoice).CurrentValues.SetValues(candidate);
                if (await model.HasContractForMonth_updateAsync(db, invoice))
                    return FieldFeedback.Reject(date_Eng_from, "فترة السداد تتداخل مع فاتورة أخرى لنفس المبنى والعداد.");
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            // تبقى الفاتورة المحفوظة في وضع التعديل لمنع إدراج نسخة ثانية عند تكرار الحفظ.
            editingPower = new PowerKey(invoice.ID, invoice.Branch_ID, invoice.Location_ID, invoice.Location, invoice.RentalPremises_ID);
            optype = 2;
            PowerDraftTitle.Text = "تعديل بيانات عداد المياه";
            model.DataSourceRefresh(new DataSourceRefreshArgs());
            return true;
        }

        private void FillPowerInvoice(T_OwnedPremises_Water invoice, long meter, decimal reading, decimal amount, decimal debt)
        {
            // نحدّث الحقول القابلة للتحرير فقط؛ يبقى المفتاح والمبنى الأصليان محفوظين عند التعديل.
            invoice.R_Water = meter;
            invoice.Water_Read = reading;
            invoice.R_WaterVal = amount;
            invoice.debts_value = debt;
            invoice.Water_inv = Text_Engry_inv.Text.Trim();
            invoice.Date_Input = DateOnly.FromDateTime(date_Engry.SelectedDate!.Value);
            invoice.From_Date = DateOnly.FromDateTime(date_Eng_from.SelectedDate!.Value);
            invoice.To_Date = DateOnly.FromDateTime(date_Eng_to.SelectedDate!.Value);
            invoice.Date_Modify = DateOnly.FromDateTime(DateTime.Now);
        }

        private static bool ReadNumber(BaseEdit editor, string message, out decimal number)
        {
            // الصفر قيمة مدخلة؛ نرفض الفراغ والنص غير الرقمي والقيمة التي تتجاوز دقة numeric(18,3).
            number = 0;
            if (editor.EditValue is null || !decimal.TryParse(Convert.ToString(editor.EditValue, CultureInfo.CurrentCulture),
                NumberStyles.Number, CultureInfo.CurrentCulture, out number)) return FieldFeedback.Reject(editor, message);
            if (number <= -1000000000000000m || number >= 1000000000000000m || decimal.Round(number, 3) != number)
                return FieldFeedback.Reject(editor, "أدخل قيمة ضمن 15 رقمًا صحيحًا وثلاث منازل عشرية كحد أقصى.");
            return true;
        }

        private static bool RequireDate(DatePicker date, string message) =>
            date.SelectedDate.HasValue || FieldFeedback.Reject(date, message);

        private void ExecuteCommand(ICommand? command, object parameter)
        {
            try
            {
                if (command?.CanExecute(parameter) == true) command.Execute(parameter);
            }
            catch (Exception ex)
            {
                UiMessages.Show(this, "بيانات عدادات المياه", ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region اختصارات النافذة والتنقل بين الخانات
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void PowerWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && Engry_Windows.IsOpen)
            {
                if (!savingPower) Engry_Windows.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.F5 && !Engry_Windows.IsOpen)
            {
                SearchPower_Click(Search_Button, new RoutedEventArgs()); e.Handled = true;
            }
        }

        private void Field_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || sender is not UIElement field) return;
            // لمحرر التاريخ أجزاء داخلية؛ نحدد الخانة التالية صراحة حتى لا يبقى المؤشر داخل التقويم.
            if (field is DatePicker date)
            {
                UIElement? next = date == date_Engry ? Text_Engry_inv
                    : date == date_Eng_from ? date_Eng_to
                    : date == date_Eng_to ? Save_Eng
                    : date == date_br_begin ? date_br_end
                    : date == date_br_end ? (txt_metar.IsEnabled ? txt_metar : Search_Button) : null;
                if (next == null) return;
                e.Handled = true;
                date.IsDropDownOpen = false;
                if (next is DatePicker nextDate)
                {
                    // ندخل إلى نص التاريخ التالي مباشرة، وتبقى آلية تحليل التاريخ والتحقق الأصلية فعالة.
                    nextDate.ApplyTemplate();
                    (nextDate.Template.FindName("PART_TextBox", nextDate) as UIElement ?? nextDate).Focus();
                }
                else next.Focus();
                return;
            }
            // تظل القائمة المفتوحة مسؤولة عن اعتماد العنصر المحدد أولًا.
            if (sender is ComboBox { IsDropDownOpen: true }) return;
            field.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
        #endregion
        #region تحميل قوائم المقرات والمستفيدين

        private async Task LoadUsersAsync()
        {
            DbContextHelper.InitOptions(App.ConnectionString);
            await using var db = new Cont_maintContext(DbContextHelper.Options!);
            if (App._listbuilding == null)
            {
                // تبقى آلية تحميل قوائم اختيارات الفلاتر كما هي في كود الواجهة.
                var in_loctions = await db.T_Inventory_Locations
                    .AsNoTracking().Include(k => k.T_OwnedPremises)
                    .OrderBy(k => k.In_Location)
                    .ToListAsync();
                App._listbuilding = new ObservableCollection<T_Inventory_Location>(in_loctions);
            }

            Combo_Locaton.ItemsSource = App._listbuilding;
           

        }


        #endregion


        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
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

        private void Combo_Locaton_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySelectedFilters(false);
        }
        private void ApplySelectedFilters(bool showValidation)
        {
            // تشترك قائمة المباني وزر عرض في جمع الفلاتر، ويظل الاستعلام في ViewModel الحالي.
            if (DataContext is not Inventory_System.viewo_Model.owned_Water_Viewomodel model) return;
            if (engry_List == null || txt_metar == null || date_br_begin == null || date_br_end == null) return;
            foreach (Control field in new Control[] { Combo_Locaton, txt_metar, date_br_begin, date_br_end })
                FieldFeedback.Clear(field);

            // التغيير التلقائي لا يعرض أخطاء قبل تنفيذ الأمر؛ إذا كانت الفلاتر ناقصة تبقى النتائج فارغة.
            void RejectFilter(Control field, string message)
            {
                if (showValidation) FieldFeedback.Reject(field, message);
                else model.ApplyFilters(null, null, null, null);
            }

            if (Combo_Locaton.SelectedItem is not T_Inventory_Location premise)
            {
                RejectFilter(Combo_Locaton, "اختر المقر لعرض فواتير المياه.");
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

            long? meterNumber = null;
            if (by_metar.IsChecked == true)
            {
                if (!long.TryParse(txt_metar.Text, out var selectedMeter))
                {
                    RejectFilter(txt_metar, "أدخل رقم عداد صحيحًا للبحث.");
                    return;
                }
                meterNumber = selectedMeter;
            }

            // القيم الموجودة في أدوات الفلاتر غير المفعّلة لا تدخل ضمن الاستعلام.
            model.ApplyFilters(premise, from, to, meterNumber);
        }

    }
}
