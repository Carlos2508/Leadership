import '../models/finance.dart';
import '../models/people.dart';

class FinanceException implements Exception {
  const FinanceException(this.message);

  final String message;

  @override
  String toString() => message;
}

abstract interface class FinanceRepository {
  Future<AccountStatement> fetchStatement(String studentId);

  Future<PaymentEvidence> submitEvidence(
    String studentId,
    EvidenceDraft draft, {
    required String submittedBy,
  });

  Future<FinanceOverview> fetchOverview();

  Future<List<StudentSummary>> searchStudents(String query);

  /// Previsualiza cómo se distribuye un monto entre los cargos elegidos.
  Future<List<PaymentAllocation>> previewAllocation(PaymentRequest request);

  /// Confirma el pago de forma idempotente y devuelve el recibo.
  Future<Payment> registerPayment(PaymentRequest request, {required String cashier});

  Future<List<PaymentEvidence>> fetchEvidences();

  Future<void> reviewEvidence(
    String evidenceId, {
    required bool accept,
    required String reviewer,
    String? reason,
  });

  Future<List<Payment>> fetchPayments();

  Future<Payment> fetchPayment(String paymentId);
}
