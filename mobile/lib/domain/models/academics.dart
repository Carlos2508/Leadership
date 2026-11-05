import 'people.dart';

enum SubjectArea { math, science, language, english, social, technology, art, sports }

class Subject {
  const Subject({
    required this.id,
    required this.name,
    required this.area,
    required this.teacherName,
  });

  final String id;
  final String name;
  final SubjectArea area;
  final String teacherName;
}

class CourseProgress {
  const CourseProgress({
    required this.subject,
    required this.progress,
    required this.completedActivities,
    required this.totalActivities,
    required this.nextTopic,
  });

  final Subject subject;

  /// Avance de actividades del período entre 0 y 1.
  final double progress;
  final int completedActivities;
  final int totalActivities;
  final String nextTopic;
}

class CourseUnit {
  const CourseUnit({required this.title, required this.done});

  final String title;
  final bool done;
}

enum ResourceKind { document, video, link }

class CourseResource {
  const CourseResource({required this.title, required this.kind});

  final String title;
  final ResourceKind kind;
}

class CourseDetail {
  const CourseDetail({
    required this.progress,
    required this.description,
    required this.schedule,
    required this.units,
    required this.resources,
  });

  final CourseProgress progress;
  final String description;
  final List<String> schedule;
  final List<CourseUnit> units;
  final List<CourseResource> resources;
}

enum EventCategory {
  academic('Académico'),
  activity('Actividad'),
  meeting('Reunión'),
  holiday('Asueto'),
  deadline('Entrega');

  const EventCategory(this.label);

  final String label;
}

class SchoolEvent {
  const SchoolEvent({
    required this.id,
    required this.title,
    required this.start,
    required this.category,
    required this.location,
    required this.description,
    this.end,
    this.allDay = false,
  });

  final String id;
  final String title;
  final DateTime start;
  final DateTime? end;
  final bool allDay;
  final EventCategory category;
  final String location;
  final String description;
}

class AttendanceSummary {
  const AttendanceSummary({
    required this.present,
    required this.absent,
    required this.late,
    required this.excused,
    required this.rate,
  });

  final int present;
  final int absent;
  final int late;
  final int excused;

  /// Porcentaje reportado por el servidor (0–100).
  final double rate;
}

class StudentHomeSummary {
  const StudentHomeSummary({
    required this.student,
    required this.attendance,
    required this.pendingAssignments,
    required this.completedThisWeek,
    required this.streakDays,
  });

  final StudentSummary student;
  final AttendanceSummary attendance;
  final int pendingAssignments;
  final int completedThisWeek;
  final int streakDays;
}
