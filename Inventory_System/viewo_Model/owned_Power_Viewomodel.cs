using DevExpress.Data.Linq;
using DevExpress.Data.Async.Helpers;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using Inventory_System.converts;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Diagnostics.Metrics;
using System.Windows;
using System.Windows.Controls;

namespace Inventory_System.viewo_Model
{
    public class owned_Power_Viewomodel : ViewModelBase
    {
        #region مصدر العرض وفلاتر فواتير الكهرباء
        // تُحفظ نسخة مستقلة من الفلاتر حتى يستخدم استعلام الخلفية آخر اختيار للمبنى أو ضغط على عرض.
        private sealed record ContractFilters(long PremiseId, int BranchId, int LocationId, string Location,
            DateOnly? From, DateOnly? To, long? Meternumber);
        private ContractFilters? _filters;
        private EntityInstantFeedbackSource? _ItemsSource;
        private IList? _listing;

        public EntityInstantFeedbackSource ItemsSource
        {
            get
            {
                if (_ItemsSource == null)
                {
                    _ItemsSource = new EntityInstantFeedbackSource
                    {
                        KeyExpression = nameof(T_OwnedPremises_Power.ID)
                    };
                    DbContextHelper.InitOptions(App.ConnectionString);
                    _ItemsSource.GetQueryable += (sender, e) =>
                    {
                        var context = new Cont_maintContext(DbContextHelper.Options!);
                        // يحتفظ DevExpress بالسياق حتى ينتهي تحميل الصفحة ثم يحرره في الحدث المقابل.
                        e.Tag = context;
                        e.QueryableSource = BuildDisplayQuery(context);
                    };
                    _ItemsSource.DismissQueryable += (sender, e) => (e.Tag as Cont_maintContext)?.Dispose();
                }
                return _ItemsSource;
            }
        }

        public IList listing
        {
            get
            {
                if (_listing == null && !IsInDesignMode)
                {
                    DbContextHelper.InitOptions(App.ConnectionString);
                    using var context = new Cont_maintContext(DbContextHelper.Options!);
                    // تستخدم القائمة الاحتياطية الفلاتر والإسقاط نفسيهما، فلا تحمل كل العقود دون تقييد.
                    _listing = ((IEnumerable)BuildDisplayQuery(context)).Cast<object>().ToArray();
                }
                return _listing ?? Array.Empty<object>();
            }
        }

        public void ApplyFilters(T_Inventory_Location? premise, DateTime? from, DateTime? to, long? Meternumber)
        {
            // غياب المقر يعني نتائج فارغة عند الفتح والإلغاء؛ تُطبق بقية الفلاتر فقط إذا مررتها الواجهة.
            // رقم العرض المعتمد هو رقم الملكية؛ غياب سجل الملكية يجعل النتائج فارغة دون استثناء.
            var filters = premise?.T_OwnedPremises == null ? null : new ContractFilters(premise.T_OwnedPremises.RentalPremises_ID,
                premise.Branch_ID, premise.Location_ID, premise.Location,
                from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
                to.HasValue ? DateOnly.FromDateTime(to.Value) : null, Meternumber);
            System.Threading.Volatile.Write(ref _filters, filters);
            ReloadItemsSource();
        }

        private void ReloadItemsSource()
        {
            // نعيد إنشاء المصدر لقراءة نتائج الحفظ الجديدة، مع إبقاء فلاتر المقر المعتمدة دون تغيير.
            _listing = null;
            // Refresh يعيد تنفيذ الاستعلام القديم؛ تبديل المصدر يعيد GetQueryable بفلاتر الاختيار الجديد.
            // يفصل الجدول عن المصدر السابق حتى لا تعيد نتيجة متأخرة صفوف المبنى السابق.
            var previousSource = _ItemsSource;
            _ItemsSource = null;
            RaisePropertyChanged(nameof(ItemsSource));
            previousSource?.Dispose();
            RaisePropertyChanged(nameof(listing));
        }

