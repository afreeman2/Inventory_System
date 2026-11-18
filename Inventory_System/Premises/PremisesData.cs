using Inventory_System.dbContext;
using Inventory_System.DatabContext;
using Inventory_System.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Inventory_System.Premises;

internal sealed class PremisesData(Func<Cont_maintContext> createContext)
{
    #region بيانات الجهة وسجل المقرات
    // يفصل الموقع الإداري للمنظومة عن المقر المسجّل، ويصون الأجزاء الأربعة من مفتاحه.
    internal static Cont_maintContext CreateContext() => new(DbContextHelper.Options
        ?? throw new InvalidOperationException("تعذّر تهيئة اتصال قاعدة بيانات حصر المقرات."));

    private static async Task<T_Location> GetScopeAsync(Cont_maintContext db, CancellationToken token)
    {
        if (App.location is { } location)
            return location;

        // تتبع المنظومة الأصلية الفرع النشط وأول موقع إداري معرّف له عند غياب موقع الجلسة.
        return await (from site in db.T_Locations.AsNoTracking()
                      join branch in db.T_Branches on site.Branch_ID equals branch.Branch_ID
                      where branch.st == true
                      orderby branch.Branch_ID, site.Location_ID, site.Location
                      select site).FirstOrDefaultAsync(token)
            ?? throw new InvalidOperationException("لم يُعرّف موقع إداري للفرع النشط.");
    }

    private static IQueryable<T_Inventory_Location> Locations(Cont_maintContext db, T_Location scope, PremisesKind kind) =>
        db.T_Inventory_Locations.Where(p => p.types == (int)kind && p.Branch_ID == scope.Branch_ID
            && p.Location_ID == scope.Location_ID && p.Location == scope.Location);

    internal async Task<List<T_Inventory_Location>> LoadLocationsAsync(PremisesKind kind, CancellationToken token)
    {
        await using var db = createContext();
        var scope = await GetScopeAsync(db, token);
        return await Locations(db, scope, kind).AsNoTracking().OrderBy(p => p.In_Location_ID).ToListAsync(token);
    }

