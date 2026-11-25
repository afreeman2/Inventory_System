using DevExpress.Data.Linq;



using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using DevExpress.Xpf.Core;
using Inventory_System.DatabContext;
using Inventory_System.dbContext;
using Inventory_System.Models;
using System.Collections;

using Microsoft.EntityFrameworkCore;
using Inventory_System.converts;
using System.Windows;

namespace Inventory_System.viewo_Model
{
    public class owned_contrect_viewomodel : ViewModelBase
    {
        #region مصدر العرض وفلاتر العقود
        // تُحفظ نسخة مستقلة من الفلاتر حتى يستخدم استعلام الخلفية آخر اختيار للمبنى أو ضغط على عرض.
        private sealed record ContractFilters(long PremiseId, int BranchId, int LocationId, string Location,
            DateOnly? From, DateOnly? To, int? BeneficiaryId);
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
                        KeyExpression = nameof(T_OwnedPremises_Contrect.ID)
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

        public void ApplyFilters(T_Inventory_Location? premise, DateTime? from, DateTime? to, int? beneficiaryId)
        {
            // غياب المقر يعني نتائج فارغة عند الفتح والإلغاء؛ تُطبق بقية الفلاتر فقط إذا مررتها الواجهة.
            // العرض يعتمد رقم ملكية المبنى، وهو RentalPremises_ID نفسه الموجود في العقد.
            var filters = premise?.T_OwnedPremises == null ? null : new ContractFilters(premise.T_OwnedPremises.RentalPremises_ID,
                premise.Branch_ID, premise.Location_ID, premise.Location,
                from.HasValue ? DateOnly.FromDateTime(from.Value) : null,
                to.HasValue ? DateOnly.FromDateTime(to.Value) : null, beneficiaryId);
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

        private IQueryable<T_OwnedPremises_Contrect> BuildFilteredQuery(Cont_maintContext context)
        {
            var filters = System.Threading.Volatile.Read(ref _filters);
            var query = context.T_OwnedPremises_Contrects.AsNoTracking();
            if (filters == null) return query.Where(item => false);

            // نطابق ملكية المبنى المختار مباشرة مع نطاق الفرع والموقع.
            query = query.Where(item => item.Branch_ID == filters.BranchId
                && item.Location_ID == filters.LocationId && item.Location == filters.Location
                && item.RentalPremises_ID == filters.PremiseId);
            if (filters.From is { } from) query = query.Where(item => item.Cont_date >= from);
            if (filters.To is { } to) query = query.Where(item => item.Cont_date <= to);
            if (filters.BeneficiaryId is { } beneficiaryId)
                query = query.Where(item => item.Benef_ID == beneficiaryId);
            return query.Include(k=>k.T_Benef).OrderBy(item => item.ID);
        }

        private IQueryable<T_OwnedPremises_Contrect> BuildDisplayQuery(Cont_maintContext context) =>
            // يحتاج فتح التعديل وحفظه إلى سجل العقد نفسه، لا كائن مجهول ناتج عن Select.
            // نحمل المقر المرتبط أيضًا؛ يبقى المستفيد محملًا في BuildFilteredQuery وتبقى مسارات الأعمدة سليمة.
            BuildFilteredQuery(context).Include(item => item.T_OwnedPremise);
        #endregion

        #region منع تكرار بيان المقر في الشهر نفسه

        // يستعمل الفحص سياق الحفظ نفسه ليبقى التحقق والإدراج داخل المعاملة نفسها.
        // نطابق أجزاء نطاق المقر كاملة حتى لا يمنع بيان فرع آخر تسجيل البيان الحالي.
        [Command(false)]
        public Task<bool> HasContractForMonthAsync(Cont_maintContext context, DateTime this_date,long RentalPremises_ID)
        {
            var month = this_date.Month;
            var year = this_date.Year;
            return context.T_OwnedPremises_Contrects.AsNoTracking().AnyAsync(item =>
                item.RentalPremises_ID == RentalPremises_ID
               
                && item.Cont_date.Month == month && item.Cont_date.Year == year);
        }

        #endregion
        #region منع تكرار بيان المقر في الشهر نفسه للتعديل

        // نفحص الشهر والسنة المعدلين للمقر نفسه، ونستثني السجل الحالي فقط من المقارنة.
        [Command(false)]
        public Task<bool> HasContractForMonth_updateAsync(Cont_maintContext context, T_OwnedPremises_Contrect contract)
        {
            var month = contract.Cont_date.Month;
            var year = contract.Cont_date.Year;
            return context.T_OwnedPremises_Contrects.AsNoTracking().AnyAsync(item =>
                item.RentalPremises_ID == contract.RentalPremises_ID
               
                && item.ID != contract.ID
                && item.Cont_date.Month == month && item.Cont_date.Year == year);
        }

        #endregion
        #region أوامر تحديث البيانات والتحقق والحذف

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
            var key = Convert.ToInt16( args.Keys.Single());
            DbContextHelper.InitOptions(
                App.ConnectionString
            );
            using var context = new Cont_maintContext(DbContextHelper.Options);
            // التحقق من ارتباط بيان التعاقد بالمستندات
            // منع الحذف إذا كان المقر مرتبطًا بمستندات أو تأمين أو فواتير أو عقود.
           
            // تأكيد الحذف
            ApplicationThemeHelper.ApplicationThemeName = "Win11Light";

            var result = ThemedMessageBox.Show(
                null,
                title: "منظومة الحصر",
                text: "هل أنت متأكد من حذف بيان التعاقد؟",
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
            var item = new T_OwnedPremises_Contrect
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
                text: "تم حذف بيان التعاقد بنجاح.",
                messageBoxButtons: MessageBoxButton.OK,
                defaultButton: MessageBoxResult.OK,
                icon: MessageBoxImage.Information,
                true);
            ApplicationThemeHelper.ApplicationThemeName = "None";

        }

        #endregion
    }

}
