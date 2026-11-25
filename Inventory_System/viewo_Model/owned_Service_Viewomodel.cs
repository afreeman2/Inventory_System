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
    public class owned_Service_Viewomodel : ViewModelBase
    {
        #region مصدر العرض وفلاتر عقود شركات الخدمات
        // تُحفظ نسخة مستقلة من الفلاتر حتى يستخدم استعلام الخلفية آخر اختيار للمبنى أو ضغط على عرض.
        private sealed record ServiceFilters(long PremiseId, int BranchId, int LocationId, string Location,
            DateOnly? From, DateOnly? To, int? CompanyId, int? TypeId);
        private ServiceFilters? _filters;
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
                        KeyExpression = nameof(T_OwnedPremises_Serivec.ID)
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

        public void ApplyFilters(T_Inventory_Location? premise, DateTime? from, DateTime? to, int? CompanyId, int? TypeId)
        {
            // غياب المقر يعني نتائج فارغة عند الفتح والإلغاء؛ تُطبق بقية الفلاتر فقط إذا مررتها الواجهة.
            // رقم العرض المعتمد هو رقم الملكية؛ غياب سجل الملكية يجعل النتائج فارغة دون استثناء.
            var filters = premise?.T_OwnedPremises == null ? null : new ServiceFilters(premise.T_OwnedPremises.RentalPremises_ID,
                premise.Branch_ID, premise.Location_ID, premise.Location,
                from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
                to.HasValue ? DateOnly.FromDateTime(to.Value) : null, CompanyId, TypeId);
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

        private IQueryable<T_OwnedPremises_Serivec> BuildFilteredQuery(Cont_maintContext context)
        {
            var filters = System.Threading.Volatile.Read(ref _filters);
            var query = context.T_OwnedPremises_Serivecs.AsNoTracking();
            if (filters == null) return query.Where(item => false);

            // نقارن رقم الملكية بالحقل نفسه في العقد مع حفظ نطاق الفرع والموقع.
            query = query.Where(item => item.RentalPremises_ID == filters.PremiseId
                && item.Branch_ID == filters.BranchId && item.Location_ID == filters.LocationId
                && item.Location == filters.Location);
            if (filters.From is { } from) query = query.Where(item => item.Date_Input >= from);
            if (filters.To is { } to) query = query.Where(item => item.Date_Input <= to);
            if (filters.CompanyId is { } companyId) query = query.Where(item => item.Service_Compny_ID == companyId);
            if (filters.TypeId is { } typeId) query = query.Where(item => item.Sevice_Compny_Type_ID == typeId);
            return query.OrderBy(item => item.ID);
        }

        private IQueryable<T_OwnedPremises_Serivec> BuildDisplayQuery(Cont_maintContext context) =>
            // يحتاج فتح التعديل وحفظه إلى سجل العقد نفسه، لا كائن مجهول ناتج عن Select.
            // نحمل المقر المرتبط أيضًا؛ تبقى حقول العقد ونطاق المبنى متاحين للتعديل والحذف.
            BuildFilteredQuery(context).Include(item => item.T_OwnedPremise)
                .Include(item => item.T_Service_Compny).Include(item => item.T_Sevice_Compny_Type);
        #endregion

        #region منع تكرار العقد وتداخل فترة التعاقد
        // تشمل المقارنة المبنى والشركة والنوع والموقع كاملًا؛ ويستثنى السجل الجاري تعديله فقط.
        private static IQueryable<T_OwnedPremises_Serivec> ComparableContracts(Cont_maintContext context, T_OwnedPremises_Serivec contract) =>
            context.T_OwnedPremises_Serivecs.AsNoTracking().Where(item =>
                item.RentalPremises_ID == contract.RentalPremises_ID && item.Branch_ID == contract.Branch_ID
                && item.Location_ID == contract.Location_ID && item.Location == contract.Location
                && item.Service_Compny_ID == contract.Service_Compny_ID && item.Sevice_Compny_Type_ID == contract.Sevice_Compny_Type_ID
                && (contract.ID == 0 || item.ID != contract.ID));

        [Command(false)]
        public Task<bool> HasContractForMonthAsync(Cont_maintContext context, T_OwnedPremises_Serivec contract) =>
            ComparableContracts(context, contract).AnyAsync(item => item.Date_Input.Year == contract.Date_Input.Year
                && item.Date_Input.Month == contract.Date_Input.Month);

        // التداخل شامل ليومي البداية والنهاية ويغني عن المرور على كل يوم في المصدر القديم.
        [Command(false)]
        public Task<bool> HasOverlappingContractAsync(Cont_maintContext context, T_OwnedPremises_Serivec contract) =>
            ComparableContracts(context, contract).AnyAsync(item => item.From_Date <= contract.To_Date && item.To_Date >= contract.From_Date);
        #endregion
        #region أوامر تحديث البيانات والتحقق والحذف

        // يستقبل الزر صف العقد من الجدول، وقد يغلفه مصدر العرض غير المتزامن بوكيل DevExpress.
        private DelegateCommand<object>? _deleteServiceCommand;
        public DelegateCommand<object> DeleteServiceCommand => _deleteServiceCommand ??=
            new DelegateCommand<object>(DeleteService);

        private void DeleteService(object row)
        {
            if (row is ReadonlyThreadSafeProxyForObjectFromAnotherThread proxy)
                row = proxy.OriginalRow;
            if (row is not T_OwnedPremises_Serivec invoice) return;

            // نحتفظ بمفاتيح الصف قبل التأكيد ولا نستعمل رقمًا مختصرًا أو نطاق مبنى آخر.
            var key = new T_OwnedPremises_Serivec
            {
                ID = invoice.ID, Branch_ID = invoice.Branch_ID,
                Location_ID = invoice.Location_ID, Location = invoice.Location,
                RentalPremises_ID = invoice.RentalPremises_ID
            };
            if (ThemedMessageBox.Show(null, title: "منظومة الحصر",
                text: "هل أنت متأكد من حذف عقد شركة الخدمات؟",
                messageBoxButtons: MessageBoxButton.YesNo, defaultButton: MessageBoxResult.No,
                icon: MessageBoxImage.Question, true) != MessageBoxResult.Yes) return;

            try
            {
                DbContextHelper.InitOptions(App.ConnectionString);
                using var context = new Cont_maintContext(DbContextHelper.Options!);
                bool deleted = DeleteServiceRecord(context, key);
                // يعاد الاستعلام بفلاتر المبنى نفسها بعد نجاح العملية أو إذا حذف السجل من جلسة أخرى.
                ReloadItemsSource();
                ThemedMessageBox.Show(null, title: "منظومة الحصر",
                    text: deleted ? "تم حذف عقد شركة الخدمات بنجاح." : "العقد لم تعد موجودة؛ تم تحديث البيانات.",
                    messageBoxButtons: MessageBoxButton.OK, defaultButton: MessageBoxResult.OK,
                    icon: MessageBoxImage.Information, true);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(null, title: "منظومة الحصر",
                    text: $"تعذر حذف عقد شركة الخدمات: {ex.Message}",
                    messageBoxButtons: MessageBoxButton.OK, defaultButton: MessageBoxResult.OK,
                    icon: MessageBoxImage.Error, true);
            }
        }

        // يطابق المفتاح المركب ورقم الملكية، ويحذف السجل الموجود فقط دون المساس بعقود بقية المباني.
        private bool DeleteServiceRecord(Cont_maintContext context, T_OwnedPremises_Serivec key)
        {
            var item = context.T_OwnedPremises_Serivecs.SingleOrDefault(item =>
                item.ID == key.ID && item.Branch_ID == key.Branch_ID
                && item.Location_ID == key.Location_ID && item.Location == key.Location
                && item.RentalPremises_ID == key.RentalPremises_ID);
            if (item == null) return false;
            context.T_OwnedPremises_Serivecs.Remove(item);
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


        #endregion
    }

}

