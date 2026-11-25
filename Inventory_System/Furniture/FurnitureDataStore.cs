using System.Data;
using System.Globalization;
using Inventory_System.dbContext;
using Inventory_System.Models;
using Inventory_System.support_Data;
using Microsoft.EntityFrameworkCore;

namespace Inventory_System.Furniture;

// يضم قراءات وحفظ الأثاث ضمن الفرع والموقع الحاليين كما يفعل المشروع في بقية شاشاته.
internal sealed class FurnitureDataStore(Func<Cont_maintContext> createContext, ServiceScope scope)
{
    #region قراءة المقرات وقوائم الإدخال
    public async Task<List<FurnitureLocationChoice>> LoadLocationsAsync(CancellationToken token = default)
    {
        await using var db = createContext();
        return await db.T_Inventory_Locations.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
            .OrderBy(x => x.In_Location)
            .Select(x => new FurnitureLocationChoice(x.In_Location_ID, x.In_Location))
            .ToListAsync(token);
    }

    public async Task<List<T_Manage>> LoadManagementsAsync(CancellationToken token = default)
    {
        await using var db = createContext();
        return await db.T_Manages.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
            .OrderBy(x => x.Manag).ToListAsync(token);
    }

    public async Task<List<T_Section>> LoadSectionsAsync(short managementId, CancellationToken token = default)
    {
        await using var db = createContext();
        return await db.T_Sections.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId
                && x.Location == scope.Location && x.Manag_ID == managementId)
            .OrderBy(x => x.Secti).ToListAsync(token);
    }

    public async Task<List<FurnitureClassChoice>> LoadClassesAsync(CancellationToken token = default)
    {
        await using var db = createContext();
        return await db.T_furniture_Classes.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
            .OrderBy(x => x.Class_name)
            .Select(x => new FurnitureClassChoice(x.Class_id, x.Class_name, x.class_value))
            .ToListAsync(token);
    }

    public async Task<List<FurnitureTypeChoice>> LoadTypesAsync(CancellationToken token = default)
    {
        await using var db = createContext();
        return await db.T_Furniture_Types.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
            .OrderBy(x => x.Furniture_Type)
            .Select(x => new FurnitureTypeChoice(x.Furniture_Type_ID, x.Furniture_Type, x.barcode,
                x.Class_id, x.T_furniture_Class.Class_name))
            .ToListAsync(token);
    }

    public async Task<List<FurnitureCatalogRow>> LoadCatalogAsync(bool types, CancellationToken token = default)
    {
        await using var db = createContext();
        if (types)
            return await db.T_Furniture_Types.AsNoTracking()
                .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
                .OrderBy(x => x.Furniture_Type)
                .Select(x => new FurnitureCatalogRow(x.Class_id, x.Furniture_Type_ID, x.Furniture_Type,
                    x.barcode ?? "", x.T_furniture_Class.Class_name, 0)).ToListAsync(token);

        return await db.T_furniture_Classes.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
            .OrderBy(x => x.Class_name)
            .Select(x => new FurnitureCatalogRow(x.Class_id, 0, x.Class_name, "", "", x.class_value))
            .ToListAsync(token);
    }
    #endregion

    #region قراءة سجلات الحصر وتفاصيلها
    public async Task<List<FurnitureRecordRow>> SearchAsync(int buildingId, DateOnly? from, DateOnly? to,
        CancellationToken token = default)
    {
        await using var db = createContext();
        var query = db.T_Furnitures.AsNoTracking().Where(x => x.Branch_ID == scope.BranchId
            && x.Location_ID == scope.LocationId && x.Location == scope.Location && x.Build_ID == buildingId);
        if (from.HasValue) query = query.Where(x => x.doc_date >= from.Value);
        if (to.HasValue) query = query.Where(x => x.doc_date <= to.Value);

        var headers = await query.OrderByDescending(x => x.doc_date).ThenByDescending(x => x.Furniture_ID)
            .Select(x => new { x.Furniture_ID, x.Build_ID, x.doc_date, x.DataDesc })
            .ToListAsync(token);
        if (headers.Count == 0) return [];

        var ids = headers.Select(x => x.Furniture_ID).ToArray();
        // نحمّل تفاصيل السجلات المختارة مرة واحدة كي تظهر في الجدول الرئيسي دون استعلام لكل رأس.
        var detailRows = await db.T_Furniture_Lists.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId
                && x.Location == scope.Location && ids.Contains(x.Furniture_ID))
            .OrderBy(x => x.Manag_ID).ThenBy(x => x.Secti_ID).ThenBy(x => x.ID)
            .Select(x => new FurnitureDetailRow(x.Furniture_ID, x.ID, x.Furniture_Type_ID, x.Manag_ID, x.Secti_ID,
                x.T_Furniture_Type.Furniture_Type, x.T_Manage.Manag, x.T_Section.Secti, x.CountNo, x.notes ?? ""))
            .ToListAsync(token);
        var detailsByHeader = detailRows.GroupBy(x => x.RecordId).ToDictionary(group => group.Key, group => group.ToList());
        var buildingIds = headers.Where(x => x.Build_ID.HasValue).Select(x => x.Build_ID!.Value).Distinct().ToArray();
        var names = await db.T_Inventory_Locations.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId
                && x.Location == scope.Location && buildingIds.Contains(x.In_Location_ID))
            .ToDictionaryAsync(x => x.In_Location_ID, x => x.In_Location, token);
        var culture = CultureInfo.GetCultureInfo("ar-LY");
        return headers.Select(x =>
        {
            detailsByHeader.TryGetValue(x.Furniture_ID, out var details);
            details ??= [];
            var date = x.doc_date;
            return new FurnitureRecordRow(x.Furniture_ID, x.Build_ID,
                x.Build_ID is int id && names.TryGetValue(id, out var name) ? name : "مقر غير متاح",
                date, date?.ToString("dd/MM/yyyy", culture) ?? "", x.DataDesc ?? "", details.Count,
                details.Sum(detail => detail.Count)) { Details = details };
        }).ToList();
    }

    public async Task<(FurnitureRecordRow Header, List<FurnitureDetailRow> Details)?> LoadRecordAsync(long id,
        CancellationToken token = default)
    {
        await using var db = createContext();
        var header = await db.T_Furnitures.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId
                && x.Location == scope.Location && x.Furniture_ID == id)
            .Select(x => new { x.Furniture_ID, x.Build_ID, x.doc_date, x.DataDesc })
            .SingleOrDefaultAsync(token);
        if (header is null) return null;
        var details = await db.T_Furniture_Lists.AsNoTracking()
            .Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId
                && x.Location == scope.Location && x.Furniture_ID == id)
            .OrderBy(x => x.ID)
            .Select(x => new FurnitureDetailRow(x.Furniture_ID, x.ID, x.Furniture_Type_ID, x.Manag_ID, x.Secti_ID,
                x.T_Furniture_Type.Furniture_Type, x.T_Manage.Manag, x.T_Section.Secti, x.CountNo, x.notes ?? ""))
            .ToListAsync(token);
        string building = header.Build_ID.HasValue
            ? await db.T_Inventory_Locations.AsNoTracking().Where(x => x.In_Location_ID == header.Build_ID
                && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location)
                .Select(x => x.In_Location).SingleOrDefaultAsync(token) ?? "مقر غير متاح"
            : "مقر غير متاح";
        var culture = CultureInfo.GetCultureInfo("ar-LY");
        var date = header.doc_date;
        return (new FurnitureRecordRow(header.Furniture_ID, header.Build_ID, building, date,
            date?.ToString("dd/MM/yyyy", culture) ?? "", header.DataDesc ?? "", details.Count, details.Sum(x => x.Count)), details);
    }
    #endregion

    #region إضافة وتعديل فئات الأثاث وأنواعه
    public async Task SaveClassAsync(int? id, string name, int percentage, CancellationToken token = default)
    {
        name = name.Trim();
        if (name.Length is 0 or > 50) throw new InvalidOperationException("أدخل اسم الفئة بما لا يتجاوز 50 حرفًا.");
        if (percentage is < 0 or > 50) throw new InvalidOperationException("نسبة الاستهلاك يجب أن تكون بين 0 و50.");
        await using var db = createContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var rows = await db.T_furniture_Classes.Where(x => x.Branch_ID == scope.BranchId
            && x.Location_ID == scope.LocationId && x.Location == scope.Location).ToListAsync(token);
        if (rows.Any(x => x.Class_id != id && NormalizeName(x.Class_name) == NormalizeName(name)))
            throw new InvalidOperationException("اسم الفئة مسجل مسبقًا.");
        var row = id.HasValue ? rows.SingleOrDefault(x => x.Class_id == id.Value)
            ?? throw new InvalidOperationException("الفئة غير موجودة؛ حدّث القائمة ثم أعد المحاولة.")
            : new T_furniture_Class { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location };
        row.Class_name = name;
        row.class_value = percentage;
        if (!id.HasValue) db.T_furniture_Classes.Add(row);
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
    }

    // تُقرأ الارتباطات قبل عرض التأكيد دون فتح معاملة أثناء انتظار رد المستخدم.
    internal async Task<string?> GetClassDeleteBlockReasonAsync(int id, CancellationToken token = default)
    {
        await using var db = createContext();
        return await GetClassDeleteBlockReasonAsync(db, id, token);
    }

    private async Task<string?> GetClassDeleteBlockReasonAsync(Cont_maintContext db, int id, CancellationToken token) =>
        await db.T_Furniture_Types.AnyAsync(x => x.Class_id == id && x.Branch_ID == scope.BranchId
            && x.Location_ID == scope.LocationId && x.Location == scope.Location, token)
            ? "لا يمكن حذف فئة الأثاث لارتباطها بأنواع أثاث."
            : null;

    public async Task DeleteClassAsync(int id, CancellationToken token = default)
    {
        await using var db = createContext();
        // يبقى فحص ارتباط الفئة بالأنواع والحذف داخل معاملة واحدة.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var row = await db.T_furniture_Classes.SingleOrDefaultAsync(x => x.Class_id == id
            && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location, token)
            ?? throw new InvalidOperationException("الفئة غير موجودة؛ حدّث القائمة.");
        var reason = await GetClassDeleteBlockReasonAsync(db, id, token);
        if (reason is not null) throw new InvalidOperationException(reason);
        db.T_furniture_Classes.Remove(row);
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
    }

    public async Task SaveTypeAsync(int? id, string name, string barcode, int classId, CancellationToken token = default)
    {
        name = name.Trim(); barcode = barcode.Trim();
        if (name.Length is 0 or > 50) throw new InvalidOperationException("أدخل اسم النوع بما لا يتجاوز 50 حرفًا.");
        if (barcode.Length is 0 or > 200) throw new InvalidOperationException("أدخل رقم باركود صحيحًا.");
        await using var db = createContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var classes = db.T_furniture_Classes.AsNoTracking().Where(x => x.Class_id == classId
            && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
        if (!await classes.AnyAsync(token)) throw new InvalidOperationException("اختر فئة أثاث من موقع الدخول الحالي.");
        var rows = await db.T_Furniture_Types.Where(x => x.Branch_ID == scope.BranchId
            && x.Location_ID == scope.LocationId && x.Location == scope.Location).ToListAsync(token);
        if (rows.Any(x => x.Furniture_Type_ID != id && NormalizeName(x.Furniture_Type) == NormalizeName(name)))
            throw new InvalidOperationException("اسم نوع الأثاث مسجل مسبقًا.");
        if (rows.Any(x => x.Furniture_Type_ID != id && string.Equals(x.barcode?.Trim(), barcode, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("رقم الباركود مسجل لنوع آخر.");
        var row = id.HasValue ? rows.SingleOrDefault(x => x.Furniture_Type_ID == id.Value)
            ?? throw new InvalidOperationException("نوع الأثاث غير موجود؛ حدّث القائمة ثم أعد المحاولة.")
            : new T_Furniture_Type { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location };
        row.Furniture_Type = name;
        row.barcode = barcode;
        row.Class_id = classId;
        row.Date_Modify = DateOnly.FromDateTime(DateTime.Today);
        if (!id.HasValue) db.T_Furniture_Types.Add(row);
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
    }

    // تُقرأ الارتباطات قبل عرض التأكيد دون فتح معاملة أثناء انتظار رد المستخدم.
    internal async Task<string?> GetTypeDeleteBlockReasonAsync(int id, CancellationToken token = default)
    {
        await using var db = createContext();
        return await GetTypeDeleteBlockReasonAsync(db, id, token);
    }

    private async Task<string?> GetTypeDeleteBlockReasonAsync(Cont_maintContext db, int id, CancellationToken token) =>
        await db.T_Furniture_Lists.AnyAsync(x => x.Furniture_Type_ID == id && x.Branch_ID == scope.BranchId
            && x.Location_ID == scope.LocationId && x.Location == scope.Location, token)
            ? "لا يمكن حذف نوع الأثاث لارتباطه ببيانات حصر الأثاث."
            : null;

    public async Task DeleteTypeAsync(int id, CancellationToken token = default)
    {
        await using var db = createContext();
        // نفحص ارتباط النوع ببنود الحصر ضمن معاملة الحذف ذاتها.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var row = await db.T_Furniture_Types.SingleOrDefaultAsync(x => x.Furniture_Type_ID == id
            && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location, token)
            ?? throw new InvalidOperationException("نوع الأثاث غير موجود؛ حدّث القائمة.");
        var reason = await GetTypeDeleteBlockReasonAsync(db, id, token);
        if (reason is not null) throw new InvalidOperationException(reason);
        db.T_Furniture_Types.Remove(row);
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
    }
    #endregion

    #region حفظ سجل الحصر وحماية نطاقه
    public async Task<long> SaveRecordAsync(FurnitureRecordDraft draft, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(draft.Description)) throw new InvalidOperationException("أدخل بيان الحصر.");
        if (draft.Description.Trim().Length > 250) throw new InvalidOperationException("بيان الحصر لا يتجاوز 250 حرفًا.");
        if (draft.Details.Count == 0) throw new InvalidOperationException("أضف صنفًا واحدًا على الأقل إلى الحصر.");
        if (draft.Details.Any(x => x.Count <= 0)) throw new InvalidOperationException("يجب أن تكون كمية كل صنف أكبر من صفر.");
        if (draft.Details.GroupBy(x => (x.TypeId, x.ManagementId, x.SectionId)).Any(x => x.Count() > 1))
            throw new InvalidOperationException("يوجد صنف مكرر للإدارة والقسم نفسيهما؛ اجمع كميته في سطر واحد.");

        await using var db = createContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        if (!await db.T_Inventory_Locations.AnyAsync(x => x.In_Location_ID == draft.BuildingId
            && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location, token))
            throw new InvalidOperationException("اختر مقرًا تابعًا لموقع الدخول الحالي.");
        if (await db.T_Furnitures.AnyAsync(x => x.Build_ID == draft.BuildingId && x.doc_date == draft.Date
            && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location
            && x.Furniture_ID != draft.Id, token))
            throw new InvalidOperationException("يوجد حصر مسجل لهذا المقر في التاريخ نفسه.");

        foreach (var detail in draft.Details)
        {
            bool typeExists = await db.T_Furniture_Types.AnyAsync(x => x.Furniture_Type_ID == detail.TypeId
                && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location, token);
            bool managerExists = await db.T_Manages.AnyAsync(x => x.Manag_ID == detail.ManagementId
                && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location, token);
            bool sectionExists = await db.T_Sections.AnyAsync(x => x.Secti_ID == detail.SectionId
                && x.Manag_ID == detail.ManagementId && x.Branch_ID == scope.BranchId
                && x.Location_ID == scope.LocationId && x.Location == scope.Location, token);
            if (!typeExists || !managerExists || !sectionExists)
                throw new InvalidOperationException("تغير نوع الأثاث أو الإدارة أو القسم. حدّث الخيارات ثم أعد المحاولة.");
        }

        var header = draft.Id.HasValue
            ? await db.T_Furnitures.Include(x => x.T_Furniture_Lists).SingleOrDefaultAsync(x => x.Furniture_ID == draft.Id
                && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location, token)
                ?? throw new InvalidOperationException("سجل الحصر غير موجود؛ حدّث القائمة.")
            : new T_Furniture { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location };
        if (!draft.Id.HasValue)
            header.Furniture_ID = (await db.T_Furnitures.Where(x => x.Branch_ID == scope.BranchId
                && x.Location_ID == scope.LocationId && x.Location == scope.Location)
                .Select(x => (long?)x.Furniture_ID).MaxAsync(token) ?? 0) + 1;
        header.Build_ID = draft.BuildingId;
        header.doc_date = draft.Date;
        header.DataDesc = draft.Description.Trim();
        header.Date_Modify = DateOnly.FromDateTime(DateTime.Today);
        if (!draft.Id.HasValue) db.T_Furnitures.Add(header);

        var existingById = header.T_Furniture_Lists.ToDictionary(x => x.ID);
        var retained = new HashSet<long>();
        foreach (var item in draft.Details)
        {
            T_Furniture_List row;
            if (item.Id is long existingId)
            {
                if (!existingById.TryGetValue(existingId, out row!))
                    throw new InvalidOperationException("أحد أسطر الحصر لم يعد تابعًا لهذا السجل.");
                retained.Add(existingId);
            }
            else
            {
                row = new T_Furniture_List { T_Furniture = header, Branch_ID = scope.BranchId,
                    Location_ID = scope.LocationId, Location = scope.Location };
                header.T_Furniture_Lists.Add(row);
            }
            row.Furniture_Type_ID = item.TypeId;
            row.Manag_ID = item.ManagementId;
            row.Secti_ID = item.SectionId;
            row.CountNo = item.Count;
            row.notes = item.Notes.Trim();
            row.Date_Modify = DateOnly.FromDateTime(DateTime.Today);
        }
        foreach (var old in existingById.Values.Where(x => !retained.Contains(x.ID)))
            db.T_Furniture_Lists.Remove(old);

        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        return header.Furniture_ID;
    }

    public async Task DeleteRecordAsync(long id, CancellationToken token = default)
    {
        await using var db = createContext();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var header = await db.T_Furnitures.Include(x => x.T_Furniture_Lists).SingleOrDefaultAsync(x => x.Furniture_ID == id
            && x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location, token)
            ?? throw new InvalidOperationException("سجل الحصر غير موجود؛ حدّث القائمة.");
        db.T_Furniture_Lists.RemoveRange(header.T_Furniture_Lists);
        db.T_Furnitures.Remove(header);
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
    }
    #endregion

    #region تطبيع الأسماء العربية لمنع التكرار بصيغ الحروف المختلفة
    internal static string NormalizeName(string? value) => (value ?? "").Trim()
        .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا')
        .Replace('ة', 'ه').Replace('ى', 'ي');
    #endregion
}
