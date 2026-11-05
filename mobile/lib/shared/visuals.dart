import 'package:flutter/material.dart';

import '../core/theme/app_colors.dart';
import '../domain/models/academics.dart';
import '../domain/models/administration.dart';
import '../domain/models/assignments.dart';
import '../domain/models/finance.dart';
import '../domain/models/grades.dart';
import '../domain/models/teaching.dart';

/// Correspondencias visuales del dominio (íconos y tonos). Reemplazan los
/// emojis del kit por un único set de íconos, como recomienda su guía.
extension SubjectAreaVisuals on SubjectArea {
  IconData get icon => switch (this) {
    SubjectArea.math => Icons.calculate_rounded,
    SubjectArea.science => Icons.science_rounded,
    SubjectArea.language => Icons.menu_book_rounded,
    SubjectArea.english => Icons.translate_rounded,
    SubjectArea.social => Icons.public_rounded,
    SubjectArea.technology => Icons.computer_rounded,
    SubjectArea.art => Icons.palette_rounded,
    SubjectArea.sports => Icons.sports_soccer_rounded,
  };

  AppTone get tone => switch (this) {
    SubjectArea.math => AppTone.blue,
    SubjectArea.science => AppTone.brand,
    SubjectArea.language => AppTone.purple,
    SubjectArea.english => AppTone.accent,
    SubjectArea.social => AppTone.coral,
    SubjectArea.technology => AppTone.blue,
    SubjectArea.art => AppTone.coral,
    SubjectArea.sports => AppTone.success,
  };
}

extension AssignmentStatusVisuals on AssignmentStatus {
  AppTone get tone => switch (this) {
    AssignmentStatus.pending => AppTone.accent,
    AssignmentStatus.submitted => AppTone.blue,
    AssignmentStatus.submittedLate => AppTone.warning,
    AssignmentStatus.reviewed => AppTone.success,
    AssignmentStatus.missing => AppTone.danger,
  };

  IconData get icon => switch (this) {
    AssignmentStatus.pending => Icons.schedule_rounded,
    AssignmentStatus.submitted => Icons.upload_file_rounded,
    AssignmentStatus.submittedLate => Icons.history_rounded,
    AssignmentStatus.reviewed => Icons.task_alt_rounded,
    AssignmentStatus.missing => Icons.block_rounded,
  };
}

extension EventCategoryVisuals on EventCategory {
  AppTone get tone => switch (this) {
    EventCategory.academic => AppTone.blue,
    EventCategory.activity => AppTone.purple,
    EventCategory.meeting => AppTone.accent,
    EventCategory.holiday => AppTone.success,
    EventCategory.deadline => AppTone.coral,
  };

  IconData get icon => switch (this) {
    EventCategory.academic => Icons.edit_note_rounded,
    EventCategory.activity => Icons.celebration_rounded,
    EventCategory.meeting => Icons.groups_rounded,
    EventCategory.holiday => Icons.beach_access_rounded,
    EventCategory.deadline => Icons.assignment_turned_in_rounded,
  };
}

extension ChargeStatusVisuals on ChargeStatus {
  AppTone get tone => switch (this) {
    ChargeStatus.paid => AppTone.success,
    ChargeStatus.partial => AppTone.accent,
    ChargeStatus.pending => AppTone.neutral,
    ChargeStatus.overdue => AppTone.danger,
    ChargeStatus.exempt => AppTone.blue,
  };
}

extension EvidenceStatusVisuals on EvidenceStatus {
  AppTone get tone => switch (this) {
    EvidenceStatus.inReview => AppTone.accent,
    EvidenceStatus.accepted => AppTone.success,
    EvidenceStatus.rejected => AppTone.danger,
  };
}

extension SubjectResultVisuals on SubjectResultStatus {
  AppTone get tone => switch (this) {
    SubjectResultStatus.approved => AppTone.success,
    SubjectResultStatus.failed => AppTone.danger,
    SubjectResultStatus.inProgress => AppTone.blue,
    SubjectResultStatus.recovered => AppTone.purple,
  };
}

extension AttendanceMarkVisuals on AttendanceMark {
  AppTone get tone => switch (this) {
    AttendanceMark.present => AppTone.success,
    AttendanceMark.late => AppTone.accent,
    AttendanceMark.absent => AppTone.danger,
    AttendanceMark.excused => AppTone.blue,
  };
}

extension EnrollmentStageVisuals on EnrollmentStage {
  AppTone get tone => switch (this) {
    EnrollmentStage.application => AppTone.neutral,
    EnrollmentStage.documents => AppTone.accent,
    EnrollmentStage.financialPlan => AppTone.purple,
    EnrollmentStage.validation => AppTone.blue,
    EnrollmentStage.active => AppTone.success,
  };
}

extension AccountStateVisuals on AccountState {
  AppTone get tone => switch (this) {
    AccountState.active => AppTone.success,
    AccountState.invited => AppTone.accent,
    AccountState.inactive => AppTone.neutral,
  };
}
