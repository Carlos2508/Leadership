import '../models/academics.dart';
import '../models/assignments.dart';
import '../models/grades.dart';
import '../models/people.dart';

abstract interface class AcademicRepository {
  Future<StudentSummary> fetchStudent(String studentId);

  Future<StudentHomeSummary> fetchHomeSummary(String studentId);

  Future<List<CourseProgress>> fetchCourses(String studentId);

  Future<CourseDetail> fetchCourse(String studentId, String subjectId);

  Future<List<Assignment>> fetchAssignments(String studentId);

  Future<Assignment> fetchAssignment(String assignmentId);

  /// Registra la entrega; el servidor marca si llegó tarde.
  Future<Assignment> submitAssignment(String assignmentId, SubmissionDraft draft);

  Future<List<SchoolEvent>> fetchEvents();

  /// Devuelve notas solo si identidad, publicación y solvencia lo permiten.
  Future<GradeReport> fetchGradeReport({required String studentId, required UserRole requester});

  Future<AttendanceSummary> fetchAttendance(String studentId);
}
