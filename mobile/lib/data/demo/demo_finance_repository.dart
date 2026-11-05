import '../../core/utils/text_search.dart';
import '../../domain/models/finance.dart';
import '../../domain/models/people.dart';
import '../../domain/repositories/finance_repository.dart';
import 'demo_backend.dart';
import 'demo_database.dart';

class DemoFinanceRepository implements FinanceRepository {
  DemoFinanceRepository(this._db, this._backend);

  final DemoDatabase _db;
  final DemoBackend _backend;

  @override
  Future<AccountStatement> fetchStatement(String studentId) =>
      _backend.call(() => _statement(studentId));

  AccountStatement _statement(String studentId) {
    final charges = _db.charges.where((c) => c.studentId == studentId).toList()
      ..sort((a, b) => a.dueDate.compareTo(b.dueDate));
    return AccountStatement(
      student: _db.student(studentId),
      charges: List.unmodifiable(charges),
      payments: List.unmodifiable(_db.payments.where((p) => p.studentId == studentId)),
      evidences: List.unmodifiable(
        _db.evidences.where((e) => e.studentId == studentId).toList()
          ..sort((a, b) => b.submittedAt.compareTo(a.submittedAt)),
      ),
      clearance: _db.clearanceFor(studentId),
      totalPending: charges.fold(
        0,
        (sum, c) =>
            sum + (c.dueDate.isAfter(_db.today.add(const Duration(days: 31))) ? 0 : c.balance),
      ),
      creditBalance: _db.credits[studentId] ?? 0,
    );
  }

  @override
  Future<PaymentEvidence> submitEvidence(
    String studentId,
    EvidenceDraft draft, {
    required String submittedBy,
  }) {
    return _backend.call(() {
      if (draft.amount <= 0) throw const FinanceException('El monto debe ser mayor que cero.');
      if (draft.reference.trim().length < 4) {
        throw const FinanceException('Escribe la referencia del comprobante.');
      }
      final duplicate = _db.evidences.any(
        (e) =>
            e.reference.toUpperCase() == draft.reference.trim().toUpperCase() &&
            e.status != EvidenceStatus.rejected,
      );
      if (duplicate) {
        throw const FinanceException('Ya existe un comprobante con esa referencia.');
      }
      final evidence = PaymentEvidence(
        id: _db.nextId('evd'),
        studentId: studentId,
        studentName: _db.student(studentId).fullName,
        submittedBy: submittedBy,
        amount: draft.amount,
        reference: draft.reference.trim().toUpperCase(),
        bank: draft.bank,
        paidOn: draft.paidOn,
        submittedAt: _db.now,
        fileName: draft.fileName,
        status: EvidenceStatus.inReview,
      );
      _db.evidences.insert(0, evidence);
      return evidence;
    });
  }

  @override
  Future<FinanceOverview> fetchOverview() {
    return _backend.call(() {
      final today = _db.today;
      double collectedIn(int monthOffset) {
        final month = DateTime(today.year, today.month + monthOffset);
        return _db.payments
            .where((p) => p.paidAt.year == month.year && p.paidAt.month == month.month)
            .fold(0, (sum, p) => sum + p.amount);
      }

      const monthNames = [
        'ene',
        'feb',
        'mar',
        'abr',
        'may',
        'jun',
        'jul',
        'ago',
        'sep',
        'oct',
        'nov',
        'dic',
      ];
      final open = _db.charges.where(
        (c) => c.status.isOpen && !c.dueDate.isAfter(today.add(const Duration(days: 31))),
      );
      final activeStudents = _db.students
          .where((s) => _db.charges.any((c) => c.studentId == s.id))
          .toList();
      final cleared = activeStudents
          .where((s) => _db.clearanceFor(s.id).state == ClearanceState.cleared)
          .length;
      return FinanceOverview(
        collectedThisMonth: collectedIn(0),
        collectedLastMonth: collectedIn(-1),
        pendingTotal: open.fold(0, (sum, c) => sum + c.balance),
        overdueTotal: open
            .where((c) => c.dueDate.isBefore(today))
            .fold(0, (sum, c) => sum + c.balance),
        clearanceRate: activeStudents.isEmpty ? 0 : cleared / activeStudents.length * 100,
        receiptsThisMonth: _db.payments
            .where((p) => p.paidAt.year == today.year && p.paidAt.month == today.month)
            .length,
        evidencesInReview: _db.evidences.where((e) => e.status == EvidenceStatus.inReview).length,
        monthlyCollections: [
          for (var i = -5; i <= 0; i++)
            MonthAmount(
              label: monthNames[DateTime(today.year, today.month + i).month - 1],
              amount: collectedIn(i),
            ),
        ],
      );
    });
  }

