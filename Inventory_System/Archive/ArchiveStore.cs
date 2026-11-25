using Inventory_System.dbContext;
using Inventory_System.Models;
using Inventory_System.support_Data;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Inventory_System.Archive;

public sealed class ArchiveStore(Cont_maintContext db, ServiceScope scope, int type)
{
    #region عزل نوع المبنى ونطاق الموقع في كل عملية
    private bool Owned => type == 1;
    private IQueryable<T_OwnedPremise> OwnedBuildings => db.T_OwnedPremises.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
    private IQueryable<T_RentalPremise> RentedBuildings => db.T_RentalPremises.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
    private IQueryable<T_OwnedPremises_Document> OwnedAlbums => db.T_OwnedPremises_Documents.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);
    private IQueryable<T_RentalPremises_Document> RentedAlbums => db.T_RentalPremises_Documents.Where(x => x.Branch_ID == scope.BranchId && x.Location_ID == scope.LocationId && x.Location == scope.Location);

    private async Task RequireBuilding(long building)
    {
        if (type is not (1 or 2) || !(Owned ? await OwnedBuildings.AnyAsync(x => x.RentalPremises_ID == building) : await RentedBuildings.AnyAsync(x => x.RentalPremises_ID == building)))
            throw new InvalidOperationException("المبنى غير موجود في موقع الدخول الحالي.");
    }

    private async Task RequireAlbum(long building, long album)
    {
        await RequireBuilding(building);
        if (!(Owned ? await OwnedAlbums.AnyAsync(x => x.Document_ID == album && x.RentalPremises_ID == building) : await RentedAlbums.AnyAsync(x => x.Document_ID == album && x.RentalPremises_ID == building)))
            throw new InvalidOperationException("الألبوم لم يعد موجودًا في المبنى المحدد؛ حدّث القائمة.");
    }
    #endregion

    #region قراءة بطاقات المباني والألبومات دون تحميل الملفات إلى الذاكرة
    public async Task<List<ArchiveCard>> BuildingsAsync()
    {
        // عند فتح الأرشيف نقرأ حقول المقرات فقط لتظهر القائمة قبل طلب الألبومات.
        var buildings = Owned
            ? await OwnedBuildings.Select(x => new { Id = x.RentalPremises_ID, LookupId = x.In_Location_ID, Name = x.T_Inventory_Location.In_Location, x.Address }).ToListAsync()
            : await RentedBuildings.Select(x => new { Id = x.RentalPremises_ID, LookupId = (int?)x.In_Location_ID, Name = x.build_name, x.Address }).ToListAsync();
        return buildings.Select(x => new ArchiveCard { Id = x.Id, LookupId = x.LookupId, Title = string.IsNullOrWhiteSpace(x.Name) ? x.Address : x.Name,
            Subject = x.Address ?? "", Icon = MahApps.Metro.IconPacks.PackIconMaterialKind.OfficeBuildingOutline }).OrderBy(x => x.Title).ToList();
    }

    public async Task<List<ArchiveCard>> AlbumsAsync(long building)
    {
        await RequireBuilding(building);
        // نقرأ ألبومات المقر المحدد وعدد مستنداتها فقط، ويُحسب المرفق القديم ضمن العدد.
        var data = Owned
            ? await OwnedAlbums.Where(x => x.RentalPremises_ID == building).Select(x => new {
                Id = x.Document_ID, Building = x.RentalPremises_ID, Title = x.Tit, Date = x.doc_date, Icon = x.AlbumIcon,
                Count = db.T_Sub_Docs.Count(d => d.Doc_ID == x.Document_ID) + (x.Documents != null && x.Documents.Length > 0 ? 1 : 0) }).ToListAsync()
            : await RentedAlbums.Where(x => x.RentalPremises_ID == building).Select(x => new {
                Id = x.Document_ID, Building = x.RentalPremises_ID, Title = x.Tit, Date = x.doc_date, Icon = x.AlbumIcon,
                Count = db.T_Sub_Doc_rentals.Count(d => d.Doc_ID == x.Document_ID) + (x.Documents != null && x.Documents.Length > 0 ? 1 : 0) }).ToListAsync();
        return data.Select(x => new ArchiveCard { Id = x.Id, ParentId = x.Building, Title = x.Title ?? $"ألبوم {x.Id}",
            Date = x.Date, Icon = ArchiveIcons.Parse(x.Icon, true), Count = x.Count }).OrderByDescending(x => x.Date).ThenBy(x => x.Title).ToList();
    }

    public async Task<List<ArchiveCard>> DocumentsAsync(long building, long album)
    {
        await RequireAlbum(building, album);
        var data = Owned
            ? await db.T_Sub_Docs.Where(x => x.Doc_ID == album).OrderBy(x => x.page_NO).ThenBy(x => x.id).Select(x => new { Id = x.id, Title = x.DocumentTitle, Date = x.DocumentDate, Subject = x.DocumentSubject, Icon = x.DocumentIcon, Page = x.page_NO }).ToListAsync()
            : await db.T_Sub_Doc_rentals.Where(x => x.Doc_ID == album).OrderBy(x => x.page_NO).ThenBy(x => x.id).Select(x => new { Id = x.id, Title = x.DocumentTitle, Date = x.DocumentDate, Subject = x.DocumentSubject, Icon = x.DocumentIcon, Page = x.page_NO }).ToListAsync();
        var cards = data.Select(x => new ArchiveCard { Id = x.Id, ParentId = album, Title = x.Title ?? $"مستند {x.Page}", Date = x.Date,
            Subject = x.Subject ?? "", Icon = ArchiveIcons.Parse(x.Icon) }).ToList();
        var legacy = Owned
            ? await OwnedAlbums.Where(x => x.Document_ID == album).Select(x => new { Has = x.Documents != null && x.Documents.Length > 0, x.doc_info, x.doc_date }).SingleAsync()
            : await RentedAlbums.Where(x => x.Document_ID == album).Select(x => new { Has = x.Documents != null && x.Documents.Length > 0, x.doc_info, x.doc_date }).SingleAsync();
        if (legacy.Has) cards.Insert(0, new ArchiveCard { Id = 0, ParentId = album, Legacy = true, Title = "المستند الأصلي للألبوم", Date = legacy.doc_date, Subject = legacy.doc_info ?? "" });
        return cards;
    }
    #endregion

    #region حفظ الألبوم وحذف محتواه كوحدة ذرية
    public async Task<long> SaveAlbumAsync(long building, long? id, string title, DateOnly date, string icon)
    {
        title = title.Trim();
        if (title.Length is 0 or > 250) throw new InvalidOperationException("أدخل اسم الألبوم بما لا يتجاوز 250 حرفًا.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequireBuilding(building);
        long result;
        if (Owned)
        {
            var row = id == null ? new T_OwnedPremises_Document { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location, RentalPremises_ID = building }
                : await OwnedAlbums.SingleOrDefaultAsync(x => x.Document_ID == id && x.RentalPremises_ID == building) ?? throw new InvalidOperationException("الألبوم غير موجود.");
            if (id == null) db.Add(row);
            row.Tit = title; row.doc_date = date; row.AlbumIcon = icon;
            await db.SaveChangesAsync(); result = row.Document_ID;
        }
        else
        {
            var row = id == null ? new T_RentalPremises_Document { Branch_ID = scope.BranchId, Location_ID = scope.LocationId, Location = scope.Location, RentalPremises_ID = building }
                : await RentedAlbums.SingleOrDefaultAsync(x => x.Document_ID == id && x.RentalPremises_ID == building) ?? throw new InvalidOperationException("الألبوم غير موجود.");
            if (id == null) db.Add(row);
            row.Tit = title; row.doc_date = date; row.AlbumIcon = icon;
            await db.SaveChangesAsync(); result = row.Document_ID;
        }
        await tx.CommitAsync(); return result;
    }

    public async Task DeleteAlbumsAsync(long building, long[] ids)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (long id in ids.Distinct())
        {
            await RequireAlbum(building, id);
            // الجدول المملوك بلا FK؛ نحذف الأبناء صراحة في النوعين قبل حذف الألبوم.
            if (Owned)
            {
                await db.T_Sub_Docs.Where(x => x.Doc_ID == id).ExecuteDeleteAsync();
                await OwnedAlbums.Where(x => x.Document_ID == id).ExecuteDeleteAsync();
            }
            else
            {
                await db.T_Sub_Doc_rentals.Where(x => x.Doc_ID == id).ExecuteDeleteAsync();
                await RentedAlbums.Where(x => x.Document_ID == id).ExecuteDeleteAsync();
            }
        }
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    #endregion

    #region قراءة المستند وحفظ بياناته واستبدال محتواه دون المساس بباقي الألبوم
    public async Task<byte[]> ReadAsync(long building, long album, long id, bool legacy = false)
    {
        await RequireAlbum(building, album);
        byte[]? bytes = legacy
            ? Owned ? await OwnedAlbums.Where(x => x.Document_ID == album).Select(x => x.Documents).SingleAsync()
                    : await RentedAlbums.Where(x => x.Document_ID == album).Select(x => x.Documents).SingleAsync()
            : Owned ? await db.T_Sub_Docs.Where(x => x.Doc_ID == album && x.id == id).Select(x => x.Docm_Image).SingleOrDefaultAsync()
                    : await db.T_Sub_Doc_rentals.Where(x => x.Doc_ID == album && x.id == id).Select(x => x.Docm_Image).SingleOrDefaultAsync();
        return bytes is { Length: > 0 } ? bytes : throw new InvalidOperationException("المستند غير موجود أو لا يحتوي ملفًا.");
    }

    public async Task<long> SaveDocumentAsync(long building, long album, long? id, bool legacy, string title, DateOnly date, string subject, string icon, byte[]? replacement)
    {
        title = title.Trim(); subject = subject.Trim();
        if (title.Length is 0 or > 250 || subject.Length > 1000) throw new InvalidOperationException("تحقق من عنوان المستند (250 حرفًا) وموضوعه (1000 حرف).");
        if (replacement != null) ArchiveFiles.Validate(replacement);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequireAlbum(building, album);
        // عند تعديل المستند القديم ننقله إلى صف مستقل في المعاملة نفسها، ثم نفرغ الحقل القديم بعد الحفظ.
        if (legacy) { replacement ??= await ReadAsync(building, album, 0, true); id = null; }
        if (id == null && replacement == null) throw new InvalidOperationException("أرفق ملفًا للمستند أولًا.");
        long result;
        if (Owned)
        {
            var row = id == null ? new T_Sub_Doc { Doc_ID = album, RentalPremises_ID = building, doc_type = type,
                page_NO = (await db.T_Sub_Docs.Where(x => x.Doc_ID == album).MaxAsync(x => (long?)x.page_NO) ?? 0) + 1 }
                : await db.T_Sub_Docs.SingleOrDefaultAsync(x => x.Doc_ID == album && x.id == id) ?? throw new InvalidOperationException("المستند غير موجود.");
            if (id == null) db.Add(row);
            row.DocumentTitle = title; row.DocumentDate = date; row.DocumentSubject = subject; row.DocumentIcon = icon;
            row.doc_type = type; row.RentalPremises_ID = building;
            if (replacement != null) row.Docm_Image = replacement;
            if (legacy) (await OwnedAlbums.SingleAsync(x => x.Document_ID == album)).Documents = null!;
            await db.SaveChangesAsync(); result = row.id;
        }
        else
        {
            var row = id == null ? new T_Sub_Doc_rental { Doc_ID = album, RentalPremises_ID = building, doc_type = type,
                page_NO = (await db.T_Sub_Doc_rentals.Where(x => x.Doc_ID == album).MaxAsync(x => (long?)x.page_NO) ?? 0) + 1 }
                : await db.T_Sub_Doc_rentals.SingleOrDefaultAsync(x => x.Doc_ID == album && x.id == id) ?? throw new InvalidOperationException("المستند غير موجود.");
            if (id == null) db.Add(row);
            row.DocumentTitle = title; row.DocumentDate = date; row.DocumentSubject = subject; row.DocumentIcon = icon;
            row.doc_type = type; row.RentalPremises_ID = building;
            if (replacement != null) row.Docm_Image = replacement;
            if (legacy) (await RentedAlbums.SingleAsync(x => x.Document_ID == album)).Documents = null!;
            await db.SaveChangesAsync(); result = row.id;
        }
        await tx.CommitAsync(); return result;
    }

    public async Task DeleteDocumentsAsync(long building, long album, IEnumerable<ArchiveCard> cards)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequireAlbum(building, album);
        foreach (var card in cards)
        {
            if (card.ParentId != album) throw new InvalidOperationException("تغير الألبوم المحدد.");
            if (Owned)
            {
                if (card.Legacy) (await OwnedAlbums.SingleAsync(x => x.Document_ID == album)).Documents = null!;
                else db.Remove(await db.T_Sub_Docs.SingleAsync(x => x.Doc_ID == album && x.id == card.Id));
            }
            else
            {
                if (card.Legacy) (await RentedAlbums.SingleAsync(x => x.Document_ID == album)).Documents = null!;
                else db.Remove(await db.T_Sub_Doc_rentals.SingleAsync(x => x.Doc_ID == album && x.id == card.Id));
            }
        }
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    #endregion
}
