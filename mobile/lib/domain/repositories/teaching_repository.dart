import '../models/teaching.dart';

abstract interface class TeachingRepository {
  Future<List<TeachingGroup>> fetchGroups(String teacherId);

  Future<TeachingGroup> fetchGroup(String groupId);

  Future<List<ClassSession>> fetchTodaySessions(String teacherId);

  Future<AttendanceSheet> fetchAttendance(String groupId, DateTime date);

  Future<AttendanceSheet> saveAttendance(AttendanceSheet sheet);

  Future<GradebookSheet> fetchGradebook(String groupId, int period);

  /// Guarda borrador de captura. No publica ni cierra el período.
  Future<GradebookSheet> saveGradebook(GradebookSheet sheet);

  Future<List<ReviewItem>> fetchReviewQueue(String teacherId);

  Future<ReviewItem> fetchReviewItem(String reviewId);

  Future<ReviewItem> reviewSubmission(
    String reviewId, {
    required double score,
    required String feedback,
  });
}
