namespace SchoolPlatform.Host.Finance;

public sealed record ConceptRequest(string Code, string Name, bool IsRecurring, bool CountsForClearance, bool IsActive = true, string? Version = null);
public sealed record ConceptView(Guid Id, string Code, string Name, bool IsRecurring, bool CountsForClearance, bool IsActive, string Version);
public sealed record BillingPeriodRequest(string Label, DateOnly DueOn);
public sealed record BillingPeriodView(Guid Id, int Sequence, string Label, DateOnly DueOn, int Charges);
public sealed record CutoffRequest(string Name, int ThroughSequence);
public sealed record CutoffView(Guid Id, string Name, int ThroughSequence, DateTimeOffset CreatedAt);
public sealed record FinanceSetup(ConceptView[] Concepts, BillingPeriodView[] Periods, CutoffView[] Cutoffs, string Currency);

public sealed record GenerationRequest(Guid AcademicYearId, Guid ConceptId, Guid? BillingPeriodId, int Occurrence, decimal Amount,
    DateOnly? DueOn, string? Description, Guid[]? GradeIds, Guid[]? SectionIds, Guid[]? ExcludedEnrollmentIds, string? PreviewHash = null);
public sealed record GenerationRow(Guid EnrollmentId, Guid StudentId, string StudentName, string StudentCode, string? Grade, string? Section,
    string Outcome);
public sealed record GenerationPreview(string PreviewHash, GenerationRow[] Rows, int ToCreate, int Existing, int Excluded, decimal Total, string Currency);
public sealed record GenerationResult(Guid BatchId, int Created, int Existing, int Excluded);

public sealed record SingleChargeRequest(Guid EnrollmentId, Guid ConceptId, Guid? BillingPeriodId, int Occurrence, decimal Amount,
    DateOnly? DueOn, string? Description);
public sealed record AdjustmentRequest(string Version, string Kind, decimal Amount, string Reason);
public sealed record VoidRequest(string Version, string Reason);

public sealed record ChargeLine(Guid Id, Guid AcademicYearId, string Year, string Concept, bool CountsForClearance, int? PeriodSequence,
    string? Period, string Description, decimal Amount, decimal Adjustments, decimal Paid, decimal Balance, DateOnly DueOn, string Status,
    string Version);
public sealed record PaymentSummary(Guid Id, long ReceiptNumber, DateOnly ReceivedOn, decimal Amount, string Method, string PayerName,
    bool Reversed, string Version);
public sealed record Statement(Guid StudentId, string StudentName, string StudentCode, string Currency, ChargeLine[] Charges,
    PaymentSummary[] Payments, decimal TotalBalance);

public sealed record AllocationRequest(Guid ChargeId, decimal Amount);
public sealed record PaymentRequest(string IdempotencyKey, string PayerName, string Method, string? Reference, DateOnly ReceivedOn,
    string? Note, decimal Amount, AllocationRequest[] Allocations);
public sealed record ReverseRequest(string Version, string Reason);

public sealed record ReceiptLine(string StudentName, string StudentCode, string Concept, string? Period, string Description, decimal Amount);
public sealed record ReceiptSnapshot(long Number, string InstitutionName, string LegalName, string TaxId, string Address, string Phone,
    string Email, string LogoSha256, string Footer, string PayerName, DateOnly ReceivedOn, DateTimeOffset IssuedAt, string Method,
    string Reference, string Currency, decimal Amount, ReceiptLine[] Lines, string IssuedBy);
public sealed record ReceiptView(Guid PaymentId, ReceiptSnapshot Receipt, bool Reversed, DateTimeOffset? ReversedAt, string? ReversalReason);

public static class ClearanceStatus
{
    public const string Clear = "Clear";
    public const string Blocked = "Blocked";
    public const string PendingFinancialSetup = "PendingFinancialSetup";
    public const string NotEnrolled = "NotEnrolled";
}

public sealed record PendingItem(Guid? ChargeId, string Label, decimal Balance, DateOnly? DueOn);
public sealed record ClearanceResult(Guid StudentId, Guid CutoffId, string Cutoff, string Status, PendingItem[] Pending, string[] Missing);
public sealed record ClearanceRow(Guid StudentId, string StudentName, string StudentCode, string? Grade, string? Section, string Status,
    decimal PendingBalance, int MissingPeriods);
public sealed record ClearanceReport(Guid CutoffId, string Cutoff, int Clear, int Blocked, int PendingSetup, ClearanceRow[] Rows);
