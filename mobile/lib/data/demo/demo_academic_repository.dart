import '../../domain/models/academics.dart';
import '../../domain/models/assignments.dart';
import '../../domain/models/finance.dart';
import '../../domain/models/grades.dart';
import '../../domain/models/people.dart';
import '../../domain/repositories/academic_repository.dart';
import 'demo_backend.dart';
import 'demo_database.dart';

class DemoAcademicRepository implements AcademicRepository {
  DemoAcademicRepository(this._db, this._backend);

  final DemoDatabase _db;
  final DemoBackend _backend;

  @override
  Future<StudentSummary> fetchStudent(String studentId) =>
      _backend.call(() => _db.student(studentId));

  @override
  Future<StudentHomeSummary> fetchHomeSummary(String studentId) {
    return _backend.call(() {
      final own = _db.assignments.where((a) => a.studentId == studentId);
      final weekAgo = _db.today.subtract(const Duration(days: 7));
      return StudentHomeSummary(
        student: _db.student(studentId),
        attendance: _db.attendance[studentId]!,
        pendingAssignments: own.where((a) => a.status.isOpen).length,
        completedThisWeek: own
            .where((a) => a.submission != null && a.submission!.submittedAt.isAfter(weekAgo))
            .length,
        streakDays: 5,
      );
    });
  }

  @override
  Future<List<CourseProgress>> fetchCourses(String studentId) =>
      _backend.call(() => List.unmodifiable(_db.courses[studentId] ?? const []));

  @override
  Future<CourseDetail> fetchCourse(String studentId, String subjectId) {
    return _backend.call(() {
      final progress = (_db.courses[studentId] ?? const <CourseProgress>[]).firstWhere(
        (c) => c.subject.id == subjectId,
      );
      final doneUnits = (progress.progress * 5).floor();
      const unitTitles = [
        'Unidad 1 · Fundamentos',
        'Unidad 2 · Exploración',
        'Unidad 3 · Práctica guiada',
        'Unidad 4 · Proyecto integrador',
        'Unidad 5 · Repaso y cierre',
      ];
      return CourseDetail(
        progress: progress,
        description:
            'Curso de ${progress.subject.name} con actividades semanales, recursos de apoyo y acompañamiento de ${progress.subject.teacherName}.',
        schedule: const ['Lunes · 7:30 a. m.', 'Miércoles · 9:00 a. m.', 'Viernes · 10:40 a. m.'],
        units: [
          for (var i = 0; i < unitTitles.length; i++)
            CourseUnit(title: unitTitles[i], done: i < doneUnits),
        ],
        resources: [
          CourseResource(
            title: 'Guía de estudio · ${progress.nextTopic}',
            kind: ResourceKind.document,
          ),
          const CourseResource(title: 'Video explicativo de la semana', kind: ResourceKind.video),
          const CourseResource(title: 'Ejercicios interactivos', kind: ResourceKind.link),
        ],
      );
    });
  }

  @override
  Future<List<Assignment>> fetchAssignments(String studentId) {
    return _backend.call(() {
      final list = _db.assignments.where((a) => a.studentId == studentId).toList()
        ..sort((a, b) => a.dueAt.compareTo(b.dueAt));
      return List.unmodifiable(list);
    });
  }

  @override
  Future<Assignment> fetchAssignment(String assignmentId) =>
      _backend.call(() => _db.assignments.firstWhere((a) => a.id == assignmentId));

  @override
  Future<Assignment> submitAssignment(String assignmentId, SubmissionDraft draft) {
    return _backend.call(() {
      final index = _db.assignments.indexWhere((a) => a.id == assignmentId);
      final current = _db.assignments[index];
      if (!current.status.isOpen) {
        throw StateError('La tarea ya fue entregada o está cerrada.');
      }
      if (draft.files.isEmpty) {
        throw ArgumentError('Adjunta al menos un archivo.');
      }
      final now = _db.now;
      final late = now.isAfter(current.dueAt);
      if (late && !current.allowsLate) {
        throw StateError('El plazo de entrega terminó.');
      }
      final updated = current.copyWith(
        status: late ? AssignmentStatus.submittedLate : AssignmentStatus.submitted,
        submission: Submission(
          submittedAt: now,
          files: List.unmodifiable(draft.files),
          comment: draft.comment.trim(),
          late: late,
        ),
      );
      _db.assignments[index] = updated;
      return updated;
    });
  }

  @override
  Future<List<SchoolEvent>> fetchEvents() {
    return _backend.call(() {
      final list = List.of(_db.events)..sort((a, b) => a.start.compareTo(b.start));
      return List.unmodifiable(list);
    });
  }

  @override
  Future<GradeReport> fetchGradeReport({required String studentId, required UserRole requester}) {
    return _backend.call(() {
      final clearance = _db.clearanceFor(studentId);
      switch (clearance.state) {
        case ClearanceState.pendingSetup:
          return const GradesPendingSetup();
        case ClearanceState.notCleared:
          final pendingAmount = _db.charges
              .where(
                (c) =>
                    c.studentId == studentId &&
                    c.countsForClearance &&
                    c.sequence <= DemoDatabase.requiredSequences &&
                    c.balance > 0,
              )
              .fold<double>(0, (sum, c) => sum + c.balance);
          final canSeeFinance = requester == UserRole.guardian;
          return GradesBlocked(
            publicationLabel: '2.º bimestre',
            canSeeFinance: canSeeFinance,
            pendingPeriods: canSeeFinance ? clearance.pendingPeriods : const [],
            pendingAmount: canSeeFinance ? pendingAmount : null,
            evidenceInReview: _db.evidences.any(
              (e) => e.studentId == studentId && e.status == EvidenceStatus.inReview,
            ),
          );
        case ClearanceState.cleared:
          final subjects = _db.grades[studentId];
          if (subjects == null) {
            return const GradesNotPublished(nextPublication: null);
          }
          return GradesAvailable(
            cycleLabel: 'Ciclo ${_db.today.year}',
            publishedPeriods: 2,
            publishedAt: _db.today.subtract(const Duration(days: 3)),
            subjects: List.unmodifiable(subjects),
            overallPartial: studentId == DemoDatabase.studentId ? 90.6 : 86.2,
            previousOverall: studentId == DemoDatabase.studentId ? 88.4 : 85.1,
          );
      }
    });
  }

  @override
  Future<AttendanceSummary> fetchAttendance(String studentId) =>
      _backend.call(() => _db.attendance[studentId]!);
}
