import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/format/formatters.dart';
import '../../../core/providers.dart';
import '../../../data/repository_providers.dart';
import '../../../domain/models/teaching.dart';
import '../../academics/application/academic_providers.dart';
import '../../auth/application/auth_controller.dart';

String _teacherId(Ref ref) => ref.watch(requireSessionProvider).teacherId!;

final groupsProvider = FutureProvider.autoDispose<List<TeachingGroup>>(
  (ref) => ref.watch(teachingRepositoryProvider).fetchGroups(_teacherId(ref)),
);

final groupProvider = FutureProvider.autoDispose.family<TeachingGroup, String>(
  (ref, id) => ref.watch(teachingRepositoryProvider).fetchGroup(id),
);

final todaySessionsProvider = FutureProvider.autoDispose<List<ClassSession>>(
  (ref) => ref.watch(teachingRepositoryProvider).fetchTodaySessions(_teacherId(ref)),
);

final attendanceSheetProvider = FutureProvider.autoDispose
    .family<AttendanceSheet, ({String groupId, DateTime date})>(
      (ref, arg) => ref.watch(teachingRepositoryProvider).fetchAttendance(arg.groupId, arg.date),
    );

final gradebookProvider = FutureProvider.autoDispose
    .family<GradebookSheet, ({String groupId, int period})>(
      (ref, arg) => ref.watch(teachingRepositoryProvider).fetchGradebook(arg.groupId, arg.period),
    );

final reviewQueueProvider = FutureProvider.autoDispose<List<ReviewItem>>(
  (ref) => ref.watch(teachingRepositoryProvider).fetchReviewQueue(_teacherId(ref)),
);

final reviewItemProvider = FutureProvider.autoDispose.family<ReviewItem, String>(
  (ref, id) => ref.watch(teachingRepositoryProvider).fetchReviewItem(id),
);

/// Día de referencia para la asistencia (sin hora).
final todayProvider = Provider<DateTime>((ref) => dateOnly(ref.watch(clockProvider)()));

class TeachingActions {
  TeachingActions(this._ref);

  final Ref _ref;

  Future<AttendanceSheet> saveAttendance(AttendanceSheet sheet) async {
    final saved = await _ref.read(teachingRepositoryProvider).saveAttendance(sheet);
    _ref
      ..invalidate(attendanceSheetProvider((groupId: sheet.groupId, date: dateOnly(sheet.date))))
      ..invalidate(todaySessionsProvider);
    return saved;
  }

  Future<GradebookSheet> saveGradebook(GradebookSheet sheet) async {
    final saved = await _ref.read(teachingRepositoryProvider).saveGradebook(sheet);
    _ref.invalidate(gradebookProvider((groupId: sheet.groupId, period: sheet.period)));
    return saved;
  }

  Future<ReviewItem> review(
    String reviewId, {
    required double score,
    required String feedback,
  }) async {
    final saved = await _ref
        .read(teachingRepositoryProvider)
        .reviewSubmission(reviewId, score: score, feedback: feedback);
    _ref
      ..invalidate(reviewQueueProvider)
      ..invalidate(reviewItemProvider(reviewId))
      ..invalidate(assignmentsProvider)
      ..invalidate(assignmentProvider);
    return saved;
  }
}

final teachingActionsProvider = Provider<TeachingActions>(TeachingActions.new);
