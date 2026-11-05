namespace SchoolPlatform.Host.Finance;

// Concepts classify obligations; they are not money. Amounts and calendars are data entered by the school.
public sealed class FeeConcept
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Recurring by billing period (tuition-like). Non recurring concepts are one-off charges.</summary>
    public bool IsRecurring { get; set; }
    /// <summary>Whether covering these obligations is required for academic clearance. Decided by the school per concept.</summary>
    public bool CountsForClearance { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] Version { get; set; } = [];
}

// Explicit billing calendar of a school year (e.g. "Cuota 3", due date). Never derived from "the current month".
public sealed class BillingPeriod
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public int Sequence { get; set; }
    public string Label { get; set; } = "";
    public DateOnly DueOn { get; set; }
}

// A cutoff chosen by the school: obligations of clearance concepts in periods 1..ThroughSequence must be covered.
public sealed class ClearanceCutoff
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; } = "";
    public int ThroughSequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

public sealed class Charge
{
    public Guid Id { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid ConceptId { get; set; }
    public Guid? BillingPeriodId { get; set; }
    public int Occurrence { get; set; } = 1;
    /// <summary>Business identity of the obligation: enrollment + concept + period + occurrence. Unique among non-voided charges.</summary>
    public string ObligationKey { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public Guid? BatchId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] Version { get; set; } = [];
    public List<ChargeAdjustment> Adjustments { get; set; } = [];
}

public static class AdjustmentKinds
{
    public const string Scholarship = "Scholarship";
    public const string Discount = "Discount";
    public const string Waiver = "Waiver";
    public static readonly string[] All = [Scholarship, Discount, Waiver];
}

// Authorized reduction of an obligation. The rule and reason stay recorded instead of editing the amount.
public sealed class ChargeAdjustment
{
    public Guid Id { get; set; }
    public Guid ChargeId { get; set; }
    public string Kind { get; set; } = "";
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

public static class PaymentMethods
{
    public static readonly string[] All = ["Cash", "Transfer", "Deposit", "Card", "Check", "Other"];
}

public sealed class Payment
{
    public Guid Id { get; set; }
    public long ReceiptNumber { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string PayerName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public string Method { get; set; } = "";
    public string Reference { get; set; } = "";
    public DateOnly ReceivedOn { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
    public Guid RecordedBy { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public Guid? ReversedBy { get; set; }
    public string? ReversalReason { get; set; }
    /// <summary>Immutable JSON snapshot of the receipt as issued (institution data, payer, lines).</summary>
    public string ReceiptSnapshot { get; set; } = "";
    public byte[] Version { get; set; } = [];
    public List<PaymentAllocation> Allocations { get; set; } = [];
}

public sealed class PaymentAllocation
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid ChargeId { get; set; }
    public decimal Amount { get; set; }
}

// Content-addressed copy of brand images used by issued documents, so later logo changes do not alter history.
public sealed class DocumentAsset
{
    public string Sha256 { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}
