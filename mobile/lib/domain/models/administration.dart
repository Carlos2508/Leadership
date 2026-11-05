import 'academics.dart';
import 'people.dart';

class MonthValue {
  const MonthValue({required this.label, required this.value});

  final String label;
  final double value;
}

class SchoolKpis {
  const SchoolKpis({
    required this.enrolledStudents,
    required this.newStudents,
    required this.sections,
    required this.teachers,
    required this.attendanceRate,
    required this.pendingEnrollments,
    required this.pendingLinks,
    required this.evidencesInReview,
    required this.monthlyAttendance,
  });

  final int enrolledStudents;
  final int newStudents;
  final int sections;
  final int teachers;
  final double attendanceRate;
  final int pendingEnrollments;
  final int pendingLinks;
  final int evidencesInReview;
  final List<MonthValue> monthlyAttendance;
}

class StudentProfile {
  const StudentProfile({
    required this.summary,
    required this.guardians,
    required this.attendance,
    required this.history,
    required this.hasPortalAccount,
  });

  final StudentSummary summary;
  final List<GuardianLink> guardians;
  final AttendanceSummary attendance;
  final List<EnrollmentHistoryEntry> history;
  final bool hasPortalAccount;
}

enum EnrollmentStage {
  application('Solicitud'),
  documents('Documentos'),
  financialPlan('Plan financiero'),
  validation('Validación'),
  active('Activa');

  const EnrollmentStage(this.label);

  final String label;

  EnrollmentStage? get next =>
      index + 1 < EnrollmentStage.values.length ? EnrollmentStage.values[index + 1] : null;
}

class EnrollmentCase {
  const EnrollmentCase({
    required this.id,
    required this.studentName,
    required this.gradeLabel,
    required this.guardianName,
    required this.stage,
    required this.submittedAt,
    required this.issues,
  });

  final String id;
  final String studentName;
  final String gradeLabel;
  final String guardianName;
  final EnrollmentStage stage;
  final DateTime submittedAt;

  /// Incidencias que impiden avanzar (documento faltante, plan sin asignar…).
  final List<String> issues;

  EnrollmentCase copyWith({EnrollmentStage? stage, List<String>? issues}) {
    return EnrollmentCase(
      id: id,
      studentName: studentName,
      gradeLabel: gradeLabel,
      guardianName: guardianName,
      stage: stage ?? this.stage,
      submittedAt: submittedAt,
      issues: issues ?? this.issues,
    );
  }
}

enum AccountState {
  active('Activa'),
  invited('Invitada'),
  inactive('Inactiva');

  const AccountState(this.label);

  final String label;
}

class AccountSummary {
  const AccountSummary({
    required this.id,
    required this.displayName,
    required this.username,
    required this.profileLabel,
    required this.state,
    this.lastSignIn,
  });

  final String id;
  final String displayName;
  final String username;
  final String profileLabel;
  final AccountState state;
  final DateTime? lastSignIn;

  AccountSummary copyWith({AccountState? state}) {
    return AccountSummary(
      id: id,
      displayName: displayName,
      username: username,
      profileLabel: profileLabel,
      state: state ?? this.state,
      lastSignIn: lastSignIn,
    );
  }
}

class ActivationCode {
  const ActivationCode({required this.code, required this.expiresAt});

  final String code;
  final DateTime expiresAt;
}
