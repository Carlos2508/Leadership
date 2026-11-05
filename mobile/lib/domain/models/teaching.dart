import 'academics.dart';
import 'people.dart';

class TeachingGroup {
  const TeachingGroup({
    required this.id,
    required this.subject,
    required this.gradeLabel,
    required this.section,
    required this.studentCount,
    required this.room,
  });

  final String id;
  final Subject subject;
  final String gradeLabel;
  final String section;
  final int studentCount;
  final String room;

  String get placement => '$gradeLabel $section';
}

class ClassSession {
  const ClassSession({
    required this.group,
    required this.start,
    required this.end,
    required this.topic,
    required this.attendanceTaken,
  });

  final TeachingGroup group;
  final DateTime start;
  final DateTime end;
  final String topic;
  final bool attendanceTaken;
}

enum AttendanceMark {
  present('Presente', 'P'),
  late('Tarde', 'T'),
  absent('Ausente', 'A'),
  excused('Justificada', 'J');

  const AttendanceMark(this.label, this.short);

  final String label;
  final String short;
}

class AttendanceSheet {
  const AttendanceSheet({
    required this.groupId,
    required this.date,
    required this.roster,
    required this.marks,
    required this.savedAt,
  });

  final String groupId;
  final DateTime date;
  final List<StudentSummary> roster;
  final Map<String, AttendanceMark> marks;
  final DateTime? savedAt;
}

enum GradebookState {
  open('Captura abierta'),
  published('Publicado');

  const GradebookState(this.label);

  final String label;
}

class GradebookSheet {
  const GradebookSheet({
    required this.groupId,
    required this.period,
    required this.state,
    required this.roster,
    required this.scores,
    required this.savedAt,
  });

  final String groupId;
  final int period;
  final GradebookState state;
  final List<StudentSummary> roster;
  final Map<String, double?> scores;
  final DateTime? savedAt;
}

class ReviewItem {
  const ReviewItem({
    required this.id,
    required this.assignmentTitle,
    required this.group,
    required this.student,
    required this.submittedAt,
    required this.late,
    required this.files,
    required this.comment,
    required this.maxScore,
    this.score,
    this.feedback,
  });

  final String id;
  final String assignmentTitle;
  final TeachingGroup group;
  final StudentSummary student;
  final DateTime submittedAt;
  final bool late;
  final List<String> files;
  final String comment;
  final int maxScore;
  final double? score;
  final String? feedback;

  bool get reviewed => score != null;

  ReviewItem copyWith({double? score, String? feedback}) {
    return ReviewItem(
      id: id,
      assignmentTitle: assignmentTitle,
      group: group,
      student: student,
      submittedAt: submittedAt,
      late: late,
      files: files,
      comment: comment,
      maxScore: maxScore,
      score: score ?? this.score,
      feedback: feedback ?? this.feedback,
    );
  }
}
