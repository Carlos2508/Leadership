import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repository_providers.dart';
import '../../../domain/models/academics.dart';
import '../../../domain/models/assignments.dart';
import '../../../domain/models/grades.dart';
import '../../../domain/models/people.dart';
import '../../auth/application/auth_controller.dart';

final studentProvider = FutureProvider.autoDispose.family<StudentSummary, String>(
  (ref, id) => ref.watch(academicRepositoryProvider).fetchStudent(id),
);

final homeSummaryProvider = FutureProvider.autoDispose.family<StudentHomeSummary, String>(
  (ref, id) => ref.watch(academicRepositoryProvider).fetchHomeSummary(id),
);

final coursesProvider = FutureProvider.autoDispose.family<List<CourseProgress>, String>(
  (ref, id) => ref.watch(academicRepositoryProvider).fetchCourses(id),
);

final courseDetailProvider = FutureProvider.autoDispose
    .family<CourseDetail, ({String studentId, String subjectId})>(
      (ref, arg) => ref.watch(academicRepositoryProvider).fetchCourse(arg.studentId, arg.subjectId),
    );

final assignmentsProvider = FutureProvider.autoDispose.family<List<Assignment>, String>(
  (ref, studentId) => ref.watch(academicRepositoryProvider).fetchAssignments(studentId),
);

final assignmentProvider = FutureProvider.autoDispose.family<Assignment, String>(
  (ref, id) => ref.watch(academicRepositoryProvider).fetchAssignment(id),
);

final eventsProvider = FutureProvider.autoDispose<List<SchoolEvent>>(
  (ref) => ref.watch(academicRepositoryProvider).fetchEvents(),
);

final gradeReportProvider = FutureProvider.autoDispose
    .family<GradeReport, ({String studentId, UserRole requester})>(
      (ref, arg) => ref
          .watch(academicRepositoryProvider)
          .fetchGradeReport(studentId: arg.studentId, requester: arg.requester),
    );

final attendanceProvider = FutureProvider.autoDispose.family<AttendanceSummary, String>(
  (ref, id) => ref.watch(academicRepositoryProvider).fetchAttendance(id),
);

/// Estudiante cuyo espacio se muestra: el propio (estudiante) o el hijo elegido.
class SelectedChildController extends Notifier<String?> {
  @override
  String? build() {
    final session = ref.watch(authControllerProvider);
    if (session == null) return null;
    if (session.role == UserRole.student) return session.studentId;
    return session.linkedStudentIds.isEmpty ? null : session.linkedStudentIds.first;
  }

  void select(String studentId) {
    final session = ref.read(authControllerProvider);
    if (session != null && session.linkedStudentIds.contains(studentId)) {
      state = studentId;
    }
  }
}

final selectedChildProvider = NotifierProvider<SelectedChildController, String?>(
  SelectedChildController.new,
);

/// Acciones que modifican datos académicos e invalidan las consultas afectadas.
class AcademicActions {
  AcademicActions(this._ref);

  final Ref _ref;

  Future<Assignment> submitAssignment(Assignment assignment, SubmissionDraft draft) async {
    final updated = await _ref
        .read(academicRepositoryProvider)
        .submitAssignment(assignment.id, draft);
    _ref
      ..invalidate(assignmentsProvider(assignment.studentId))
      ..invalidate(assignmentProvider(assignment.id))
      ..invalidate(homeSummaryProvider(assignment.studentId));
    return updated;
  }
}

final academicActionsProvider = Provider<AcademicActions>(AcademicActions.new);
