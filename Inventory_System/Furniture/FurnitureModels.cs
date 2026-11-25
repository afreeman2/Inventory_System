using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Inventory_System.Furniture;

// نماذج عرض مستقلة عن كيانات EF حتى تبقى الشاشة واضحة ولا تعدّل قاعدة البيانات قبل الحفظ.
internal sealed record FurnitureLocationChoice(int Id, string Name);
internal sealed record FurnitureClassChoice(int Id, string Name, int Percentage);
internal sealed record FurnitureTypeChoice(int Id, string Name, string Barcode, int ClassId, string ClassName);
internal sealed record FurnitureCatalogRow(int ClassId, int TypeId, string Name, string Barcode, string ClassName, int Percentage)
{
    #region قيم البحث في الجدول
    // نطبّع النص للبحث فقط، مع عرض الأسماء الأصلية في الجدول.
    public string SearchName => Inventory_System.converts.ArabicGridSearch.Normalize(Name);
    public string SearchClassName => Inventory_System.converts.ArabicGridSearch.Normalize(ClassName);
    #endregion
}
internal sealed record FurnitureRecordRow(long Id, int? BuildingId, string Building, DateOnly? Date, string DateText,
    string Description, int LineCount, long Quantity)
{
    // يحمل الرأس تفاصيله لعرضها مباشرة أسفل السجل في الجدول الرئيسي.
    public IReadOnlyList<FurnitureDetailRow> Details { get; init; } = [];

    #region نصوص البحث العربية لسجل الحصر
    // يهيئ النصوص للبحث دون تغيير البيانات التي تظهر للمستخدم.
    public string SearchName => Inventory_System.converts.ArabicGridSearch.Normalize(Building);
    public string SearchContent => Inventory_System.converts.ArabicGridSearch.Normalize(string.Join(" ",
        new[] { Id.ToString(), DateText, Description }
            .Concat(Details.SelectMany(x => new[] { x.Management, x.Section, x.Type, x.Notes }))));
    #endregion
}

internal sealed record FurnitureDetailRow(long RecordId, long Id, int TypeId, short ManagementId, short SectionId,
    string Type, string Management, string Section, long Count, string Notes);

// تمثل مسودة سطر الحصر وتبلغ الجدول عند تغيير العدد أو الملاحظات.
internal sealed class FurnitureDetailDraft : INotifyPropertyChanged
{
    private long count;
    private string notes = "";

    public long? Id { get; init; }
    public int TypeId { get; init; }
    public short ManagementId { get; init; }
    public short SectionId { get; init; }
    public string Type { get; init; } = "";
    public string Management { get; init; } = "";
    public string Section { get; init; } = "";

    public long Count
    {
        get => count;
        set { if (count == value) return; count = value; OnPropertyChanged(); }
    }

    public string Notes
    {
        get => notes;
        set { if (notes == value) return; notes = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

internal sealed record FurnitureRecordDraft(long? Id, int BuildingId, DateOnly Date, string Description,
    IReadOnlyList<FurnitureDetailDraft> Details);