        private IQueryable<T_OwnedPremises_Power> BuildFilteredQuery(Cont_maintContext context)
        {
            var filters = System.Threading.Volatile.Read(ref _filters);
            var query = context.T_OwnedPremises_Powers.AsNoTracking();
            if (filters == null) return query.Where(item => false);

            // نقارن رقم الملكية بالحقل نفسه في الفاتورة مع حفظ نطاق الفرع والموقع.
            query = query.Where(item => item.RentalPremises_ID == filters.PremiseId
                && item.Branch_ID == filters.BranchId && item.Location_ID == filters.LocationId
                && item.Location == filters.Location);
            if (filters.From is { } from) query = query.Where(item => item.Date_Input >= from);
            if (filters.To is { } to) query = query.Where(item => item.Date_Input <= to);
            if (filters.Meternumber is { } Meternumber)
                query = query.Where(item => item.R_power == Meternumber);
            return query.OrderBy(item => item.ID);
        }

        private IQueryable<T_OwnedPremises_Power> BuildDisplayQuery(Cont_maintContext context) =>
            // يحتاج فتح التعديل وحفظه إلى سجل العقد نفسه، لا كائن مجهول ناتج عن Select.
            // نحمل المقر المرتبط أيضًا؛ يبقى المستفيد محملًا في BuildFilteredQuery وتبقى مسارات الأعمدة سليمة.
            BuildFilteredQuery(context).Include(item => item.T_OwnedPremise);
        #endregion

        #region منع تكرار بيان المقر في الشهر نفسه

        // يستعمل الفحص سياق الحفظ نفسه ليبقى التحقق والإدراج داخل المعاملة نفسها.
        // نطابق أجزاء نطاق المقر كاملة حتى لا يمنع بيان فرع آخر تسجيل البيان الحالي.
        [Command(false)]
        public Task<bool> HasContractForMonthAsync(Cont_maintContext context, DateTime fromd,DateTime tod,long power_conter, long RentalPremises_ID)
        {
            // تتداخل الفترتان إذا بدأت الجديدة قبل نهاية السابقة وانتهت بعد بدايتها.
            DateOnly from = fromd.ToDateOnly();
            DateOnly to = tod.ToDateOnly(); 
            long meter = power_conter;

            return context.T_OwnedPremises_Powers.AnyAsync(k =>
                k.R_power == meter &&
                k.RentalPremises_ID == RentalPremises_ID &&
                k.From_Date <= to &&
                k.To_Date >= from);

            
        }

        #endregion
        #region منع تكرار بيان المقر في الشهر نفسه للتعديل

        // نفحص الشهر والسنة المعدلين للمقر نفسه، ونستثني السجل الحالي فقط من المقارنة.
        [Command(false)]
        public Task<bool> HasContractForMonth_updateAsync(Cont_maintContext context, T_OwnedPremises_Power contract)
        {

            var from = contract.From_Date;
            var to = contract.To_Date;
            return context.T_OwnedPremises_Powers.AsNoTracking().AnyAsync(item =>
                item.RentalPremises_ID == contract.RentalPremises_ID
                && item.R_power == contract.R_power &&
                 item.ID != contract.ID
               &&
                item.From_Date <= to &&
               item.To_Date >= from);
        }

        #endregion
        #region أوامر تحديث البيانات والتحقق والحذف

        // يستقبل الزر صف الفاتورة من الجدول، وقد يغلفه مصدر العرض غير المتزامن بوكيل DevExpress.
        private DelegateCommand<object>? _deletePowerCommand;
        public DelegateCommand<object> DeletePowerCommand => _deletePowerCommand ??=
            new DelegateCommand<object>(DeletePower);

