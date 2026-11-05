import 'academics.dart';

/// Estado por materia calculado y publicado por el servidor. La app no
/// recalcula aprobación ni promedios (docs/ACADEMIC_RULES.md).
enum SubjectResultStatus {
  approved('Aprobada'),
  failed('Reprobada'),
  inProgress('En curso'),
  recovered('Recuperada');

  const SubjectResultStatus(this.label);

  final String label;
}

class SubjectGrade {
  const SubjectGrade({
    required this.subject,
    required this.periods,
    required this.partialAverage,
    required this.status,
  });

  final Subject subject;

  /// Notas publicadas por período; `null` significa pendiente, no cero.
  final List<double?> periods;

  /// Promedio parcial informado por el servidor sobre períodos publicados.
  final double? partialAverage;
  final SubjectResultStatus status;
}

/// Resultado de consultar calificaciones. El servidor decide si se pueden ver.
sealed class GradeReport {
  const GradeReport();
}

class GradesAvailable extends GradeReport {
  const GradesAvailable({
    required this.cycleLabel,
    required this.publishedPeriods,
    required this.publishedAt,
    required this.subjects,
    required this.overallPartial,
    required this.previousOverall,
  });

  final String cycleLabel;
  final int publishedPeriods;
  final DateTime publishedAt;
  final List<SubjectGrade> subjects;
  final double? overallPartial;
  final double? previousOverall;
}

/// Notas publicadas pero no disponibles por solvencia. No contiene notas.
class GradesBlocked extends GradeReport {
  const GradesBlocked({
    required this.publicationLabel,
    required this.canSeeFinance,
    required this.pendingPeriods,
    required this.pendingAmount,
    required this.evidenceInReview,
  });

  final String publicationLabel;
  final bool canSeeFinance;
  final List<String> pendingPeriods;
  final double? pendingAmount;
  final bool evidenceInReview;
}

/// La matrícula no tiene plan o calendario financiero validado.
class GradesPendingSetup extends GradeReport {
  const GradesPendingSetup();
}

class GradesNotPublished extends GradeReport {
  const GradesNotPublished({required this.nextPublication});

  final DateTime? nextPublication;
}
