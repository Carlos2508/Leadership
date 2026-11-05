import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_platform_app/data/demo/demo_backend.dart';
import 'package:school_platform_app/data/demo/demo_database.dart';
import 'package:school_platform_app/data/repository_providers.dart';
import 'package:school_platform_app/domain/models/assignments.dart';
import 'package:school_platform_app/domain/models/finance.dart';
import 'package:school_platform_app/domain/models/grades.dart';
import 'package:school_platform_app/domain/models/people.dart';
import 'package:school_platform_app/domain/models/teaching.dart';
import 'package:school_platform_app/domain/repositories/auth_repository.dart';
import 'package:school_platform_app/domain/repositories/finance_repository.dart';

import '../helpers/test_app.dart';

void main() {
  setUpAll(initTestEnvironment);

  late ProviderContainer container;
  setUp(() async => container = await createTestContainer());
  tearDown(() => container.dispose());

  group('autenticación', () {
    test('mensaje genérico ante usuario o contraseña incorrectos', () async {
      final auth = container.read(authRepositoryProvider);
      await expectLater(
        auth.signIn(username: 'nadie', password: 'demo1234'),
        throwsA(
          isA<AuthException>().having(
            (e) => e.message,
            'mensaje',
            'Usuario o contraseña incorrectos.',
          ),
        ),
      );
      await expectLater(
        auth.signIn(username: 'estudiante.demo', password: '123'),
        throwsA(
          isA<AuthException>().having(
            (e) => e.message,
            'mensaje',
            'Usuario o contraseña incorrectos.',
          ),
        ),
      );
      final session = await auth.signIn(username: ' Estudiante.Demo ', password: 'demo1234');
      expect(session.role, UserRole.student);
    });

    test('una cuenta desactivada no puede ingresar', () async {
      await container
          .read(administrationRepositoryProvider)
          .setAccountActive('acc-07', active: false);
      await expectLater(
        container
            .read(authRepositoryProvider)
            .signIn(username: 'estudiante.demo', password: 'demo1234'),
        throwsA(isA<AuthException>()),
      );
    });

    test('el código de activación es de un solo uso', () async {
      final code = await container
          .read(administrationRepositoryProvider)
          .issueActivationCode('acc-05');
      final auth = container.read(authRepositoryProvider);
      await auth.redeemCode(code: code.code.toLowerCase(), newPassword: 'nuevaClave1');
      await expectLater(
        auth.redeemCode(code: code.code, newPassword: 'nuevaClave1'),
        throwsA(isA<AuthException>()),
      );
    });
  });

  group('calificaciones y solvencia', () {
    test('estudiante solvente ve sus notas; pendientes no son cero', () async {
      final report = await container
          .read(academicRepositoryProvider)
          .fetchGradeReport(studentId: DemoDatabase.studentId, requester: UserRole.student);
      expect(report, isA<GradesAvailable>());
      final math = (report as GradesAvailable).subjects.first;
      expect(math.periods[2], isNull);
    });

    test(
      'sin solvencia la respuesta no contiene notas y aceptar el comprobante las habilita',
      () async {
        final academic = container.read(academicRepositoryProvider);
        final blocked = await academic.fetchGradeReport(
          studentId: DemoDatabase.siblingId,
          requester: UserRole.guardian,
        );
        expect(blocked, isA<GradesBlocked>());
        blocked as GradesBlocked;
        expect(blocked.pendingPeriods, ['Cuota 05']);
        expect(blocked.evidenceInReview, isTrue);

        final asStudent = await academic.fetchGradeReport(
          studentId: DemoDatabase.siblingId,
          requester: UserRole.student,
        );
        expect(
          (asStudent as GradesBlocked).pendingPeriods,
          isEmpty,
          reason: 'sin detalle financiero',
        );

        final finance = container.read(financeRepositoryProvider);
        await finance.reviewEvidence('evd-001', accept: true, reviewer: 'Caja');
        final released = await academic.fetchGradeReport(
          studentId: DemoDatabase.siblingId,
          requester: UserRole.guardian,
        );
        expect(released, isA<GradesAvailable>());
      },
    );

    test('matrícula sin plan financiero devuelve configuración pendiente', () async {
      final db = container.read(demoDatabaseProvider);
      final pending = db.students.firstWhere((s) => s.status == EnrollmentStatus.pending);
      final report = await container
          .read(academicRepositoryProvider)
          .fetchGradeReport(studentId: pending.id, requester: UserRole.guardian);
      expect(report, isA<GradesPendingSetup>());
    });
  });

  group('caja', () {
    test('el pago es idempotente y el excedente queda como saldo a favor', () async {
      final finance = container.read(financeRepositoryProvider);
      final before = await finance.fetchStatement(DemoDatabase.siblingId);
      final open = before.openCharges;
      final request = PaymentRequest(
        studentId: DemoDatabase.siblingId,
        chargeIds: [open.first.id],
        amount: open.first.balance + 100,
        method: PaymentMethod.cash,
        reference: 'EFECTIVO',
        idempotencyKey: 'k-1',
      );
      final first = await finance.registerPayment(request, cashier: 'Caja');
      final retry = await finance.registerPayment(request, cashier: 'Caja');
      expect(retry.id, first.id);
      expect(first.creditBalance, 100);

      final after = await finance.fetchStatement(DemoDatabase.siblingId);
      expect(after.openCharges.length, open.length - 1);
      expect(after.creditBalance, 100);
      expect(after.payments.where((p) => p.id == first.id), hasLength(1));
    });

    test('valida monto y cargos', () async {
      final finance = container.read(financeRepositoryProvider);
      await expectLater(
        finance.previewAllocation(
          const PaymentRequest(
            studentId: DemoDatabase.siblingId,
            chargeIds: [],
            amount: 10,
            method: PaymentMethod.cash,
            reference: '',
            idempotencyKey: 'k-2',
          ),
        ),
        throwsA(isA<FinanceException>()),
      );
    });

    test('rechazar un comprobante exige motivo y no habilita notas', () async {
      final finance = container.read(financeRepositoryProvider);
      await expectLater(
        finance.reviewEvidence('evd-001', accept: false, reviewer: 'Caja', reason: ''),
        throwsA(isA<FinanceException>()),
      );
      await finance.reviewEvidence(
        'evd-001',
        accept: false,
        reviewer: 'Caja',
        reason: 'Imagen ilegible',
      );
      final report = await container
          .read(academicRepositoryProvider)
          .fetchGradeReport(studentId: DemoDatabase.siblingId, requester: UserRole.guardian);
      expect(report, isA<GradesBlocked>());
    });
  });

  group('aprendizaje y docencia', () {
    test('entregar una tarea una sola vez', () async {
      final academic = container.read(academicRepositoryProvider);
      final updated = await academic.submitAssignment(
        'asg-101',
        const SubmissionDraft(files: ['foto.jpg'], comment: ' listo '),
      );
      expect(updated.status, AssignmentStatus.submitted);
      expect(updated.submission!.comment, 'listo');
      await expectLater(
        academic.submitAssignment('asg-101', const SubmissionDraft(files: ['x.pdf'], comment: '')),
        throwsA(isA<StateError>()),
      );
    });

    test('la libreta publicada no se modifica y valida el rango', () async {
      final teaching = container.read(teachingRepositoryProvider);
      final published = await teaching.fetchGradebook('grp-1a', 1);
      await expectLater(teaching.saveGradebook(published), throwsA(isA<StateError>()));

      final open = await teaching.fetchGradebook('grp-1a', 3);
      final invalid = GradebookSheet(
        groupId: open.groupId,
        period: open.period,
        state: open.state,
        roster: open.roster,
        scores: {open.roster.first.id: 101},
        savedAt: open.savedAt,
      );
      await expectLater(teaching.saveGradebook(invalid), throwsA(isA<ArgumentError>()));
    });

    test('revisar la entrega de la estudiante actualiza su tarea', () async {
      final teaching = container.read(teachingRepositoryProvider);
      final db = container.read(demoDatabaseProvider);
      final reviewId = db.reviewToAssignment.keys.single;
      await teaching.reviewSubmission(reviewId, score: 9, feedback: '¡Muy bien!');
      expect((await teaching.fetchReviewItem(reviewId)).reviewed, isTrue);
      final task = await container
          .read(academicRepositoryProvider)
          .fetchAssignment(db.reviewToAssignment[reviewId]!);
      expect(task.status, AssignmentStatus.reviewed);
      expect(task.teacherFeedback, '¡Muy bien!');
    });

    test('la asistencia guardada se conserva', () async {
      final teaching = container.read(teachingRepositoryProvider);
      final sheet = await teaching.fetchAttendance('grp-1a', testNow);
      final marks = Map.of(sheet.marks)..[sheet.roster.first.id] = AttendanceMark.absent;
      await teaching.saveAttendance(
        AttendanceSheet(
          groupId: sheet.groupId,
          date: sheet.date,
          roster: sheet.roster,
          marks: marks,
          savedAt: null,
        ),
      );
      final again = await teaching.fetchAttendance('grp-1a', testNow);
      expect(again.marks[sheet.roster.first.id], AttendanceMark.absent);
      expect(again.savedAt, isNotNull);
    });
  });

  test('el modo de errores simulados produce un error de red', () async {
    container.read(simulateFailuresProvider.notifier).toggle(true);
    await expectLater(
      container.read(academicRepositoryProvider).fetchEvents(),
      throwsA(isA<DemoNetworkException>()),
    );
  });
}
