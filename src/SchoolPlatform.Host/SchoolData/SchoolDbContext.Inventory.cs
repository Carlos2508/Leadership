using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Inventory;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<InventoryProduct> InventoryProducts => Set<InventoryProduct>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    private static void ConfigureInventory(ModelBuilder builder)
    {
        builder.Entity<InventoryProduct>(e =>
        {
            e.ToTable("Products", "inventory", t =>
            {
                t.HasCheckConstraint("CK_InventoryProduct_Stock", "[OnHand] BETWEEN 0 AND 1000000 AND [Minimum] BETWEEN 0 AND 1000000 AND [LastSequence] >= 0 AND ([IsActive] = 1 OR [OnHand] = 0)");
                t.HasCheckConstraint("CK_InventoryProduct_Text", "LEN(LTRIM(RTRIM([Code]))) > 0 AND LEN(LTRIM(RTRIM([Name]))) > 0");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.Category).HasMaxLength(80);
            e.Property(x => x.Location).HasMaxLength(100);
            e.Property(x => x.CreationHash).HasColumnType("binary(32)");
            e.Property(x => x.Version).IsRowVersion();
            e.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<InventoryMovement>(e =>
        {
            e.ToTable("Movements", "inventory", t =>
            {
                t.HasTrigger("Movements_AppendOnly");
                t.HasCheckConstraint("CK_InventoryMovement_Stock", "[Before] BETWEEN 0 AND 1000000 AND [After] BETWEEN 0 AND 1000000 AND [After] = [Before] + [Delta] AND [Sequence] > 0");
                t.HasCheckConstraint("CK_InventoryMovement_Kind", "([Kind] = 'In' AND [Delta] > 0) OR ([Kind] = 'Out' AND [Delta] < 0) OR [Kind] = 'Count'");
                t.HasCheckConstraint("CK_InventoryMovement_Reason", "LEN(LTRIM(RTRIM([Reason]))) >= 8");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProductId, x.Sequence }).IsUnique();
            e.Property(x => x.Kind).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.RequestHash).HasColumnType("binary(32)");
            e.HasOne<InventoryProduct>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
