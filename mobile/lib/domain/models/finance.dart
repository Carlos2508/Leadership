import 'people.dart';

enum ChargeStatus {
  paid('Pagado'),
  partial('Abono parcial'),
  pending('Pendiente'),
  overdue('Vencido'),
  exempt('Exonerado');

  const ChargeStatus(this.label);

  final String label;

  bool get isOpen =>
      this == ChargeStatus.pending || this == ChargeStatus.partial || this == ChargeStatus.overdue;
}

class Charge {
  const Charge({
    required this.id,
    required this.studentId,
    required this.concept,
    required this.periodLabel,
    required this.sequence,
    required this.amount,
    required this.balance,
    required this.dueDate,
    required this.status,
    required this.countsForClearance,
  });

  final String id;
  final String studentId;
  final String concept;
  final String periodLabel;

  /// Número de cuota dentro del plan; 0 para cobros únicos.
  final int sequence;
  final double amount;
  final double balance;
  final DateTime dueDate;
  final ChargeStatus status;
  final bool countsForClearance;

  String get label => periodLabel.isEmpty ? concept : '$concept · $periodLabel';
}

enum PaymentMethod {
  cash('Efectivo'),
  transfer('Transferencia'),
  deposit('Depósito'),
  card('Tarjeta');

  const PaymentMethod(this.label);

  final String label;
}

class PaymentAllocation {
  const PaymentAllocation({required this.chargeId, required this.label, required this.amount});

  final String chargeId;
  final String label;
  final double amount;
}

class Payment {
  const Payment({
    required this.id,
    required this.receiptNumber,
    required this.studentId,
    required this.studentName,
    required this.amount,
    required this.method,
    required this.paidAt,
    required this.reference,
    required this.allocations,
    required this.registeredBy,
    this.creditBalance = 0,
  });

  final String id;
  final String receiptNumber;
  final String studentId;
  final String studentName;
  final double amount;
  final PaymentMethod method;
  final DateTime paidAt;
  final String reference;
  final List<PaymentAllocation> allocations;
  final String registeredBy;

  /// Excedente conservado como saldo a favor del mismo estudiante.
  final double creditBalance;
}

enum EvidenceStatus {
  inReview('En revisión'),
  accepted('Aceptado'),
  rejected('Rechazado');

  const EvidenceStatus(this.label);

  final String label;
}

class PaymentEvidence {
  const PaymentEvidence({
    required this.id,
    required this.studentId,
    required this.studentName,
    required this.submittedBy,
    required this.amount,
    required this.reference,
    required this.bank,
    required this.paidOn,
    required this.submittedAt,
    required this.fileName,
    required this.status,
    this.rejectionReason,
  });

  final String id;
  final String studentId;
  final String studentName;
  final String submittedBy;
  final double amount;
  final String reference;
  final String bank;
  final DateTime paidOn;
  final DateTime submittedAt;
  final String fileName;
  final EvidenceStatus status;
  final String? rejectionReason;

  PaymentEvidence copyWith({EvidenceStatus? status, String? rejectionReason}) {
    return PaymentEvidence(
      id: id,
      studentId: studentId,
      studentName: studentName,
      submittedBy: submittedBy,
      amount: amount,
      reference: reference,
      bank: bank,
      paidOn: paidOn,
      submittedAt: submittedAt,
      fileName: fileName,
      status: status ?? this.status,
      rejectionReason: rejectionReason ?? this.rejectionReason,
    );
  }
}

enum ClearanceState { cleared, notCleared, pendingSetup }

class Clearance {
  const Clearance({
    required this.state,
    required this.requirementLabel,
    required this.pendingPeriods,
  });

  final ClearanceState state;

  /// Corte vigente, p. ej. "Cuotas 1 a 5 para el 2.º bimestre".
  final String requirementLabel;
  final List<String> pendingPeriods;
}

class AccountStatement {
  const AccountStatement({
    required this.student,
    required this.charges,
    required this.payments,
    required this.evidences,
    required this.clearance,
    required this.totalPending,
    required this.creditBalance,
  });

  final StudentSummary student;
  final List<Charge> charges;
  final List<Payment> payments;
  final List<PaymentEvidence> evidences;
  final Clearance clearance;
  final double totalPending;
  final double creditBalance;

  List<Charge> get openCharges => charges.where((charge) => charge.status.isOpen).toList();
}

class MonthAmount {
  const MonthAmount({required this.label, required this.amount});

  final String label;
  final double amount;
}

class FinanceOverview {
  const FinanceOverview({
    required this.collectedThisMonth,
    required this.collectedLastMonth,
    required this.pendingTotal,
    required this.overdueTotal,
    required this.clearanceRate,
    required this.receiptsThisMonth,
    required this.evidencesInReview,
    required this.monthlyCollections,
  });

  final double collectedThisMonth;
  final double collectedLastMonth;
  final double pendingTotal;
  final double overdueTotal;
  final double clearanceRate;
  final int receiptsThisMonth;
  final int evidencesInReview;
  final List<MonthAmount> monthlyCollections;
}

class PaymentRequest {
  const PaymentRequest({
    required this.studentId,
    required this.chargeIds,
    required this.amount,
    required this.method,
    required this.reference,
    required this.idempotencyKey,
  });

  final String studentId;

  /// Cargos elegidos en orden de aplicación.
  final List<String> chargeIds;
  final double amount;
  final PaymentMethod method;
  final String reference;
  final String idempotencyKey;
}

class EvidenceDraft {
  const EvidenceDraft({
    required this.amount,
    required this.reference,
    required this.bank,
    required this.paidOn,
    required this.fileName,
  });

  final double amount;
  final String reference;
  final String bank;
  final DateTime paidOn;
  final String fileName;
}