    internal async Task<bool> SaveLocationAsync(PremisesKind kind, T_Inventory_Location? original,
        string name, string address, CancellationToken token)
    {
        await using var db = createContext();
        var scope = await GetScopeAsync(db, token);
        // تمنع المعاملة إدراج اسم مكرر أثناء الحفظ المتزامن وتربط المقر بسجل الملكية أو الإيجار دفعة واحدة.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var locations = Locations(db, scope, kind);
        string normalizedName = NormalizeName(name);
        int? originalId = original?.In_Location_ID;
        var otherNames = await locations.Where(p => p.In_Location_ID != originalId)
            .Select(p => p.In_Location).ToListAsync(token);
        if (otherNames.Any(other => NormalizeName(other) == normalizedName))
            return false;

        var record = original is null
            ? new T_Inventory_Location
            {
                Branch_ID = scope.Branch_ID, Location_ID = scope.Location_ID, Location = scope.Location, types = (int)kind
            }
            : await locations.SingleOrDefaultAsync(p => p.In_Location_ID == original.In_Location_ID
                && p.Branch_ID == original.Branch_ID && p.Location_ID == original.Location_ID
                && p.Location == original.Location, token)
                ?? throw new InvalidOperationException("تعذّر العثور على المقر. حدّث القائمة ثم أعد المحاولة.");

        record.In_Location = name;
        record.Address = address;
        if (original is null)
        {
            db.T_Inventory_Locations.Add(record);
            await db.SaveChangesAsync(token);
            var today = DateOnly.FromDateTime(DateTime.Today);
            // رقم المقر المولّد يُخزّن كمرجع في الجدول الموافق لنوع الحصر.
            if (kind == PremisesKind.Owned)
                db.T_OwnedPremises.Add(new T_OwnedPremise
                {
                    Branch_ID = record.Branch_ID, Location_ID = record.Location_ID, Location = record.Location,
                    In_Location_ID = record.In_Location_ID, Date_Modify = today
                });
            else
                db.T_RentalPremises.Add(new T_RentalPremise
                {
                    Branch_ID = record.Branch_ID, Location_ID = record.Location_ID, Location = record.Location,
                    In_Location_ID = record.In_Location_ID, Date_Modify = today
                });
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }

    // تُقارن الصيغ العربية التي كان المصدر يعاملها كاسم واحد، مع إبقاء الكتابة الأصلية عند الحفظ.
    private static string NormalizeName(string name) =>
        name.Trim().Replace('أ', 'ا').Replace('إ', 'ا').Replace('ة', 'ه').Replace('ى', 'ي');
    #endregion

    #region تفاصيل الفواتير والعقود وأرقام الهواتف
    // تُمثّل كل فئة سجلاتها بالأعمدة المشتركة، ويُربط رقم المقر بالفرع والموقع لمنع خلط السجلات.
    internal sealed record Detail(string? Reference, string? Description, DateOnly? Date, decimal? Value);

    internal async Task<List<Detail>> LoadDetailsAsync(PremisesKind kind, PremisesRegister register,
        T_Inventory_Location selected, CancellationToken token)
    {
        await using var db = createContext();
        var scope = await GetScopeAsync(db, token);
        if (!await Locations(db, scope, kind).AnyAsync(p => p.In_Location_ID == selected.In_Location_ID
            && p.Branch_ID == selected.Branch_ID && p.Location_ID == selected.Location_ID && p.Location == selected.Location, token))
            return [];

        if (register == PremisesRegister.PhoneNumbers)
        {
            return await (from document in db.T_Phones_Buildings
                          join phone in db.T_Sub_PHones_Buildings
                              on new { Doc = document.ID, document.Branch_ID, document.Location_ID, document.Location }
                              equals new { Doc = phone.DOC_ID, phone.Branch_ID, phone.Location_ID, phone.Location }
                          where document.Build_ID == selected.In_Location_ID && document.types == (int)kind
                              && document.Branch_ID == scope.Branch_ID && document.Location_ID == scope.Location_ID
                              && document.Location == scope.Location
                          orderby document.Doc_Date descending, phone.ID
                          select new Detail(phone.Phone, phone.nameuser, document.Doc_Date, null)).ToListAsync(token);
        }

        var premiseIds = kind == PremisesKind.Owned
            ? db.T_OwnedPremises.Where(p => p.In_Location_ID == selected.In_Location_ID
                && p.Branch_ID == scope.Branch_ID && p.Location_ID == scope.Location_ID && p.Location == scope.Location)
                .Select(p => p.RentalPremises_ID)
            : db.T_RentalPremises.Where(p => p.In_Location_ID == selected.In_Location_ID
                && p.Branch_ID == scope.Branch_ID && p.Location_ID == scope.Location_ID && p.Location == scope.Location)
                .Select(p => p.RentalPremises_ID);

        var rows = (kind, register) switch
        {
            (PremisesKind.Owned, PremisesRegister.Power) => Records(db.T_OwnedPremises_Powers)
                .Select(p => new Detail(p.Power_inv, p.R_power.ToString(), p.Date_Input, p.R_powerVal)),
            (PremisesKind.Rented, PremisesRegister.Power) => Records(db.T_RentalPremises_Powers)
                .Select(p => new Detail(p.Power_inv, p.R_power.ToString(), p.Date_Input, p.R_powerVal)),
            (PremisesKind.Owned, PremisesRegister.Water) => Records(db.T_OwnedPremises_Waters)
                .Select(p => new Detail(p.Water_inv, p.R_Water.ToString(), p.Date_Input, p.R_WaterVal)),
            (PremisesKind.Rented, PremisesRegister.Water) => Records(db.T_RentalPremises_Waters)
                .Select(p => new Detail(p.Water_inv, p.R_Water.ToString(), p.Date_Input, p.R_WaterVal)),
            (PremisesKind.Owned, PremisesRegister.PhoneBills) => Records(db.T_OwnedPremises_Phones)
                .Select(p => new Detail(p.phone_inv, p.V_Phone, p.Date_Input, p.Phone_Value)),
            (PremisesKind.Rented, PremisesRegister.PhoneBills) => Records(db.T_RentalPremises_Phones)
                .Select(p => new Detail(p.phone_inv, p.V_Phone, p.Date_Input, p.Phone_Value)),
            (PremisesKind.Owned, PremisesRegister.Insurance) => Records(db.T_OwnedPremises_Insucs)
                .Select(p => new Detail(p.INS_TYPE_ID.ToString(), p.insu_Compny_ID.ToString(), p.Date_Input, p.INS_Value)),
            (PremisesKind.Rented, PremisesRegister.Insurance) => Records(db.T_RentalPremises_Insucs)
                .Select(p => new Detail(p.INS_TYPE_ID.ToString(), p.insu_Compny_ID.ToString(), p.Date_Input, p.INS_Value)),
            (PremisesKind.Owned, PremisesRegister.Services) => Records(db.T_OwnedPremises_Serivecs)
                .Select(p => new Detail(p.Contrect_ID, p.Service_Compny_ID.ToString(), p.Date_Input, p.INS_Value)),
            (PremisesKind.Rented, PremisesRegister.Services) => Records(db.T_RentalPremises_Serivecs)
                .Select(p => new Detail(p.Contrect_ID, p.Service_Compny_ID.ToString(), p.Date_Input, p.INS_Value)),
            (PremisesKind.Owned, PremisesRegister.Contracts) => Records(db.T_OwnedPremises_Contrects)
                .Select(p => new Detail(p.ID.ToString(), p.Cont_data, p.Cont_date, null)),
            (PremisesKind.Rented, PremisesRegister.Contracts) => Records(db.T_RentalPremises_Contrects)
                .Select(p => new Detail(p.ID.ToString(), p.Cont_data, p.Cont_date, null)),
            _ => throw new ArgumentOutOfRangeException(nameof(register))
        };
        // يأخذ جدول العرض السجلات مرتبة تنازليًا بحسب معرّفها.
        return await rows.ToListAsync(token);

        IQueryable<T> Records<T>(DbSet<T> set) where T : class => set.AsNoTracking().Where(p =>
            EF.Property<int>(p, "Branch_ID") == scope.Branch_ID
            && EF.Property<int>(p, "Location_ID") == scope.Location_ID
            && EF.Property<string>(p, "Location") == scope.Location
            && premiseIds.Contains(EF.Property<long>(p, "RentalPremises_ID"))).OrderByDescending(p => EF.Property<long>(p, "ID"));
    }
    #endregion

    #region إحصاءات المقرات السنوية
    internal async Task<(int? Owned, int? Rented)> CountAsync(int year, bool owned, bool rented, CancellationToken token)
    {
        await using var db = createContext();
        var site = await GetScopeAsync(db, token);
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        int? ownedCount = owned ? await db.T_OwnedPremises.CountAsync(p => p.Branch_ID == site.Branch_ID
            && p.Location_ID == site.Location_ID && p.Location == site.Location && p.In_Location_ID != null
            && p.Date_Modify >= from && p.Date_Modify <= to, token) : null;
        int? rentedCount = rented ? await db.T_RentalPremises.CountAsync(p => p.Branch_ID == site.Branch_ID
            && p.Location_ID == site.Location_ID && p.Location == site.Location
            && p.Date_Modify >= from && p.Date_Modify <= to, token) : null;
        return (ownedCount, rentedCount);
    }
    #endregion
}
