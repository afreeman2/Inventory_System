using Inventory_System.dbContext;
using Inventory_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory_System.support_Data;

#region نطاق شركات الخدمات وعمليات قوائمها المساعدة
// المفاتيح قد تتكرر بين المواقع؛ يُمرر نطاق المبنى لكل قراءة وحفظ وحذف.
public sealed record ServiceScope(int BranchId, int LocationId, string Location);
public sealed record ServiceLookupEntry(int Id, string Name)
{
    // تستعمله جداول الخدمات والتأمين للبحث من دون تعديل الاسم الأصلي.
    public string SearchName => Inventory_System.converts.ArabicGridSearch.Normalize(Name);
}

public static class ServiceLookupStore
{
    public static async Task<List<ServiceLookupEntry>> LoadAsync(Cont_maintContext db, ServiceScope scope, bool isType)
    {
        if (isType)
            return await db.T_Sevice_Compny_Types.AsNoTracking().Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
                .OrderBy(x => x.Sevice_Compny_Type).Select(x => new ServiceLookupEntry(x.Sevice_Compny_Type_ID, x.Sevice_Compny_Type)).ToListAsync();
        return await db.T_Service_Compnies.AsNoTracking().Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
            .OrderBy(x => x.Service_Compny).Select(x => new ServiceLookupEntry(x.Service_Compny_ID, x.Service_Compny!)).ToListAsync();
    }
    public static async Task<string?> SaveAsync(Cont_maintContext db, ServiceScope scope, bool isType, int? id, string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 50) return "أدخل الاسم بما لا يتجاوز 50 حرفًا.";
        // يظل فحص تكرار الاسم والحفظ في المعاملة نفسها، دون تغيير المفاتيح عند التعديل.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (isType)
        {
            var query = db.T_Sevice_Compny_Types.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
            if (await query.AnyAsync(x => x.Sevice_Compny_Type.Trim() == name && (!id.HasValue || x.Sevice_Compny_Type_ID != id.Value))) return "نوع التعاقد مسجل من قبل.";
            var item = id.HasValue ? await query.SingleOrDefaultAsync(x => x.Sevice_Compny_Type_ID == id) : new T_Sevice_Compny_Type { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location };
            if (item == null) return "السجل لم يعد موجودًا؛ حدّث البيانات.";
            item.Sevice_Compny_Type = name;
            if (!id.HasValue) db.T_Sevice_Compny_Types.Add(item);
        }
        else
        {
            var query = db.T_Service_Compnies.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
            if (await query.AnyAsync(x => x.Service_Compny != null && x.Service_Compny.Trim() == name && (!id.HasValue || x.Service_Compny_ID != id.Value))) return "الشركة مسجلة من قبل.";
            var item = id.HasValue ? await query.SingleOrDefaultAsync(x => x.Service_Compny_ID == id) : new T_Service_Compny { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location };
            if (item == null) return "السجل لم يعد موجودًا؛ حدّث البيانات.";
            item.Service_Compny = name;
            if (!id.HasValue) db.T_Service_Compnies.Add(item);
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return null;
    }
    // تستعمل الواجهة هذا الفحص قبل عرض التأكيد، ويعيد الحذف الفحص داخل المعاملة.
    internal static async Task<string?> GetDeleteBlockReasonAsync(Cont_maintContext db, ServiceScope scope, bool isType, int id)
    {
        // يمنع حذف الشركة أو النوع المرتبط بعقود مملوكة أو مؤجرة، ضمن نطاق المفتاح نفسه.
        var owned = db.T_OwnedPremises_Serivecs.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
        var rented = db.T_RentalPremises_Serivecs.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
        if (await owned.AnyAsync(x => isType ? x.Sevice_Compny_Type_ID == id : x.Service_Compny_ID == id)
            || await rented.AnyAsync(x => isType ? x.Sevice_Compny_Type_ID == id : x.Service_Compny_ID == id))
            return isType
                ? "لا يمكن حذف نوع التعاقد لارتباطه بعقود الخدمات."
                : "لا يمكن حذف شركة الخدمات لارتباطها بعقود الخدمات.";
        return null;
    }

    public static async Task<string?> DeleteAsync(Cont_maintContext db, ServiceScope scope, bool isType, int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var reason = await GetDeleteBlockReasonAsync(db, scope, isType, id);
        if (reason is not null) return reason;
        if (isType)
        {
            var item = await db.T_Sevice_Compny_Types.SingleOrDefaultAsync(x => x.Sevice_Compny_Type_ID == id && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
            if (item != null) db.T_Sevice_Compny_Types.Remove(item);
        }
        else
        {
            var item = await db.T_Service_Compnies.SingleOrDefaultAsync(x => x.Service_Compny_ID == id && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
            if (item != null) db.T_Service_Compnies.Remove(item);
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return null;
    }
}
#endregion
