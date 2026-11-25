using Inventory_System.dbContext;
using Inventory_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory_System.support_Data;

#region إدارة الإدارات والأقسام داخل نطاق الفرع والموقع
// نفس مفاتيح المصدر؛ القسم يتبع إدارة واحدة ولا يتغير ارتباطه بمجرد فتح لوحة التعديل.
public sealed record OrganizationEntry(short Id, string Name, short? ManagementId = null, string? Management = null)
{
    // حقل عرض للبحث فقط؛ يبقى الاسم العربي الأصلي محفوظًا دون تعديل.
    public string SearchName => Inventory_System.converts.ArabicGridSearch.Normalize(Name);
}
public static class OrganizationLookupStore
{
    public static string NormalizeName(string name) => name.Trim().Replace('أ', 'ا').Replace('إ', 'ا').Replace('ة', 'ه').Replace('ى', 'ي');
    public static IQueryable<T_Manage> Managers(Cont_maintContext db, ServiceScope scope) => db.T_Manages
        .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
    public static IQueryable<T_Section> Sections(Cont_maintContext db, ServiceScope scope) => db.T_Sections
        .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);

    public static async Task<List<OrganizationEntry>> LoadAsync(Cont_maintContext db, ServiceScope scope, bool sections)
    {
        if (!sections) return await Managers(db, scope).AsNoTracking().OrderBy(x => x.Manag)
            .Select(x => new OrganizationEntry(x.Manag_ID, x.Manag, null, null)).ToListAsync();
        return await Sections(db, scope).AsNoTracking().OrderBy(x => x.T_Manage.Manag).ThenBy(x => x.Secti)
            .Select(x => new OrganizationEntry(x.Secti_ID, x.Secti, x.Manag_ID, x.T_Manage.Manag)).ToListAsync();
    }

    public static async Task<string?> SaveAsync(Cont_maintContext db, ServiceScope scope, bool sections,
        short? id, string name, short? managementId)
    {
        name = name.Trim();
        if (name.Length is 0 or > 50) return "أدخل الاسم بما لا يتجاوز 50 حرفًا.";
        // الترقيم يدوي كما في المصدر؛ المعاملة تمنع اختيار المفتاح التالي نفسه لحفظين متزامنين.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (sections)
        {
            if (!managementId.HasValue || !await Managers(db, scope).AnyAsync(x => x.Manag_ID == managementId))
                return "اختر الإدارة أو القسم من القائمة الحالية.";
            var query = Sections(db, scope);
            // يحافظ النقل على تكافؤ صور الألف والهاء والياء الذي اعتمده المصدر لمنع تكرار الاسم.
            var names = await query.Where(x => x.Manag_ID == managementId && (!id.HasValue || x.Secti_ID != id)).Select(x => x.Secti).ToListAsync();
            if (names.Any(x => NormalizeName(x) == NormalizeName(name)))
                return "القسم أو الوحدة مسجل مسبقًا لهذه الإدارة.";
            var item = id.HasValue ? await query.SingleOrDefaultAsync(x => x.Secti_ID == id) : new T_Section
                { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location };
            if (item is null) return "السجل لم يعد موجودًا؛ حدّث القائمة.";
            // نقل قسم مرتبط إلى إدارة أخرى يفسد توزيع الهواتف والأثاث؛ لا نسمح به ضمن تعديل الاسم.
            if (id.HasValue && item.Manag_ID != managementId) return "لا يمكن تغيير الإدارة الأصلية للقسم من لوحة تعديل الاسم.";
            if (!id.HasValue)
            {
                var next = (await query.MaxAsync(x => (int?)x.Secti_ID) ?? 0) + 1;
                if (next > short.MaxValue) return "وصل ترقيم الأقسام إلى الحد الأقصى.";
                item.Secti_ID = (short)next; db.T_Sections.Add(item);
            }
            item.Secti = name; item.Manag_ID = managementId.Value; item.Date_Modify = DateOnly.FromDateTime(DateTime.Today);
        }
        else
        {
            var query = Managers(db, scope);
            var names = await query.Where(x => !id.HasValue || x.Manag_ID != id).Select(x => x.Manag).ToListAsync();
            if (names.Any(x => NormalizeName(x) == NormalizeName(name))) return "الإدارة أو القسم مسجل مسبقًا.";
            var item = id.HasValue ? await query.SingleOrDefaultAsync(x => x.Manag_ID == id) : new T_Manage
                { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location };
            if (item is null) return "السجل لم يعد موجودًا؛ حدّث القائمة.";
            if (!id.HasValue)
            {
                var next = (await query.MaxAsync(x => (int?)x.Manag_ID) ?? 0) + 1;
                if (next > short.MaxValue) return "وصل ترقيم الإدارات إلى الحد الأقصى.";
                item.Manag_ID = (short)next; db.T_Manages.Add(item);
            }
            item.Manag = name; item.Date_Modify = DateOnly.FromDateTime(DateTime.Today);
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return null;
    }

    // تستعمل الواجهة هذا الفحص قبل عرض التأكيد، ويعيد الحذف الفحص داخل المعاملة.
    internal static async Task<string?> GetDeleteBlockReasonAsync(Cont_maintContext db, ServiceScope scope, bool sections, short id)
    {
        // نفحص بنود حصر الأثاث أولًا لإظهار سبب المنع، ثم نفحص الهواتف والوحدات التابعة.
        var furniture = db.T_Furniture_Lists.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
        if (await furniture.AnyAsync(x => sections ? x.Secti_ID == id : x.Manag_ID == id))
            return sections
                ? "لا يمكن حذف القسم أو الوحدة لارتباطه ببيانات حصر الأثاث."
                : "لا يمكن حذف الإدارة أو القسم لارتباطه ببيانات حصر الأثاث.";

        // تفاصيل الهواتف تُفحص صراحة ضمن المفتاح المركب لأن ارتباطها غير معرّف في EF.
        var phones = db.T_Sub_PHones_Buildings.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
        if (await phones.AnyAsync(x => sections ? x.Secti_ID == id : x.Mangment_ID == id))
            return "لا يمكن حذف البيان لارتباطه بأرقام هواتف.";
        if (!sections && await Sections(db, scope).AnyAsync(x => x.Manag_ID == id))
            return "لا يمكن حذف الإدارة أو القسم لوجود أقسام أو وحدات تابعة له.";
        return null;
    }

    public static async Task<string?> DeleteAsync(Cont_maintContext db, ServiceScope scope, bool sections, short id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var reason = await GetDeleteBlockReasonAsync(db, scope, sections, id);
        if (reason is not null) return reason;
        if (sections)
        {
            var item = await Sections(db, scope).SingleOrDefaultAsync(x => x.Secti_ID == id);
            if (item != null) db.T_Sections.Remove(item);
        }
        else
        {
            var item = await Managers(db, scope).SingleOrDefaultAsync(x => x.Manag_ID == id);
            if (item != null) db.T_Manages.Remove(item);
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return null;
    }
}
#endregion
