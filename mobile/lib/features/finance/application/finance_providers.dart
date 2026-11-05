import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repository_providers.dart';
import '../../../domain/models/finance.dart';
import '../../../domain/models/people.dart';
import '../../academics/application/academic_providers.dart';
import '../../auth/application/auth_controller.dart';

final statementProvider = FutureProvider.autoDispose.family<AccountStatement, String>(
  (ref, studentId) => ref.watch(financeRepositoryProvider).fetchStatement(studentId),
);

final financeOverviewProvider = FutureProvider.autoDispose<FinanceOverview>(
  (ref) => ref.watch(financeRepositoryProvider).fetchOverview(),
);

final studentSearchProvider = FutureProvider.autoDispose.family<List<StudentSummary>, String>(
  (ref, query) => ref.watch(financeRepositoryProvider).searchStudents(query),
);

final evidencesProvider = FutureProvider.autoDispose<List<PaymentEvidence>>(
  (ref) => ref.watch(financeRepositoryProvider).fetchEvidences(),
);

final paymentsProvider = FutureProvider.autoDispose<List<Payment>>(
  (ref) => ref.watch(financeRepositoryProvider).fetchPayments(),
);

final paymentProvider = FutureProvider.autoDispose.family<Payment, String>(
  (ref, id) => ref.watch(financeRepositoryProvider).fetchPayment(id),
);

/// Operaciones financieras. Tras cada cambio se invalidan las consultas que
/// dependen de saldos y solvencia (incluidas las notas).
class FinanceActions {
  FinanceActions(this._ref);

  final Ref _ref;

  String get _actor => _ref.read(requireSessionProvider).displayName;

  void _invalidateMoney(String studentId) {
    _ref
      ..invalidate(statementProvider(studentId))
      ..invalidate(financeOverviewProvider)
      ..invalidate(evidencesProvider)
      ..invalidate(paymentsProvider)
      ..invalidate(gradeReportProvider);
  }

  Future<PaymentEvidence> submitEvidence(String studentId, EvidenceDraft draft) async {
    final evidence = await _ref
        .read(financeRepositoryProvider)
        .submitEvidence(studentId, draft, submittedBy: _actor);
    _invalidateMoney(studentId);
    return evidence;
  }

  Future<List<PaymentAllocation>> preview(PaymentRequest request) =>
      _ref.read(financeRepositoryProvider).previewAllocation(request);

  Future<Payment> registerPayment(PaymentRequest request) async {
    final payment = await _ref
        .read(financeRepositoryProvider)
        .registerPayment(request, cashier: _actor);
    _invalidateMoney(request.studentId);
    return payment;
  }

  Future<void> reviewEvidence(
    PaymentEvidence evidence, {
    required bool accept,
    String? reason,
  }) async {
    await _ref
        .read(financeRepositoryProvider)
        .reviewEvidence(evidence.id, accept: accept, reviewer: _actor, reason: reason);
    _invalidateMoney(evidence.studentId);
  }
}

final financeActionsProvider = Provider<FinanceActions>(FinanceActions.new);
