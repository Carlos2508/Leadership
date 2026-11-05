/// Perfiles de experiencia de la app. Organizan la navegación; la autorización
/// real corresponde al servidor (ver docs/IDENTITY_AND_ACCESS.md).
enum UserRole {
  student('Estudiante'),
  guardian('Familia'),
  teacher('Docente'),
  admin('Administración'),
  finance('Caja');

  const UserRole(this.label);

  final String label;
}

class UserSession {
  const UserSession({
    required this.accountId,
    required this.displayName,
    required this.firstName,
    required this.role,
    required this.roleTitle,
    this.studentId,
    this.teacherId,
    this.linkedStudentIds = const [],
  });

  final String accountId;
  final String displayName;
  final String firstName;
  final UserRole role;

  /// Descripción visible del puesto o vínculo, p. ej. "Dirección".
  final String roleTitle;
  final String? studentId;
  final String? teacherId;

  /// Estudiantes vinculados a un encargado mediante relaciones aprobadas.
  final List<String> linkedStudentIds;

  String get initials => initialsOf(displayName);
}

enum EnrollmentStatus {
  active('Activo'),
  pending('En proceso'),
  withdrawn('Retirado');

  const EnrollmentStatus(this.label);

  final String label;
}

class StudentSummary {
  const StudentSummary({
    required this.id,
    required this.code,
    required this.firstName,
    required this.lastName,
    required this.gradeLabel,
    required this.section,
    required this.level,
    required this.status,
  });

  final String id;
  final String code;
  final String firstName;
  final String lastName;
  final String gradeLabel;
  final String section;
  final String level;
  final EnrollmentStatus status;

  String get fullName => '$firstName $lastName';
  String get initials => initialsOf(fullName);
  String get placement => '$gradeLabel $section';
}

class GuardianLink {
  const GuardianLink({
    required this.name,
    required this.relationship,
    required this.canViewGrades,
    required this.canViewFinance,
    required this.canPickUp,
    required this.hasAccount,
  });

  final String name;
  final String relationship;
  final bool canViewGrades;
  final bool canViewFinance;
  final bool canPickUp;
  final bool hasAccount;
}

class EnrollmentHistoryEntry {
  const EnrollmentHistoryEntry({required this.year, required this.placement, required this.result});

  final int year;
  final String placement;
  final String result;
}

String initialsOf(String name) {
  final parts = name
      .split(RegExp(r'\s+'))
      .where((part) => part.isNotEmpty && part[0].toUpperCase() != part[0].toLowerCase())
      .toList();
  if (parts.isEmpty) return '?';
  if (parts.length == 1) return parts.first[0].toUpperCase();
  return (parts.first[0] + parts[1][0]).toUpperCase();
}