  @override
  Future<List<StudentSummary>> searchStudents(String query) {
    return _backend.call(() {
      final list = _db.students
          .where((s) => matchesSearch('${s.fullName} ${s.code} ${s.placement}', query))
          .toList();
      return List.unmodifiable(list);
    });
  }

  @override
  Future<List<PaymentAllocation>> previewAllocation(PaymentRequest request) {
    return _backend.call(() {
      _validate(request);
      return _db.allocate(request.chargeIds, request.amount).allocations;
    });
  }

  void _validate(PaymentRequest request) {
    if (request.amount <= 0) throw const FinanceException('El monto debe ser mayor que cero.');
    if (request.chargeIds.isEmpty) throw const FinanceException('Selecciona al menos un cargo.');
    final valid = request.chargeIds.every(
      (id) =>
          _db.charges.any((c) => c.id == id && c.studentId == request.studentId && c.balance > 0),
    );
    if (!valid) {
      throw const FinanceException('Uno de los cargos ya no tiene saldo. Actualiza la cuenta.');
    }
  }

  @override
  Future<Payment> registerPayment(PaymentRequest request, {required String cashier}) {
    return _backend.call(() {
      final existing = _db.paymentsByIdempotencyKey[request.idempotencyKey];
      if (existing != null) return existing; // reintento seguro, sin duplicar
      _validate(request);
      final result = _db.allocate(request.chargeIds, request.amount);
      _db.applyAllocations(result.allocations);
      if (result.credit > 0) {
        _db.credits[request.studentId] = (_db.credits[request.studentId] ?? 0) + result.credit;
      }
      final student = _db.student(request.studentId);
      final payment = Payment(
        id: _db.nextId('pay'),
        receiptNumber: _db.nextReceiptNumber(),
        studentId: student.id,
        studentName: student.fullName,
        amount: request.amount,
        method: request.method,
        paidAt: _db.now,
        reference: request.reference.trim(),
        allocations: result.allocations,
        registeredBy: cashier,
        creditBalance: result.credit,
      );
      _db.payments.insert(0, payment);
      _db.paymentsByIdempotencyKey[request.idempotencyKey] = payment;
      return payment;
    });
  }

  @override
  Future<List<PaymentEvidence>> fetchEvidences() {
    return _backend.call(() {
      final list = List.of(_db.evidences)
        ..sort((a, b) {
          final byStatus = a.status.index.compareTo(b.status.index);
          return byStatus != 0 ? byStatus : b.submittedAt.compareTo(a.submittedAt);
        });
      return List.unmodifiable(list);
    });
  }

  @override
  Future<void> reviewEvidence(
    String evidenceId, {
    required bool accept,
    required String reviewer,
    String? reason,
  }) {
    return _backend.call(() {
      final index = _db.evidences.indexWhere((e) => e.id == evidenceId);
      final evidence = _db.evidences[index];
      if (evidence.status != EvidenceStatus.inReview) {
        throw const FinanceException('Este comprobante ya fue revisado.');
      }
      if (!accept) {
        if (reason == null || reason.trim().length < 5) {
          throw const FinanceException('Indica el motivo del rechazo.');
        }
        _db.evidences[index] = evidence.copyWith(
          status: EvidenceStatus.rejected,
          rejectionReason: reason.trim(),
        );
        return;
      }
      final open =
          _db.charges.where((c) => c.studentId == evidence.studentId && c.balance > 0).toList()
            ..sort((a, b) => a.dueDate.compareTo(b.dueDate));
      final result = _db.allocate(open.map((c) => c.id).toList(), evidence.amount);
      _db.applyAllocations(result.allocations);
      if (result.credit > 0) {
        _db.credits[evidence.studentId] = (_db.credits[evidence.studentId] ?? 0) + result.credit;
      }
      _db.payments.insert(
        0,
        Payment(
          id: _db.nextId('pay'),
          receiptNumber: _db.nextReceiptNumber(),
          studentId: evidence.studentId,
          studentName: evidence.studentName,
          amount: evidence.amount,
          method: PaymentMethod.transfer,
          paidAt: _db.now,
          reference: evidence.reference,
          allocations: result.allocations,
          registeredBy: reviewer,
          creditBalance: result.credit,
        ),
      );
      _db.evidences[index] = evidence.copyWith(status: EvidenceStatus.accepted);
    });
  }

  @override
  Future<List<Payment>> fetchPayments() => _backend.call(() => List.unmodifiable(_db.payments));

  @override
  Future<Payment> fetchPayment(String paymentId) =>
      _backend.call(() => _db.payments.firstWhere((p) => p.id == paymentId));
}
