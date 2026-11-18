using Inventory_System.Models;
using MahApps.Metro.IconPacks;

namespace Inventory_System.Premises;

#region أنواع المقرات والسجلات
// ترمز القاعدة للمملوكة بالقيمة 1 وللمؤجرة بالقيمة 2 كما في منظومة المصدر.
internal enum PremisesKind
{
    Owned = 1,
    Rented = 2
}

internal enum PremisesRegister
{
    Locations, PhoneBills, Power, Insurance, Water, Services, Contracts, Archive, PhoneNumbers
}
#endregion

internal sealed record PremisesSection(
    PremisesRegister Register, string Title, PackIconMaterialKind Icon,
    Func<T_SystemUser, bool> OwnedPermission, Func<T_SystemUser, bool> RentedPermission)
{
    #region أقسام الحصر وصلاحياتها
    // ترتيب البطاقات هو ترتيب لوحة الحصر في المنظومة الأصلية، مع صلاحية مستقلة لكل نوع.
    internal static IReadOnlyList<PremisesSection> All { get; } =
    [
        new(PremisesRegister.Locations, "بيانات المقرات", PackIconMaterialKind.OfficeBuildingOutline, u => u.forowbuild, u => u.forrebuild),
        new(PremisesRegister.PhoneBills, "فواتير الهاتف", PackIconMaterialKind.PhoneOutline, u => u.forowline, u => u.forreline),
        new(PremisesRegister.Power, "فواتير الكهرباء", PackIconMaterialKind.LightningBolt, u => u.forowelc, u => u.forreelc),
        new(PremisesRegister.Insurance, "عقود التأمين", PackIconMaterialKind.ShieldCheckOutline, u => u.forowsec, u => u.forresec),
        new(PremisesRegister.Water, "فواتير المياه", PackIconMaterialKind.WaterOutline, u => u.forowwat, u => u.forrewat),
        new(PremisesRegister.Services, "عقود شركات الخدمات", PackIconMaterialKind.Tools, u => u.forowserv, u => u.forreserv),
        new(PremisesRegister.Contracts, "العقود", PackIconMaterialKind.FileDocumentOutline, u => u.forowcont, u => u.forrecont),
        new(PremisesRegister.Archive, "أرشيف المستندات", PackIconMaterialKind.ArchiveOutline, u => u.forowarch, u => u.forrearch),
        new(PremisesRegister.PhoneNumbers, "أرقام الهواتف", PackIconMaterialKind.PhoneInTalkOutline, u => u.forowphon, u => u.forrephon)
    ];

    internal static T_SystemUser? User =>
        (App.appuser ?? App.login_user) is { st: not false } user ? user : null;

    internal bool CanOpen(PremisesKind kind) =>
        User is { } user && (kind == PremisesKind.Owned ? OwnedPermission(user) : RentedPermission(user));

    internal static bool CanReport(PremisesKind kind) =>
        User is { } user && (kind == PremisesKind.Owned ? user.forowrepo : user.forrerepo);

    internal static string KindName(PremisesKind kind) =>
        kind == PremisesKind.Owned ? "المقرات المملوكة" : "المقرات المؤجرة";
    #endregion
}
