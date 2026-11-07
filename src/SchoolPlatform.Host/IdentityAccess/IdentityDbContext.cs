using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SchoolPlatform.Host.IdentityAccess;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<AccountPermission> AccountPermissions => Set<AccountPermission>();
    public DbSet<AccessRole> AccessRoles => Set<AccessRole>();
    public DbSet<AccessRolePermission> AccessRolePermissions => Set<AccessRolePermission>();
    public DbSet<AccountRoleAssignment> AccountRoleAssignments => Set<AccountRoleAssignment>();
    public DbSet<AccountAccessGrant> AccessGrants => Set<AccountAccessGrant>();
    public DbSet<SecurityAuditEvent> AuditEvents => Set<SecurityAuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("iam");
        builder.Entity<AccessRole>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(80).IsUnicode(false);
            entity.Property(x => x.Label).HasMaxLength(100);
            entity.HasIndex(x => x.Label).IsUnique();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Version).IsRowVersion();
        });
        builder.Entity<AccessRolePermission>(entity =>
        {
            entity.HasKey(x => new { x.RoleKey, x.Permission });
            entity.Property(x => x.RoleKey).HasMaxLength(80).IsUnicode(false);
            entity.Property(x => x.Permission).HasMaxLength(80).IsUnicode(false);
            entity.HasOne<AccessRole>().WithMany().HasForeignKey(x => x.RoleKey).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AccountRoleAssignment>(entity =>
        {
            // An account may combine several roles (e.g. Secretaría + Caja in a small school); permissions are their union.
            entity.HasKey(x => new { x.UserId, x.RoleKey });
            entity.Property(x => x.RoleKey).HasMaxLength(80).IsUnicode(false);
            entity.HasOne<AccessRole>().WithMany().HasForeignKey(x => x.RoleKey).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AccountPermission>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.Permission });
            entity.Property(x => x.Permission).HasMaxLength(80).IsUnicode(false);
            entity.HasIndex(x => new { x.Permission, x.UserId });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AccountAccessGrant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Purpose).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.SecretHash).HasColumnType("binary(32)");
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.IssuedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.UserId, x.Purpose }).IsUnique()
                .HasFilter("[ConsumedAt] IS NULL AND [RevokedAt] IS NULL");
            entity.ToTable(table => table.HasCheckConstraint("CK_AccessGrants_Purpose", "[Purpose] IN ('Activation','Recovery')"));
        });
        builder.Entity<SecurityAuditEvent>(entity =>
        {
            entity.ToTable("Events", "audit", table => table.HasTrigger("Events_AppendOnly"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(80).IsUnicode(false);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.Detail).HasMaxLength(120);
            entity.Property(x => x.TraceId).HasMaxLength(100);
            entity.Property(x => x.TargetType).HasMaxLength(40).IsUnicode(false);
            entity.HasIndex(x => new { x.OccurredAt, x.Id }).IsDescending();
            entity.HasIndex(x => new { x.TargetId, x.OccurredAt });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectAudit()
    {
        if (ChangeTracker.Entries<SecurityAuditEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("La auditoría solo admite nuevos eventos.");
    }
}
