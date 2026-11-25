using Inventory_System.Models;
using Microsoft.EntityFrameworkCore;
using MahApps.Metro.IconPacks;
using System.ComponentModel;

namespace Inventory_System.Models
{
    #region الحقول الإضافية للأرشيف دون تغيير أعمدة الملفات القديمة
    public partial class T_OwnedPremises_Document { public string? AlbumIcon { get; set; } }
    public partial class T_RentalPremises_Document { public string? AlbumIcon { get; set; } }
    public partial class T_Sub_Doc
    {
        // لكل صف مستند مستقل، ويمكن أن يحتوي ملف PDF متعدد الصفحات.
        public string? DocumentTitle { get; set; }
        public DateOnly? DocumentDate { get; set; }
        public string? DocumentSubject { get; set; }
        public string? DocumentIcon { get; set; }
    }
    public partial class T_Sub_Doc_rental
    {
        public string? DocumentTitle { get; set; }
        public DateOnly? DocumentDate { get; set; }
        public string? DocumentSubject { get; set; }
        public string? DocumentIcon { get; set; }
    }
    #endregion
}

namespace Inventory_System.Archive
{
    #region نماذج العرض المستقلة عن البيانات الثنائية والتحديد الجماعي
    public sealed class ArchiveCard : INotifyPropertyChanged
    {
        public long Id { get; init; }
        // معرف المقر في قائمة الاختيار، مستقل عن معرف سجل المقر الطويل المستخدم في جداول الأرشيف.
        public int? LookupId { get; init; }
        public long ParentId { get; init; }
        public string Title { get; init; } = "";
        public DateOnly? Date { get; init; }
        public string Subject { get; init; } = "";
        public PackIconMaterialKind Icon { get; init; } = PackIconMaterialKind.FileDocumentOutline;
        public int Count { get; init; }
        public bool Legacy { get; init; }
        public string DateText => Date?.ToString("yyyy/MM/dd") ?? "التاريخ غير مسجل";
        public string CountText => $"{Count:N0} مستند";
        private bool selected;
        public bool IsSelected { get => selected; set { selected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public sealed record ArchiveIcon(string Name, PackIconMaterialKind Kind);
    internal static class ArchiveIcons
    {
        // الأيقونات من الحزمة المدمجة، وتخزن أسماؤها الثابتة بدل أرقام قابلة للتغير بين الإصدارات.
        internal static ArchiveIcon[] All { get; } =
        [new("ألبوم", PackIconMaterialKind.FolderImage), new("مستند", PackIconMaterialKind.FileDocumentOutline),
         new("عقد", PackIconMaterialKind.FileSign), new("مبنى", PackIconMaterialKind.OfficeBuildingOutline),
         new("تأمين", PackIconMaterialKind.ShieldCheckOutline), new("مخططات", PackIconMaterialKind.FloorPlan),
         new("فواتير", PackIconMaterialKind.Receipt), new("صور", PackIconMaterialKind.ImageOutline),
         new("مراسلات", PackIconMaterialKind.EmailOutline), new("أرشيف", PackIconMaterialKind.ArchiveOutline)];
        internal static PackIconMaterialKind Parse(string? value, bool album = false) =>
            Enum.TryParse<PackIconMaterialKind>(value, out var kind) && Enum.IsDefined(kind) ? kind :
                album ? PackIconMaterialKind.FolderOutline : PackIconMaterialKind.FileDocumentOutline;
    }
    #endregion
}
