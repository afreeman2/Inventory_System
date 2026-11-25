using Inventory_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory_System.dbContext;

public partial class Cont_maintContext
{
    #region ربط الحقول الإضافية للألبومات والمستندات بالجداول الأصلية
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // الربط في ملف جزئي يحافظ على فصل الإضافات عن الملف المولد بواسطة EF Power Tools.
        modelBuilder.Entity<T_OwnedPremises_Document>().Property(x => x.AlbumIcon).HasMaxLength(80);
        modelBuilder.Entity<T_RentalPremises_Document>().Property(x => x.AlbumIcon).HasMaxLength(80);
        modelBuilder.Entity<T_Sub_Doc>().Property(x => x.DocumentTitle).HasMaxLength(250);
        modelBuilder.Entity<T_Sub_Doc>().Property(x => x.DocumentSubject).HasMaxLength(1000);
        modelBuilder.Entity<T_Sub_Doc>().Property(x => x.DocumentIcon).HasMaxLength(80);
        modelBuilder.Entity<T_Sub_Doc>().Property(x => x.DocumentDate).HasColumnType("date");
        modelBuilder.Entity<T_Sub_Doc_rental>().Property(x => x.DocumentTitle).HasMaxLength(250);
        modelBuilder.Entity<T_Sub_Doc_rental>().Property(x => x.DocumentSubject).HasMaxLength(1000);
        modelBuilder.Entity<T_Sub_Doc_rental>().Property(x => x.DocumentIcon).HasMaxLength(80);
        modelBuilder.Entity<T_Sub_Doc_rental>().Property(x => x.DocumentDate).HasColumnType("date");
    }
    #endregion
}
