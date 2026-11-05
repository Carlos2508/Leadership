import '../../core/format/formatters.dart';
import '../../domain/models/assignments.dart';
import '../../domain/models/teaching.dart';
import '../../domain/repositories/teaching_repository.dart';
import 'demo_backend.dart';
import 'demo_database.dart';

class DemoTeachingRepository implements TeachingRepository {
  DemoTeachingRepository(this._db, this._backend);

  final DemoDatabase _db;
  final DemoBackend _backend;

  @override
  Future<List<TeachingGroup>> fetchGroups(String teacherId) =>
      _backend.call(() => List.unmodifiable(_db.groups));

  @override
  Future<TeachingGroup> fetchGroup(String groupId) => _backend.call(() => _group(groupId));

  TeachingGroup _group(String id) => _db.groups.firstWhere((g) => g.id == id);

  @override
  Future<List<ClassSession>> fetchTodaySessions(String teacherId) {
    return _backend.call(() {
      const slots = [
        (7, 30, 'Fracciones equivalentes'),
        (9, 0, 'Problemas con fracciones'),
        (10, 40, 'Repaso de decimales'),
        (13, 0, 'Fórmulas en hojas de cálculo'),
      ];
      final today = _db.today;
      return [
        for (var i = 0; i < _db.groups.length; i++)
          () {
            final (hour, minute, topic) = slots[i];
            final start = DateTime(today.year, today.month, today.day, hour, minute);
            final group = _db.groups[i];
            return ClassSession(
              group: group,
              start: start,
              end: start.add(const Duration(minutes: 80)),
              topic: topic,
              attendanceTaken: _db.attendanceSheets.containsKey(_sheetKey(group.id, today)),
            );
          }(),
      ];
    });
  }

  String _sheetKey(String groupId, DateTime date) => '$groupId|${dateOnly(date).toIso8601String()}';

  @override
  Future<AttendanceSheet> fetchAttendance(String groupId, DateTime date) {
    return _backend.call(() {
      final existing = _db.attendanceSheets[_sheetKey(groupId, date)];
      if (existing != null) return existing;
      _group(groupId); // valida que el grupo exista
      final roster = _db.gradebooks['$groupId|1']!.roster;
      return AttendanceSheet(
        groupId: groupId,
        date: dateOnly(date),
        roster: roster,
        marks: {for (final s in roster) s.id: AttendanceMark.present},
        savedAt: null,
      );
    });
  }

  @override
  Future<AttendanceSheet> saveAttendance(AttendanceSheet sheet) {
    return _backend.call(() {
      final missing = sheet.roster.where((s) => !sheet.marks.containsKey(s.id));
      if (missing.isNotEmpty) {
        throw ArgumentError('Falta marcar a ${missing.length} estudiante(s).');
      }
      final saved = AttendanceSheet(
        groupId: sheet.groupId,
        date: dateOnly(sheet.date),
        roster: sheet.roster,
        marks: Map.unmodifiable(sheet.marks),
        savedAt: _db.now,
      );
      _db.attendanceSheets[_sheetKey(sheet.groupId, sheet.date)] = saved;
      return saved;
    });
  }

  @override
  Future<GradebookSheet> fetchGradebook(String groupId, int period) =>
      _backend.call(() => _db.gradebooks['$groupId|$period']!);

  @override
  Future<GradebookSheet> saveGradebook(GradebookSheet sheet) {
    return _backend.call(() {
      final key = '${sheet.groupId}|${sheet.period}';
      final current = _db.gradebooks[key]!;
      if (current.state == GradebookState.published) {
        throw StateError('Este período ya fue publicado; usa una corrección autorizada.');
      }
      for (final score in sheet.scores.values) {
        if (score != null && (score < 0 || score > 100)) {
          throw ArgumentError('Las notas deben estar entre 0 y 100.');
        }
      }
      final saved = GradebookSheet(
        groupId: current.groupId,
        period: current.period,
        state: current.state,
        roster: current.roster,
        scores: Map.unmodifiable(sheet.scores),
        savedAt: _db.now,
      );
      _db.gradebooks[key] = saved;
      return saved;
    });
  }

  @override
  Future<List<ReviewItem>> fetchReviewQueue(String teacherId) {
    return _backend.call(() {
      final list = List.of(_db.reviewQueue)
        ..sort((a, b) {
          if (a.reviewed != b.reviewed) return a.reviewed ? 1 : -1;
          return b.submittedAt.compareTo(a.submittedAt);
        });
      return List.unmodifiable(list);
    });
  }

  @override
  Future<ReviewItem> fetchReviewItem(String reviewId) =>
      _backend.call(() => _db.reviewQueue.firstWhere((r) => r.id == reviewId));

  @override
  Future<ReviewItem> reviewSubmission(
    String reviewId, {
    required double score,
    required String feedback,
  }) {
    return _backend.call(() {
      final index = _db.reviewQueue.indexWhere((r) => r.id == reviewId);
      final item = _db.reviewQueue[index];
      if (score < 0 || score > item.maxScore) {
        throw ArgumentError('El punteo debe estar entre 0 y ${item.maxScore}.');
      }
      final updated = item.copyWith(score: score, feedback: feedback.trim());
      _db.reviewQueue[index] = updated;

      final assignmentId = _db.reviewToAssignment[reviewId];
      if (assignmentId != null) {
        final i = _db.assignments.indexWhere((a) => a.id == assignmentId);
        if (i >= 0) {
          _db.assignments[i] = _db.assignments[i].copyWith(
            status: AssignmentStatus.reviewed,
            teacherFeedback: feedback.trim().isEmpty
                ? 'Tu docente revisó tu entrega.'
                : feedback.trim(),
          );
        }
      }
      return updated;
    });
  }
}
