using Inventory_System.dbContext;
using Inventory_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory_System.support_Data;

#region نطاق شركات التأمين وعمليات قوائمها المساعدة
// تستخدم قوائم التأمين نطاق الفرع والموقع المشترك وجداول التأمين المستقلة عن شركات الخدمات.
public static class InsuranceLookupStore
{
    public static async Task<List<ServiceLookupEntry>> LoadAsync(Cont_maintContext db, ServiceScope scope, bool isType)
    {
        if (isType)
            return await db.T_instu_Types.AsNoTracking().Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
                .OrderBy(x => x.instu_Type).Select(x => new ServiceLookupEntry(x.instu_Type_ID, x.instu_Type)).ToListAsync();
        return await db.T_insu_Compnies.AsNoTracking().Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
            .OrderBy(x => x.insu_Compny).Select(x => new ServiceLookupEntry(x.insu_Compny_ID, x.insu_Compny!)).ToListAsync();
    }
    public static async Task<string?> SaveAsync(Cont_maintContext db, ServiceScope scope, bool isType, int? id, string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 50) return "أدخل الاسم بما لا يتجاوز 50 حرفًا.";
        // يظل فحص تكرار الاسم والحفظ في المعاملة نفسها، دون تغيير المفاتيح عند التعديل.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (isType)
        {
            var query = db.T_instu_Types.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
            if (await query.AnyAsync(x => x.instu_Type.Trim() == name && (!id.HasValue || x.instu_Type_ID != id.Value))) return "نوع التأمين مسجل من قبل.";
            var item = id.HasValue ? await query.SingleOrDefaultAsync(x => x.instu_Type_ID == id) : new T_instu_Type { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location };
            if (item == null) return "السجل لم يعد موجودًا؛ حدّث البيانات.";
            item.instu_Type = name;
            if (!id.HasValue) db.T_instu_Types.Add(item);
        }
        else
        {
            var query = db.T_insu_Compnies.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
            if (await query.AnyAsync(x => x.insu_Compny != null && x.insu_Compny.Trim() == name && (!id.HasValue || x.insu_Compny_ID != id.Value))) return "الشركة مسجلة من قبل.";
            var item = id.HasValue ? await query.SingleOrDefaultAsync(x => x.insu_Compny_ID == id) : new T_insu_Compny { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location };
            if (item == null) return "السجل لم يعد موجودًا؛ حدّث البيانات.";
            item.insu_Compny = name;
            if (!id.HasValue) db.T_insu_Compnies.Add(item);
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return null;
    }
    // تستعمل الواجهة هذا الفحص قبل عرض التأكيد، ويعيد الحذف الفحص داخل المعاملة.
    internal static async Task<string?> GetDeleteBlockReasonAsync(Cont_maintContext db, ServiceScope scope, bool isType, int id)
    {
        // يمنع حذف الشركة أو النوع المرتبط بعقود مملوكة أو مؤجرة، ضمن نطاق المفتاح نفسه.
        var owned = db.T_OwnedPremises_Insucs.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
        var rented = db.T_RentalPremises_Insucs.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
        if (await owned.AnyAsync(x => isType ? x.INS_TYPE_ID == id : x.insu_Compny_ID == id)
            || await rented.AnyAsync(x => isType ? x.INS_TYPE_ID == id : x.insu_Compny_ID == id))
            return isType
                ? "لا يمكن حذف نوع التأمين لارتباطه بعقود التأمين."
                : "لا يمكن حذف شركة التأمين لارتباطها بعقود التأمين.";
        return null;
    }

    public static async Task<string?> DeleteAsync(Cont_maintContext db, ServiceScope scope, bool isType, int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var reason = await GetDeleteBlockReasonAsync(db, scope, isType, id);
        if (reason is not null) return reason;
        if (isType)
        {
            var item = await db.T_instu_Types.SingleOrDefaultAsync(x => x.instu_Type_ID == id && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
            if (item != null) db.T_instu_Types.Remove(item);
        }
        else
        {
            var item = await db.T_insu_Compnies.SingleOrDefaultAsync(x => x.insu_Compny_ID == id && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
            if (item != null) db.T_insu_Compnies.Remove(item);
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return null;
    }
}
#endregion