        private void DeletePower(object row)
        {
            if (row is ReadonlyThreadSafeProxyForObjectFromAnotherThread proxy)
                row = proxy.OriginalRow;
            if (row is not T_OwnedPremises_Power invoice) return;

            // نحتفظ بمفاتيح الصف قبل التأكيد ولا نستعمل رقمًا مختصرًا أو نطاق مبنى آخر.
            var key = new T_OwnedPremises_Power
            {
                ID = invoice.ID, Branch_ID = invoice.Branch_ID,
                Location_ID = invoice.Location_ID, Location = invoice.Location,
                RentalPremises_ID = invoice.RentalPremises_ID
            };
            if (ThemedMessageBox.Show(null, title: "منظومة الحصر",
                text: "هل أنت متأكد من حذف فاتورة الكهرباء؟",
                messageBoxButtons: MessageBoxButton.YesNo, defaultButton: MessageBoxResult.No,
                icon: MessageBoxImage.Question, true) != MessageBoxResult.Yes) return;

            try
            {
                DbContextHelper.InitOptions(App.ConnectionString);
                using var context = new Cont_maintContext(DbContextHelper.Options!);
                bool deleted = DeletePowerRecord(context, key);
                // يعاد الاستعلام بفلاتر المبنى نفسها بعد نجاح العملية أو إذا حذف السجل من جلسة أخرى.
                ReloadItemsSource();
                ThemedMessageBox.Show(null, title: "منظومة الحصر",
                    text: deleted ? "تم حذف فاتورة الكهرباء بنجاح." : "الفاتورة لم تعد موجودة؛ تم تحديث البيانات.",
                    messageBoxButtons: MessageBoxButton.OK, defaultButton: MessageBoxResult.OK,
                    icon: MessageBoxImage.Information, true);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(null, title: "منظومة الحصر",
                    text: $"تعذر حذف فاتورة الكهرباء: {ex.Message}",
                    messageBoxButtons: MessageBoxButton.OK, defaultButton: MessageBoxResult.OK,
                    icon: MessageBoxImage.Error, true);
            }
        }

        // يطابق المفتاح المركب ورقم الملكية، ويحذف السجل الموجود فقط دون المساس بفواتير بقية المباني.
        private bool DeletePowerRecord(Cont_maintContext context, T_OwnedPremises_Power key)
        {
            var item = context.T_OwnedPremises_Powers.SingleOrDefault(item =>
                item.ID == key.ID && item.Branch_ID == key.Branch_ID
                && item.Location_ID == key.Location_ID && item.Location == key.Location
                && item.RentalPremises_ID == key.RentalPremises_ID);
            if (item == null) return false;
            context.T_OwnedPremises_Powers.Remove(item);
            context.SaveChanges();
            return true;
        }

        [Command]
        public void DataSourceRefresh(DataSourceRefreshArgs args)
        {
            // يستدعي الحفظ هذا الأمر بعد نجاحه؛ يعاد الاستعلام للمبنى المختار وفلاتره نفسها.
            ReloadItemsSource();
        }

        [Command]
        public void ValidateRow(EditFormRowValidationArgs args)
        {
            var context = ((EditIssueInfo)args.EditOperationContext).DbContext;
            context.SaveChanges();
        }


        [Command]
        public void ValidateRowDeletion(EditFormValidateRowDeletionArgs args)
        {
            var key = Convert.ToInt16(args.Keys.Single());
            DbContextHelper.InitOptions(
                App.ConnectionString
            );
            using var context = new Cont_maintContext(DbContextHelper.Options);
            // التحقق من ارتباط فاتورة الكهرباء بالمستندات
            // منع الحذف إذا كان المقر مرتبطًا بمستندات أو تأمين أو فواتير أو عقود.

            // تأكيد الحذف
            ApplicationThemeHelper.ApplicationThemeName = "Win11Light";

            var result = ThemedMessageBox.Show(
                null,
                title: "منظومة الحصر",
                text: "هل أنت متأكد من حذف فاتورة الكهرباء؟",
                messageBoxButtons: MessageBoxButton.YesNo,
                defaultButton: MessageBoxResult.No,
                icon: MessageBoxImage.Question,
                true);

            if (result != MessageBoxResult.Yes)
            {
                ApplicationThemeHelper.ApplicationThemeName = "None";
                return;
            }
            // تنفيذ الحذف
            var item = new T_OwnedPremises_Power
            {
                ID = key
                ,
                Location = App.location.Location,
                Branch_ID = App.location.Branch_ID,
                Location_ID = App.location.Location_ID
            };

            context.Entry(item).State = EntityState.Deleted;

            context.SaveChanges();
            ApplicationThemeHelper.ApplicationThemeName = "Win11Light";

            ThemedMessageBox.Show(
                null,
                title: "منظومة الحصر",
                text: "تم حذف فاتورة الكهرباء بنجاح.",
                messageBoxButtons: MessageBoxButton.OK,
                defaultButton: MessageBoxResult.OK,
                icon: MessageBoxImage.Information,
                true);
            ApplicationThemeHelper.ApplicationThemeName = "None";

        }

        #endregion
    }

}
