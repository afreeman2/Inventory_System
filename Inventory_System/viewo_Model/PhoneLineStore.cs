using Inventory_System.dbContext;
using Inventory_System.Models;
using Inventory_System.support_Data;
using Microsoft.EntityFrameworkCore;

namespace Inventory_System.viewo_Model;

#region بيانات خطوط الهاتف ومفاتيحها المستقلة عن تحديد الجدول
public sealed record PhoneLineKey(long Id, long DocumentId, int BuildingId, ServiceScope Scope);
public sealed record PhoneLineRow(PhoneLineKey Key, string Building, string Phone, int ManagementId,
    string Management, short SectionId, string Section, string? UserName, bool InService, DateOnly DocumentDate);
public sealed record PhoneLineDraft(int BuildingId, string Phone, short ManagementId, short SectionId, string UserName);
public sealed record PhoneLineResult(PhoneLineKey? Key, string? Error = null, string Field = "Phone");
#endregion

public static class PhoneLineStore
{
    #region قراءة المقرات وخطوطها مع أسماء الإدارات والأقسام
    public static IQueryable<T_Inventory_Location> Buildings(Cont_maintContext db, ServiceScope scope) => db.T_Inventory_Locations
        .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location && x.types == 1);

    public static async Task<List<PhoneLineRow>> LoadAsync(Cont_maintContext db, ServiceScope scope)
    {
        // تُقرأ الأسماء بنطاقها الكامل؛ رقم الإدارة أو القسم وحده قد يتكرر في فرع آخر.
        var managers = OrganizationLookupStore.Managers(db, scope);
        var sections = OrganizationLookupStore.Sections(db, scope);
        var query = from line in db.T_Sub_PHones_Buildings.AsNoTracking()
                    join doc in db.T_Phones_Buildings on new { Id = line.DOC_ID, line.Branch_ID, line.Location_ID, line.Location }
                        equals new { Id = doc.ID, doc.Branch_ID, doc.Location_ID, doc.Location }
                    join building in Buildings(db, scope) on new { Id = doc.Build_ID, doc.Branch_ID, doc.Location_ID, doc.Location }
                        equals new { Id = building.In_Location_ID, building.Branch_ID, building.Location_ID, building.Location }
                    where doc.types == 1 || doc.types == null
                    orderby building.In_Location, line.Phone
                    select new PhoneLineRow(new PhoneLineKey(line.ID, doc.ID, doc.Build_ID, scope), building.In_Location,
                        line.Phone, line.Mangment_ID,
                        managers.Where(x => x.Manag_ID == line.Mangment_ID).Select(x => x.Manag).FirstOrDefault() ?? "غير محدد",
                        line.Secti_ID,
                        sections.Where(x => x.Secti_ID == line.Secti_ID && x.Manag_ID == line.Mangment_ID).Select(x => x.Secti).FirstOrDefault() ?? "غير محدد",
                        line.nameuser, line.IN_Service, doc.Doc_Date);
        return await query.ToListAsync();
    }
    #endregion

    #region حفظ خط جديد أو تعديل الخط الأصلي دون تغيير هويته
    public static async Task<PhoneLineResult> SaveAsync(Cont_maintContext db, ServiceScope scope, PhoneLineKey? key, PhoneLineDraft draft)
    {
        var phone = draft.Phone.Trim(); var name = draft.UserName.Trim();
        if (phone.Length is 0 or > 50) return new(null, "أدخل رقم الهاتف بما لا يتجاوز 50 حرفًا.");
        if (name.Length > 250) return new(null, "اسم المستخدم يجب ألا يتجاوز 250 حرفًا.", "User");
        if (key != null && (key.Scope != scope || key.BuildingId != draft.BuildingId))
            return new(null, "تغير نطاق السجل؛ أغلق المسودة وأعد تحميله.", "Building");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (!await Buildings(db, scope).AnyAsync(x => x.In_Location_ID == draft.BuildingId))
            return new(null, "اختر مقرًا مملوكًا من القائمة الحالية.", "Building");
        if (!await OrganizationLookupStore.Managers(db, scope).AnyAsync(x => x.Manag_ID == draft.ManagementId))
            return new(null, "اختر الإدارة أو القسم من القائمة الحالية.", "Management");
        if (!await OrganizationLookupStore.Sections(db, scope).AnyAsync(x => x.Secti_ID == draft.SectionId && x.Manag_ID == draft.ManagementId))
            return new(null, "اختر قسمًا أو وحدة يتبع الإدارة المختارة.", "Section");
        var lines = db.T_Sub_PHones_Buildings.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
        var excludedId = key?.Id ?? long.MinValue;
        if (await lines.AnyAsync(x => x.Phone.Trim() == phone && x.T_Phones_Building.Build_ID == draft.BuildingId && x.ID != excludedId))
            return new(null, "رقم الهاتف مدرج مسبقًا لهذا المقر.");
        T_Sub_PHones_Building line;
        if (key is null)
        {
            // نجمع خطوط المبنى في مستنده الحالي، وننشئ المستند عند إضافة أول رقم فقط.
            var doc = await db.T_Phones_Buildings.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId
                && x.Location == scope.Location && x.Build_ID == draft.BuildingId && (x.types == 1 || x.types == null)).OrderBy(x => x.ID).FirstOrDefaultAsync();
            doc ??= new T_Phones_Building { Build_ID = draft.BuildingId, Branch_ID = scope.BranchId,
                Location_ID = scope.LocationId, Location = scope.Location, types = 1, Doc_Date = DateOnly.FromDateTime(DateTime.Today) };
            line = new T_Sub_PHones_Building { Branch_ID = scope.BranchId, Location_ID = scope.LocationId,
                Location = scope.Location, T_Phones_Building = doc, IN_Service = true };
            db.T_Sub_PHones_Buildings.Add(line);
        }
        else
        {
            var original = await lines.SingleOrDefaultAsync(x => x.ID == key.Id && x.DOC_ID == key.DocumentId && x.T_Phones_Building.Build_ID == key.BuildingId);
            if (original is null) return new(null, "الخط لم يعد موجودًا؛ حدّث القائمة.");
            line = original;
        }
        // الرقم نص للحفاظ على الصفر الأول؛ التعديل يبقي حالة الخدمة والمستند والمفتاح الأصليين.
        line.Phone = phone; line.Mangment_ID = draft.ManagementId; line.Secti_ID = draft.SectionId; line.nameuser = name;
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return new(new PhoneLineKey(line.ID, line.DOC_ID, draft.BuildingId, scope));
    }
    #endregion

    #region حذف الخط المحدد بمفتاحه الكامل
    public static async Task DeleteAsync(Cont_maintContext db, PhoneLineKey key)
    {
        var scope = key.Scope;
        var line = await db.T_Sub_PHones_Buildings.SingleOrDefaultAsync(x => x.ID == key.Id && x.DOC_ID == key.DocumentId
            && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location
            && x.T_Phones_Building.Build_ID == key.BuildingId && x.T_Phones_Building.T_Inventory_Location.types == 1);
        if (line is null) return;
        db.T_Sub_PHones_Buildings.Remove(line); await db.SaveChangesAsync();
    }
    #endregion
}
