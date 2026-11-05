import 'dart:math';

import '../../domain/models/administration.dart';
import '../../domain/models/finance.dart';
import '../../domain/models/people.dart';
import '../../domain/repositories/administration_repository.dart';
import 'demo_backend.dart';
import 'demo_database.dart';

class DemoAdministrationRepository implements AdministrationRepository {
  DemoAdministrationRepository(this._db, this._backend);

  final DemoDatabase _db;
  final DemoBackend _backend;
  final Random _random = Random.secure();

  @override
  Future<SchoolKpis> fetchKpis() {
    return _backend.call(() {
      final active = _db.students.where((s) => s.status == EnrollmentStatus.active).length;
      final sections = _db.students.map((s) => s.placement).toSet().length;
      final pendingLinks = _db.guardians.values
          .expand((links) => links)
          .where((link) => !link.hasAccount)
          .length;
      final rates = _db.attendance.values.map((a) => a.rate).toList();
      return SchoolKpis(
        enrolledStudents: active,
        newStudents: _db.enrollments.where((e) => e.stage == EnrollmentStage.active).length,
        sections: sections,
        teachers: 9,
        attendanceRate: rates.isEmpty ? 0 : rates.reduce((a, b) => a + b) / rates.length,
        pendingEnrollments: _db.enrollments.where((e) => e.stage != EnrollmentStage.active).length,
        pendingLinks: pendingLinks,
        evidencesInReview: _db.evidences.where((e) => e.status == EvidenceStatus.inReview).length,
        monthlyAttendance: _db.monthlyAttendance,
      );
    });
  }

  @override
  Future<List<StudentSummary>> fetchDirectory() =>
      _backend.call(() => List.unmodifiable(_db.students));

  @override
  Future<StudentProfile> fetchStudentProfile(String studentId) {
    return _backend.call(() {
      final student = _db.student(studentId);
      final year = _db.today.year;
      final gradeNumber = int.tryParse(student.gradeLabel.substring(0, 1)) ?? 1;
      return StudentProfile(
        summary: student,
        guardians: _db.guardians[studentId] ?? const [],
        attendance: _db.attendance[studentId]!,
        hasPortalAccount: _db.studentsWithAccount.contains(studentId),
        history: [
          EnrollmentHistoryEntry(year: year, placement: student.placement, result: 'En curso'),
          if (gradeNumber > 1)
            EnrollmentHistoryEntry(
              year: year - 1,
              placement:
                  '${gradeNumber - 1}.º ${student.level == 'Básicos' ? 'Básico' : 'Primaria'} ${student.section}',
              result: 'Promovido',
            ),
        ],
      );
    });
  }

  @override
  Future<List<EnrollmentCase>> fetchEnrollments() {
    return _backend.call(() {
      final list = List.of(_db.enrollments)
        ..sort((a, b) {
          final byStage = a.stage.index.compareTo(b.stage.index);
          return byStage != 0 ? byStage : b.submittedAt.compareTo(a.submittedAt);
        });
      return List.unmodifiable(list);
    });
  }

  @override
  Future<EnrollmentCase> fetchEnrollment(String caseId) =>
      _backend.call(() => _db.enrollments.firstWhere((e) => e.id == caseId));

  @override
  Future<EnrollmentCase> advanceEnrollment(String caseId) {
    return _backend.call(() {
      final index = _db.enrollments.indexWhere((e) => e.id == caseId);
      final current = _db.enrollments[index];
      if (current.issues.isNotEmpty) {
        throw StateError('Resuelve las incidencias antes de avanzar.');
      }
      final next = current.stage.next;
      if (next == null) throw StateError('La matrícula ya está activa.');
      final updated = current.copyWith(stage: next);
      _db.enrollments[index] = updated;
      return updated;
    });
  }

  @override
  Future<EnrollmentCase> resolveIssue(String caseId, String issue) {
    return _backend.call(() {
      final index = _db.enrollments.indexWhere((e) => e.id == caseId);
      final current = _db.enrollments[index];
      final updated = current.copyWith(issues: current.issues.where((i) => i != issue).toList());
      _db.enrollments[index] = updated;
      return updated;
    });
  }

  @override
  Future<List<AccountSummary>> fetchAccounts() =>
      _backend.call(() => List.unmodifiable(_db.accounts));

  @override
  Future<ActivationCode> issueActivationCode(String accountId) {
    return _backend.call(() {
      final account = _db.accounts.firstWhere((a) => a.id == accountId);
      if (account.state == AccountState.inactive) {
        throw StateError('Activa la cuenta antes de generar un código.');
      }
      const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
      String block() => List.generate(4, (_) => alphabet[_random.nextInt(alphabet.length)]).join();
      final code = '${block()}-${block()}';
      final expiresAt = _db.now.add(const Duration(hours: 48));
      _db.activationCodes[code] = expiresAt;
      return ActivationCode(code: code, expiresAt: expiresAt);
    });
  }

  @override
  Future<AccountSummary> setAccountActive(String accountId, {required bool active}) {
    return _backend.call(() {
      final index = _db.accounts.indexWhere((a) => a.id == accountId);
      final current = _db.accounts[index];
      final updated = current.copyWith(state: active ? AccountState.active : AccountState.inactive);
      _db.accounts[index] = updated;
      return updated;
    });
  }
}
