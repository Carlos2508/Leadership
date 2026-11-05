using Microsoft.EntityFrameworkCore;
using SchoolPlatform.Host.Academic;
using SchoolPlatform.Host.Finance;
using SchoolPlatform.Host.People;

namespace SchoolPlatform.Host.SchoolData;

public sealed partial class SchoolDbContext
{
    public DbSet<FeeConcept> FeeConcepts => Set<FeeConcept>();
    public DbSet<BillingPeriod> BillingPeriods => Set<BillingPeriod>();
    public DbSet<ClearanceCutoff> ClearanceCutoffs => Set<ClearanceCutoff>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<ChargeAdjustment> ChargeAdjustments => Set<ChargeAdjustment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<DocumentAsset> DocumentAssets => Set<DocumentAsset>();

    partial void ConfigureFinanceModel(ModelBuilder builder)
    {
        builder.HasSequence<long>("ReceiptNumbers", "finance").StartsAt(1).IncrementsBy(1);
        builder.Entity<FeeConcept>(entity =>
        {
            entity.ToTable("FeeConcepts", "finance");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name).IsUnique();
        });
        builder.Entity<BillingPeriod>(entity =>
        {
            entity.ToTable("BillingPeriods", "finance", table => table.HasCheckConstraint("CK_BillingPeriods_Sequence", "[Sequence] BETWEEN 1 AND 24"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Label).HasMaxLength(60);
            entity.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AcademicYearId, x.Sequence }).IsUnique();
        });
        builder.Entity<ClearanceCutoff>(entity =>
        {
            entity.ToTable("ClearanceCutoffs", "finance", table => table.HasCheckConstraint("CK_ClearanceCutoffs_Sequence", "[ThroughSequence] BETWEEN 1 AND 24"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80);
            entity.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AcademicYearId, x.Name }).IsUnique();
        });
        builder.Entity<Charge>(entity =>
        {
            entity.ToTable("Charges", "finance", table =>
            {
                table.HasCheckConstraint("CK_Charges_Amount", "[Amount] > 0");
                table.HasCheckConstraint("CK_Charges_Occurrence", "[Occurrence] >= 1");
                table.HasCheckConstraint("CK_Charges_Void",
                    "([VoidedAt] IS NULL AND [VoidedBy] IS NULL) OR ([VoidedAt] IS NOT NULL AND [VoidedBy] IS NOT NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ObligationKey).HasMaxLength(120).IsUnicode(false);
            entity.Property(x => x.Description).HasMaxLength(160);
            entity.Property(x => x.Amount).HasPrecision(19, 4);
            entity.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false).IsFixedLength();
            entity.Property(x => x.VoidReason).HasMaxLength(500);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FeeConcept>().WithMany().HasForeignKey(x => x.ConceptId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BillingPeriod>().WithMany().HasForeignKey(x => x.BillingPeriodId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.VoidedBy).OnDelete(DeleteBehavior.Restrict);
            // Two batches can never bill the same obligation twice.
            entity.HasIndex(x => x.ObligationKey).IsUnique().HasFilter("[VoidedAt] IS NULL");
            entity.HasIndex(x => new { x.StudentId, x.AcademicYearId });
            entity.HasIndex(x => new { x.EnrollmentId, x.BillingPeriodId });
            entity.HasMany(x => x.Adjustments).WithOne().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ChargeAdjustment>(entity =>
        {
            entity.ToTable("ChargeAdjustments", "finance", table =>
            {
                table.HasCheckConstraint("CK_ChargeAdjustments_Amount", "[Amount] > 0");
                table.HasCheckConstraint("CK_ChargeAdjustments_Kind", "[Kind] IN ('Scholarship','Discount','Waiver')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Amount).HasPrecision(19, 4);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments", "finance", table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                table.HasCheckConstraint("CK_Payments_Reversal",
                    "([ReversedAt] IS NULL AND [ReversedBy] IS NULL) OR ([ReversedAt] IS NOT NULL AND [ReversedBy] IS NOT NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReceiptNumber).HasDefaultValueSql("NEXT VALUE FOR finance.ReceiptNumbers");
            entity.Property(x => x.IdempotencyKey).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.PayerName).HasMaxLength(120);
            entity.Property(x => x.Amount).HasPrecision(19, 4);
            entity.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false).IsFixedLength();
            entity.Property(x => x.Method).HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.Reference).HasMaxLength(80);
            entity.Property(x => x.Note).HasMaxLength(300);
            entity.Property(x => x.ReversalReason).HasMaxLength(500);
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.RecordedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountRef>().WithMany().HasForeignKey(x => x.ReversedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ReceiptNumber).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => x.ReceivedOn);
            entity.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PaymentAllocation>(entity =>
        {
            entity.ToTable("PaymentAllocations", "finance", table => table.HasCheckConstraint("CK_PaymentAllocations_Amount", "[Amount] > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(19, 4);
            entity.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PaymentId, x.ChargeId }).IsUnique();
            entity.HasIndex(x => x.ChargeId);
        });
        builder.Entity<DocumentAsset>(entity =>
        {
            entity.ToTable("DocumentAssets", "finance");
            entity.HasKey(x => x.Sha256);
            entity.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false);
            entity.Property(x => x.ContentType).HasMaxLength(40).IsUnicode(false);
        });
    }
}
