import 'academics.dart';

enum AssignmentStatus {
  pending('Pendiente'),
  submitted('Entregada'),
  submittedLate('Entregada tarde'),
  reviewed('Revisada'),
  missing('No entregada');

  const AssignmentStatus(this.label);

  final String label;

  bool get isOpen => this == AssignmentStatus.pending;
  bool get isDone =>
      this == AssignmentStatus.submitted ||
      this == AssignmentStatus.submittedLate ||
      this == AssignmentStatus.reviewed;
}

class Submission {
  const Submission({
    required this.submittedAt,
    required this.files,
    required this.comment,
    required this.late,
  });

  final DateTime submittedAt;
  final List<String> files;
  final String comment;
  final bool late;
}

class Assignment {
  const Assignment({
    required this.id,
    required this.studentId,
    required this.subject,
    required this.title,
    required this.instructions,
    required this.publishedAt,
    required this.dueAt,
    required this.allowsLate,
    required this.status,
    this.submission,
    this.teacherFeedback,
  });

  final String id;
  final String studentId;
  final Subject subject;
  final String title;
  final String instructions;
  final DateTime publishedAt;
  final DateTime dueAt;
  final bool allowsLate;
  final AssignmentStatus status;
  final Submission? submission;

  /// Comentario del docente. El punteo pertenece a Calificaciones y queda
  /// sujeto al mismo control de solvencia, por eso no se incluye aquí.
  final String? teacherFeedback;

  Assignment copyWith({AssignmentStatus? status, Submission? submission, String? teacherFeedback}) {
    return Assignment(
      id: id,
      studentId: studentId,
      subject: subject,
      title: title,
      instructions: instructions,
      publishedAt: publishedAt,
      dueAt: dueAt,
      allowsLate: allowsLate,
      status: status ?? this.status,
      submission: submission ?? this.submission,
      teacherFeedback: teacherFeedback ?? this.teacherFeedback,
    );
  }
}

class SubmissionDraft {
  const SubmissionDraft({required this.files, required this.comment});

  final List<String> files;
  final String comment;
}
