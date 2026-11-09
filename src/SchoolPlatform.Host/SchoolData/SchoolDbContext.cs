using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.IdentityAccess;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

// Read-only projections of identity tables owned by IdentityDbContext. Excluded from these migrations.
public sealed class AccountRef
{
    public Guid Id { get; set; }
    public string? UserName { get; set; }
    public bool IsActive { get; set; }
    public string? PasswordHash { get; set; }
    public string? SecurityStamp { get; set; }
}

public sealed class PermissionRef
{
    public Guid UserId { get; set; }
    public string Permission { get; set; } = "";
}

/// <summary>
/// School records of this single installation (people now; academic structure and enrollment next).
/// Identity and audit tables are owned by <see cref="IdentityDbContext"/>; here they are mapped only to
/// reference accounts and to commit business changes together with their audit event in one SaveChanges.
/// </summary>
public sealed partial class SchoolDbContext(DbContextOptions<SchoolDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<PersonContact> PersonContacts => Set<PersonContact>();
    public DbSet<StudentProfile> Students => Set<StudentProfile>();
    public DbSet<GuardianProfile> Guardians => Set<GuardianProfile>();
    public DbSet<StaffProfile> Staff => Set<StaffProfile>();
    public DbSet<SchoolPlatform.Host.LegacyImport.LegacyIdentity> LegacyIdentities => Set<SchoolPlatform.Host.LegacyImport.LegacyIdentity>();
    public DbSet<GuardianRelationship> Relationships => Set<GuardianRelationship>();
    public DbSet<RelationshipFaculty> RelationshipFaculties => Set<RelationshipFaculty>();
    public DbSet<AccountLink> AccountLinks => Set<AccountLink>();
    public DbSet<AccountRef> Accounts => Set<AccountRef>();
    public DbSet<PermissionRef> Permissions => Set<PermissionRef>();
    public DbSet<SecurityAuditEvent> AuditEvents => Set<SecurityAuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<AccountRef>(entity =>
        {
            entity.ToTable("AspNetUsers", "iam", table => table.ExcludeFromMigrations());
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).HasMaxLength(256);
        });
        builder.Entity<PermissionRef>(entity =>
        {
            entity.ToTable("AccountPermissions", "iam", table => table.ExcludeFromMigrations());
            entity.HasKey(x => new { x.UserId, x.Permission });
            entity.Property(x => x.Permission).HasMaxLength(80).IsUnicode(false);
        });
        builder.Entity<SecurityAuditEvent>(entity =>
        {
            entity.ToTable("Events", "audit", table => { table.ExcludeFromMigrations(); table.HasTrigger("Events_AppendOnly"); });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(80).IsUnicode(false);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.Detail).HasMaxLength(120);
            entity.Property(x => x.TraceId).HasMaxLength(100);
            entity.Property(x => x.TargetType).HasMaxLength(40).IsUnicode(false);
        });

        ConfigurePeople(builder);
        ConfigureFamilies(builder);
        ConfigureRoster(builder);
        ConfigureAcademicModel(builder);
        ConfigureFinanceModel(builder);
        ConfigureGradesModel(builder);
        ConfigureLearningModel(builder);
        ConfigureAttendanceModel(builder);
        ConfigureCommunicationModel(builder);
        ConfigureChat(builder);
        ConfigureInventory(builder);
        ConfigureGateModel(builder);
    }

    // Implemented by the academic/enrollment and finance parts of this context.
    partial void ConfigureAcademicModel(ModelBuilder builder);
    partial void ConfigureFinanceModel(ModelBuilder builder);
    partial void ConfigureGradesModel(ModelBuilder builder);
    partial void ConfigureLearningModel(ModelBuilder builder);
    partial void ConfigureAttendanceModel(ModelBuilder builder);
    partial void ConfigureCommunicationModel(ModelBuilder builder);
    partial void ConfigureGateModel(ModelBuilder builder);

    private static void ConfigurePeople(ModelBuilder builder)
    {
        builder.Entity<Person>(entity =>
        {
            entity.ToTable("Persons", "people", table =>
                table.HasCheckConstraint("CK_Persons_Status", "[Status] IN ('Active','Archived')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GivenNames).HasMaxLength(100);
            entity.Property(x => x.FamilyNames).HasMaxLength(100);
            entity.Property(x => x.PreferredName).HasMaxLength(60);
            entity.Property(x => x.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Version).IsRowVersion();
            entity.Ignore(x => x.DisplayName);
            entity.HasIndex(x => new { x.FamilyNames, x.GivenNames });
            entity.HasMany(x => x.Contacts).WithOne().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Student).WithOne().HasForeignKey<StudentProfile>(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Guardian).WithOne().HasForeignKey<GuardianProfile>(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Staff).WithOne().HasForeignKey<StaffProfile>(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PersonContact>(entity =>
        {
            entity.ToTable("PersonContacts", "people", table =>
                table.HasCheckConstraint("CK_PersonContacts_Kind", "[Kind] IN ('Phone','Email','Address')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Value).HasMaxLength(200);
            entity.Property(x => x.Label).HasMaxLength(60);
        });
        builder.Entity<StudentProfile>(entity =>
        {
            entity.ToTable("Students", "people");
            entity.HasKey(x => x.PersonId);
            entity.Property(x => x.StudentCode).HasMaxLength(30).IsUnicode(false);
            entity.HasIndex(x => x.StudentCode).IsUnique();
        });
        builder.Entity<SchoolPlatform.Host.LegacyImport.LegacyIdentity>(entity =>
        {
            entity.ToTable("LegacyIdentities", "people", table => table.HasCheckConstraint("CK_LegacyIdentities_Entity", "[Entity] IN ('student','guardian')"));
            entity.HasKey(x => new { x.Source, x.Entity, x.LegacyId });
            entity.Property(x => x.Source).HasMaxLength(40).IsUnicode(false);
            entity.Property(x => x.Entity).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.LegacyId).HasMaxLength(60);
            entity.Property(x => x.FileSha256).HasMaxLength(64).IsUnicode(false);
            entity.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PersonId);
        });
        builder.Entity<StaffProfile>(entity =>
        {
            entity.ToTable("Staff", "people");
            entity.HasKey(x => x.PersonId);
            entity.Property(x => x.Position).HasMaxLength(80);
        });
        builder.Entity<GuardianProfile>(entity =>
        {
            entity.ToTable("Guardians", "people");
            entity.HasKey(x => x.PersonId);
        });
        builder.Entity<GuardianRelationship>(entity =>
        {
            entity.ToTable("GuardianRelationships", "people", table =>
            {
                table.HasCheckConstraint("CK_GuardianRelationships_Distinct", "[StudentId] <> [GuardianId]");
                table.HasCheckConstraint("CK_GuardianRelationships_Kind",
                    "[Kind] IN ('Mother','Father','Grandparent','Sibling','OtherRelative','LegalGuardian','Other')");
                table.HasCheckConstraint("CK_GuardianRelationships_End",
                    "([EndedAt] IS NULL AND [EndedBy] IS NULL) OR ([EndedAt] IS NOT NULL AND [EndedBy] IS NOT NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.EndReason).HasMaxLength(500);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GuardianProfile>().WithMany().HasForeignKey(x => x.GuardianId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.EndedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.StudentId, x.GuardianId }).IsUnique().HasFilter("[EndedAt] IS NULL");
            entity.HasIndex(x => new { x.GuardianId, x.EndedAt });
            entity.HasMany(x => x.Faculties).WithOne().HasForeignKey(x => x.RelationshipId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RelationshipFaculty>(entity =>
        {
            entity.ToTable("RelationshipFaculties", "people", table =>
                table.HasCheckConstraint("CK_RelationshipFaculties_Faculty",
                    "[Faculty] IN ('AcademicRecords','Finance','BillingResponsible','EmergencyContact','Pickup','Communications')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Faculty).HasMaxLength(40).IsUnicode(false);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.GrantedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.RelationshipId, x.Faculty }).IsUnique().HasFilter("[RevokedAt] IS NULL");
        });
        builder.Entity<AccountLink>(entity =>
        {
            entity.ToTable("AccountLinks", "people");
            entity.HasKey(x => x.Id);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.LinkedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.UnlinkedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("[UnlinkedAt] IS NULL");
            entity.HasIndex(x => x.PersonId).IsUnique().HasFilter("[UnlinkedAt] IS NULL");
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Usa SaveChangesAsync.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries<FamilyRegistration>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("La confirmación de un registro familiar no se modifica ni elimina.");
        if (ChangeTracker.Entries<SchoolPlatform.Host.Inventory.InventoryMovement>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("El historial de inventario solo admite nuevos movimientos.");
        if (ChangeTracker.Entries<SecurityAuditEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<AccountRef>().Any(x => x.State != EntityState.Unchanged) ||
            ChangeTracker.Entries<PermissionRef>().Any(x => x.State != EntityState.Unchanged))
            throw new InvalidOperationException("Los datos de cuentas y auditoría se modifican solo desde su módulo; la auditoría solo admite nuevos eventos.");
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
