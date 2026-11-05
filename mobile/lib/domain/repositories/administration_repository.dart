import '../models/administration.dart';
import '../models/people.dart';

abstract interface class AdministrationRepository {
  Future<SchoolKpis> fetchKpis();

  Future<List<StudentSummary>> fetchDirectory();

  Future<StudentProfile> fetchStudentProfile(String studentId);

  Future<List<EnrollmentCase>> fetchEnrollments();

  Future<EnrollmentCase> fetchEnrollment(String caseId);

  Future<EnrollmentCase> advanceEnrollment(String caseId);

  Future<EnrollmentCase> resolveIssue(String caseId, String issue);

  Future<List<AccountSummary>> fetchAccounts();

  Future<ActivationCode> issueActivationCode(String accountId);

  Future<AccountSummary> setAccountActive(String accountId, {required bool active});
}
