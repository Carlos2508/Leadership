import 'dart:math';

import '../../domain/models/academics.dart';
import '../../domain/models/administration.dart';
import '../../domain/models/assignments.dart';
import '../../domain/models/communication.dart';
import '../../domain/models/finance.dart';
import '../../domain/models/grades.dart';
import '../../domain/models/people.dart';
import '../../domain/models/teaching.dart';

/// Base en memoria con datos **ficticios** que imita al servidor.
///
/// Todos los nombres, montos y resultados son sintéticos y se generan de forma
/// determinista a partir de la fecha actual. No representan personas reales
/// ni se usan fuera del prototipo.
class DemoDatabase {
  DemoDatabase({required this._clock}) {
    _seed();
  }

  final DateTime Function() _clock;
  final Random _random = Random(20261104);

  DateTime get now => _clock();
  late final DateTime today;

  // Identificadores de las cuentas demo.
  static const studentId = 'stu-001';
  static const siblingId = 'stu-002';
  static const teacherId = 'tea-001';
  static const guardianName = 'Marta Ramírez';
  static const requiredSequences = 5;

  final Map<String, Subject> subjects = {};
  final List<StudentSummary> students = [];
  final Map<String, List<GuardianLink>> guardians = {};
  final Set<String> studentsWithAccount = {};
  final Map<String, List<CourseProgress>> courses = {};
  final List<Assignment> assignments = [];
  final List<SchoolEvent> events = [];
  final Map<String, List<SubjectGrade>> grades = {};
  final Map<String, AttendanceSummary> attendance = {};
  final Map<UserRole, List<Notice>> notices = {};
  final List<Announcement> announcements = [];
  final List<Charge> charges = [];
  final List<Payment> payments = [];
  final Map<String, double> credits = {};
  final List<PaymentEvidence> evidences = [];
  final Map<String, Payment> paymentsByIdempotencyKey = {};
  final List<TeachingGroup> groups = [];
  final Map<String, AttendanceSheet> attendanceSheets = {};
  final Map<String, GradebookSheet> gradebooks = {};
  final List<ReviewItem> reviewQueue = [];
  final Map<String, String> reviewToAssignment = {};
  final List<EnrollmentCase> enrollments = [];
  final List<AccountSummary> accounts = [];
  final Map<String, DateTime> activationCodes = {};
  List<MonthValue> monthlyAttendance = const [];

  int _receiptCounter = 1040;
  int _idCounter = 1;

  String nextId(String prefix) => '$prefix-${(_idCounter++).toString().padLeft(4, '0')}';

  String nextReceiptNumber() => 'R-${today.year}-${(_receiptCounter++).toString().padLeft(5, '0')}';

  StudentSummary student(String id) => students.firstWhere((s) => s.id == id);

  DateTime _at(int dayOffset, [int hour = 0, int minute = 0]) =>
      DateTime(today.year, today.month, today.day + dayOffset, hour, minute);

  // ---------------------------------------------------------------------------
  // Reglas del servidor simulado (en producción las decide la API).
  // ---------------------------------------------------------------------------

  /// Solvencia para la publicación vigente: cuotas 1..N cubiertas por completo.
  Clearance clearanceFor(String studentId) {
    final own = charges.where((c) => c.studentId == studentId).toList();
    final tuition = own.where((c) => c.countsForClearance).toList();
    if (tuition.isEmpty) {
      return const Clearance(
        state: ClearanceState.pendingSetup,
        requirementLabel: 'Plan financiero pendiente de asignar',
        pendingPeriods: [],
      );
    }
    final pending = tuition
        .where((c) => c.sequence >= 1 && c.sequence <= requiredSequences && c.balance > 0)
        .map((c) => c.periodLabel)
        .toList();
    return Clearance(
      state: pending.isEmpty ? ClearanceState.cleared : ClearanceState.notCleared,
      requirementLabel: 'Cuotas 1 a $requiredSequences · publicación del 2.º bimestre',
      pendingPeriods: pending,
    );
  }

  /// Aplica un monto a cargos en el orden indicado. Devuelve aplicaciones y excedente.
  ({List<PaymentAllocation> allocations, double credit}) allocate(
    List<String> chargeIds,
    double amount,
  ) {
    var remaining = _round(amount);
    final allocations = <PaymentAllocation>[];
    for (final id in chargeIds) {
      if (remaining <= 0) break;
      final charge = charges.firstWhere((c) => c.id == id);
      if (charge.balance <= 0) continue;
      final applied = _round(min(charge.balance, remaining));
      allocations.add(PaymentAllocation(chargeId: id, label: charge.label, amount: applied));
      remaining = _round(remaining - applied);
    }
    return (allocations: allocations, credit: remaining);
  }

  void applyAllocations(List<PaymentAllocation> allocations) {
    for (final allocation in allocations) {
      final index = charges.indexWhere((c) => c.id == allocation.chargeId);
      final charge = charges[index];
      final balance = _round(charge.balance - allocation.amount);
      charges[index] = Charge(
        id: charge.id,
        studentId: charge.studentId,
        concept: charge.concept,
        periodLabel: charge.periodLabel,
        sequence: charge.sequence,
        amount: charge.amount,
        balance: balance,
        dueDate: charge.dueDate,
        status: _statusFor(charge.amount, balance, charge.dueDate),
        countsForClearance: charge.countsForClearance,
      );
    }
  }

  ChargeStatus _statusFor(double amount, double balance, DateTime due) {
    if (balance <= 0) return ChargeStatus.paid;
    if (balance < amount) return ChargeStatus.partial;
    return due.isBefore(today) ? ChargeStatus.overdue : ChargeStatus.pending;
  }

  static double _round(double value) => (value * 100).roundToDouble() / 100;

  void broadcastNotice(AnnouncementAudience audience, Notice notice) {
    final roles = switch (audience) {
      AnnouncementAudience.everyone => UserRole.values,
      AnnouncementAudience.families => [UserRole.guardian],
      AnnouncementAudience.students => [UserRole.student],
      AnnouncementAudience.teachers => [UserRole.teacher],
    };
    for (final role in roles) {
      notices[role] = [notice, ...?notices[role]];
    }
  }

  // ---------------------------------------------------------------------------
  // Semilla
  // ---------------------------------------------------------------------------

  void _seed() {
    final current = now;
    today = DateTime(current.year, current.month, current.day);
    _seedSubjects();
    _seedStudents();
    _seedCourses();
    _seedAssignments();
    _seedEvents();
    _seedGrades();
    _seedAttendance();
    _seedFinance();
    _seedNotices();
    _seedTeaching();
    _seedAdministration();
  }

  void _seedSubjects() {
    const list = [
      Subject(
        id: 'mat',
        name: 'Matemáticas',
        area: SubjectArea.math,
        teacherName: 'Profa. Andrea Morales',
      ),
      Subject(
        id: 'cie',
        name: 'Ciencias Naturales',
        area: SubjectArea.science,
        teacherName: 'Prof. Luis Herrera',
      ),
      Subject(
        id: 'com',
        name: 'Comunicación y Lenguaje',
        area: SubjectArea.language,
        teacherName: 'Profa. Carmen Ortiz',
      ),
      Subject(
        id: 'ing',
        name: 'Inglés',
        area: SubjectArea.english,
        teacherName: 'Prof. Kevin Juárez',
      ),
      Subject(
        id: 'soc',
        name: 'Ciencias Sociales',
        area: SubjectArea.social,
        teacherName: 'Profa. Rocío Méndez',
      ),
      Subject(
        id: 'tec',
        name: 'Tecnología',
        area: SubjectArea.technology,
        teacherName: 'Prof. Daniel Pineda',
      ),
      Subject(
        id: 'art',
        name: 'Expresión Artística',
        area: SubjectArea.art,
        teacherName: 'Profa. Silvia Rivas',
      ),
    ];
    for (final subject in list) {
      subjects[subject.id] = subject;
    }
  }

  void _seedStudents() {
    const firstNames = [
      'Valentina',
      'Mateo',
      'Camila',
      'Santiago',
      'Isabella',
      'Sebastián',
      'Lucía',
      'Emiliano',
      'Mariana',
      'Gabriel',
      'Daniela',
      'Andrés',
      'Ximena',
      'Javier',
      'Paula',
      'Nicolás',
      'Fernanda',
      'Alejandro',
      'Renata',
      'Pablo',
      'Regina',
      'Joaquín',
      'Julia',
      'Tomás',
      'Elena',
      'Rodrigo',
      'Victoria',
      'Adrián',
      'Carla',
      'Martín',
      'Abril',
      'Leonardo',
      'Natalia',
      'Hugo',
      'Alma',
      'Bruno',
      'Andrea',
      'Samuel',
      'Inés',
      'Iker',
    ];
    const lastNames = [
      'Morales',
      'Castillo',
      'Herrera',
      'Méndez',
      'Ortiz',
      'Juárez',
      'Pineda',
      'Rivas',
      'Cifuentes',
      'Barrios',
      'Aguilar',
      'Molina',
      'Estrada',
      'Figueroa',
      'Gálvez',
      'Solís',
      'Recinos',
      'Velásquez',
      'Arriaga',
      'Monterroso',
    ];
    const placements = [
      ('1.º Básico', 'A', 'Básicos', 8),
      ('1.º Básico', 'B', 'Básicos', 7),
      ('2.º Básico', 'A', 'Básicos', 8),
      ('3.º Básico', 'A', 'Básicos', 6),
      ('4.º Primaria', 'B', 'Primaria', 5),
      ('5.º Primaria', 'A', 'Primaria', 6),
    ];

    students
      ..add(
        const StudentSummary(
          id: studentId,
          code: 'EST-2026-001',
          firstName: 'Sofía',
          lastName: 'Ramírez',
          gradeLabel: '1.º Básico',
          section: 'A',
          level: 'Básicos',
          status: EnrollmentStatus.active,
        ),
      )
      ..add(
        const StudentSummary(
          id: siblingId,
          code: 'EST-2026-002',
          firstName: 'Diego',
          lastName: 'Ramírez',
          gradeLabel: '4.º Primaria',
          section: 'B',
          level: 'Primaria',
          status: EnrollmentStatus.active,
        ),
      );

    var n = 3;
    var nameIndex = 0;
    for (final (grade, section, level, count) in placements) {
      for (var i = 0; i < count; i++) {
        final first = firstNames[nameIndex % firstNames.length];
        final last = lastNames[(nameIndex * 7 + 3) % lastNames.length];
        nameIndex++;
        final status = n == 17
            ? EnrollmentStatus.pending
            : n == 29
            ? EnrollmentStatus.withdrawn
            : EnrollmentStatus.active;
        students.add(
          StudentSummary(
            id: 'stu-${n.toString().padLeft(3, '0')}',
            code: 'EST-2026-${n.toString().padLeft(3, '0')}',
            firstName: first,
            lastName: last,
            gradeLabel: grade,
            section: section,
            level: level,
            status: status,
          ),
        );
        n++;
      }
    }
    students.sort(
      (a, b) => a.lastName.compareTo(b.lastName) != 0
          ? a.lastName.compareTo(b.lastName)
          : a.firstName.compareTo(b.firstName),
    );

    for (final s in students) {
      final hasSecond = _random.nextBool();
      guardians[s.id] = s.lastName == 'Ramírez'
          ? const [
              GuardianLink(
                name: guardianName,
                relationship: 'Madre',
                canViewGrades: true,
                canViewFinance: true,
                canPickUp: true,
                hasAccount: true,
              ),
              GuardianLink(
                name: 'Jorge Ramírez',
                relationship: 'Padre',
                canViewGrades: true,
                canViewFinance: false,
                canPickUp: true,
                hasAccount: false,
              ),
            ]
          : [
              GuardianLink(
                name: '${_random.nextBool() ? 'Carmen' : 'Patricia'} ${s.lastName}',
                relationship: 'Madre',
                canViewGrades: true,
                canViewFinance: true,
                canPickUp: true,
                hasAccount: _random.nextInt(4) != 0,
              ),
              if (hasSecond)
                GuardianLink(
                  name: '${_random.nextBool() ? 'Luis' : 'Fernando'} ${s.lastName}',
                  relationship: 'Padre',
                  canViewGrades: true,
                  canViewFinance: _random.nextBool(),
                  canPickUp: true,
                  hasAccount: _random.nextBool(),
                ),
            ];
      if (_random.nextInt(5) != 0 || s.id == studentId) {
        studentsWithAccount.add(s.id);
      }
    }
  }

  void _seedCourses() {
    const topics = {
      'mat': 'Fracciones equivalentes',
      'cie': 'El ciclo del agua',
      'com': 'El cuento y sus partes',
      'ing': 'Present continuous',
      'soc': 'Regiones de Guatemala',
      'tec': 'Hojas de cálculo',
      'art': 'Color y contraste',
    };
    for (final id in [studentId, siblingId]) {
      courses[id] = [
        for (final subject in subjects.values)
          () {
            final total = 10 + _random.nextInt(6);
            final done = (total * (0.45 + _random.nextDouble() * 0.45)).round();
            return CourseProgress(
              subject: subject,
              progress: done / total,
              completedActivities: done,
              totalActivities: total,
              nextTopic: topics[subject.id]!,
            );
          }(),
      ];
    }
  }

  void _seedAssignments() {
    Assignment make(
      String id,
      String studentId,
      String subjectId,
      String title,
      String instructions,
      int dueOffset,
      AssignmentStatus status, {
      int dueHour = 23,
      bool allowsLate = true,
      String? feedback,
    }) {
      final due = _at(dueOffset, dueHour, 59);
      final submitted = status.isDone
          ? Submission(
              submittedAt: due.subtract(
                Duration(hours: status == AssignmentStatus.submittedLate ? -20 : 30),
              ),
              files: ['${title.toLowerCase().replaceAll(' ', '_')}.pdf'],
              comment: status == AssignmentStatus.reviewed ? 'Adjunto mi trabajo, ¡gracias!' : '',
              late: status == AssignmentStatus.submittedLate,
            )
          : null;
      return Assignment(
        id: id,
        studentId: studentId,
        subject: subjects[subjectId]!,
        title: title,
        instructions: instructions,
        publishedAt: due.subtract(const Duration(days: 7)),
        dueAt: due,
        allowsLate: allowsLate,
        status: status,
        submission: submitted,
        teacherFeedback: feedback,
      );
    }

    assignments.addAll([
      make(
        'asg-101',
        studentId,
        'cie',
        'Proyecto: el ciclo del agua',
        'Elabora una maqueta o un diagrama del ciclo del agua. Incluye evaporación, condensación, precipitación y escorrentía. Adjunta una foto clara de tu trabajo.',
        1,
        AssignmentStatus.pending,
        dueHour: 8,
      ),
      make(
        'asg-102',
        studentId,
        'com',
        'Mi cuento corto',
        'Escribe un cuento de una página con inicio, nudo y desenlace. Cuida la ortografía y usa al menos tres adjetivos por párrafo.',
        3,
        AssignmentStatus.pending,
      ),
      make(
        'asg-103',
        studentId,
        'mat',
        'Guía de fracciones equivalentes',
        'Resuelve los ejercicios 1 al 15 de la guía. Muestra el procedimiento de cada ejercicio.',
        5,
        AssignmentStatus.pending,
      ),
      make(
        'asg-104',
        studentId,
        'ing',
        'My daily routine',
        'Record a short audio (1 minute) describing your daily routine using present simple.',
        8,
        AssignmentStatus.pending,
      ),
      make(
        'asg-105',
        studentId,
        'mat',
        'Problemas de fracciones',
        'Resuelve los cinco problemas de la página 42 y explica tu razonamiento.',
        -2,
        AssignmentStatus.submitted,
      ),
      make(
        'asg-106',
        studentId,
        'soc',
        'Mapa de regiones',
        'Colorea el mapa e identifica las ocho regiones con su cabecera principal.',
        -6,
        AssignmentStatus.reviewed,
        feedback: '¡Excelente trabajo! Tu mapa está muy ordenado. Revisa la ubicación de la región nororiente.',
      ),
      make(
        'asg-107',
        studentId,
        'tec',
        'Presupuesto en hoja de cálculo',
        'Crea una tabla con ingresos y gastos de una semana y aplica la fórmula SUMA.',
        -9,
        AssignmentStatus.reviewed,
        feedback: 'Buen uso de fórmulas. Agrega formato de moneda la próxima vez.',
      ),
      make(
        'asg-108',
        studentId,
        'art',
        'Círculo cromático',
        'Pinta un círculo cromático con colores primarios, secundarios y terciarios.',
        -4,
        AssignmentStatus.submittedLate,
      ),
      make(
        'asg-109',
        studentId,
        'cie',
        'Cuestionario: estados de la materia',
        'Responde el cuestionario de diez preguntas del capítulo 3.',
        -12,
        AssignmentStatus.missing,
        allowsLate: false,
      ),
      make(
        'asg-201',
        siblingId,
        'mat',
        'Tablas de multiplicar del 6 al 9',
        'Completa las tablas y practica con el juego de la página 18.',
        2,
        AssignmentStatus.pending,
      ),
      make(
        'asg-202',
        siblingId,
        'com',
        'Lectura: El principito (cap. 1-3)',
        'Lee los capítulos y responde las preguntas de comprensión.',
        4,
        AssignmentStatus.pending,
      ),
      make(
        'asg-203',
        siblingId,
        'cie',
        'Las partes de la planta',
        'Dibuja una planta y señala raíz, tallo, hojas, flor y fruto.',
        -3,
        AssignmentStatus.reviewed,
        feedback: 'Muy buen dibujo, Diego. ¡Sigue así!',
      ),
      make(
        'asg-204',
        siblingId,
        'art',
        'Collage de texturas',
        'Crea un collage con al menos cinco texturas distintas.',
        -1,
        AssignmentStatus.submitted,
      ),
    ]);
  }

  void _seedEvents() {
    SchoolEvent event(
      String id,
      String title,
      int offset,
      int hour,
      EventCategory category,
      String location,
      String description, {
      int minute = 0,
      int durationMinutes = 60,
      bool allDay = false,
    }) {
      final start = _at(offset, hour, minute);
      return SchoolEvent(
        id: id,
        title: title,
        start: start,
        end: allDay ? null : start.add(Duration(minutes: durationMinutes)),
        allDay: allDay,
        category: category,
        location: location,
        description: description,
      );
    }

    events.addAll([
      event(
        'evt-01',
        'Feria de talentos',
        2,
        9,
        EventCategory.activity,
        'Gimnasio',
        'Presentaciones artísticas y musicales de todos los grados. Las familias están invitadas.',
        durationMinutes: 180,
      ),
      event(
        'evt-02',
        'Entrega de proyectos de Ciencias',
        1,
        8,
        EventCategory.deadline,
        'Laboratorio',
        'Entrega física de maquetas del ciclo del agua.',
      ),
      event(
        'evt-03',
        'Reunión de familias',
        8,
        15,
        EventCategory.meeting,
        'Salón de usos múltiples',
        'Revisión del avance del bimestre y orientación para el cierre del ciclo.',
        minute: 30,
        durationMinutes: 90,
      ),
      event(
        'evt-04',
        'Evaluación de Matemáticas',
        6,
        7,
        EventCategory.academic,
        'Aula 1.º Básico A',
        'Evaluación parcial de fracciones y decimales.',
        minute: 30,
        durationMinutes: 80,
      ),
      event(
        'evt-05',
        'Día del árbol',
        12,
        8,
        EventCategory.activity,
        'Jardín escolar',
        'Jornada de siembra con cada sección. Traer ropa cómoda.',
        durationMinutes: 150,
      ),
      event(
        'evt-06',
        'Asueto institucional',
        15,
        0,
        EventCategory.holiday,
        'Todo el colegio',
        'No hay actividades académicas.',
        allDay: true,
      ),
      event(
        'evt-07',
        'Excursión al museo',
        -5,
        8,
        EventCategory.activity,
        'Museo de Historia Natural',
        'Salida educativa de Ciencias Naturales.',
        durationMinutes: 240,
      ),
      event(
        'evt-08',
        'Cierre de captura del 3.er bimestre',
        20,
        17,
        EventCategory.academic,
        'Coordinación académica',
        'Último día para que docentes registren calificaciones del período.',
      ),
      event(
        'evt-09',
        'Taller de lectura en familia',
        4,
        16,
        EventCategory.meeting,
        'Biblioteca',
        'Estrategias para acompañar la lectura en casa.',
      ),
      event(
        'evt-10',
        'Torneo deportivo interno',
        25,
        9,
        EventCategory.activity,
        'Cancha principal',
        'Fútbol y baloncesto por secciones.',
        durationMinutes: 240,
      ),
    ]);
  }

  void _seedGrades() {
    SubjectGrade grade(String subjectId, double p1, double p2, double average) => SubjectGrade(
      subject: subjects[subjectId]!,
      periods: [p1, p2, null, null],
      partialAverage: average,
      status: SubjectResultStatus.inProgress,
    );

    // Valores publicados por el "servidor" (promedio parcial ya calculado).
    grades[studentId] = [
      grade('mat', 92, 96, 94),
      grade('cie', 85, 91, 88),
      grade('com', 88, 92, 90),
      grade('ing', 80, 86, 83),
      grade('soc', 90, 94, 92),
      grade('tec', 95, 97, 96),
      grade('art', 89, 93, 91),
    ];
    grades[siblingId] = [
      grade('mat', 78, 84, 81),
      grade('cie', 88, 90, 89),
      grade('com', 82, 80, 81),
      grade('ing', 75, 83, 79),
      grade('soc', 86, 88, 87),
      grade('tec', 92, 90, 91),
      grade('art', 95, 96, 95.5),
    ];
  }

  void _seedAttendance() {
    for (final s in students) {
      final absent = s.id == studentId ? 2 : _random.nextInt(6);
      final late = s.id == studentId ? 1 : _random.nextInt(4);
      final excused = _random.nextInt(2);
      const total = 82;
      final present = total - absent - late - excused;
      attendance[s.id] = AttendanceSummary(
        present: present,
        absent: absent,
        late: late,
        excused: excused,
        rate: ((present + late) / total * 1000).roundToDouble() / 10,
      );
    }
    monthlyAttendance = [
      for (var i = 5; i >= 0; i--)
        MonthValue(
          label: _monthAbbrev(DateTime(today.year, today.month - i)),
          value: 91 + _random.nextInt(7) + _random.nextDouble(),
        ),
    ];
  }

  void _seedFinance() {
    for (final s in students) {
      if (s.status == EnrollmentStatus.pending) continue; // sin plan asignado aún
      final tuition = s.level == 'Primaria' ? 550.0 : 650.0;
      final paidThrough = switch (s.id) {
        studentId => 6,
        siblingId => 4,
        _ => s.status == EnrollmentStatus.withdrawn ? 3 : 3 + _random.nextInt(4),
      };
      final inscription = Charge(
        id: 'chg-${s.id}-00',
        studentId: s.id,
        concept: 'Inscripción',
        periodLabel: '',
        sequence: 0,
        amount: 800,
        balance: 0,
        dueDate: _at(-160),
        status: ChargeStatus.paid,
        countsForClearance: false,
      );
      charges.add(inscription);
      _recordSeedPayment(s, [inscription], _at(-163, 10, 15));

      final paidCharges = <Charge>[];
      final quotas = s.status == EnrollmentStatus.withdrawn ? 4 : 10;
      for (var k = 1; k <= quotas; k++) {
        final due = _at((k - 6) * 30 + 5);
        final paid = k <= paidThrough;
        final charge = Charge(
          id: 'chg-${s.id}-${k.toString().padLeft(2, '0')}',
          studentId: s.id,
          concept: 'Colegiatura',
          periodLabel: 'Cuota ${k.toString().padLeft(2, '0')}',
          sequence: k,
          amount: tuition,
          balance: paid ? 0 : tuition,
          dueDate: due,
          status: paid ? ChargeStatus.paid : _statusFor(tuition, tuition, due),
          countsForClearance: true,
        );
        charges.add(charge);
        if (paid) paidCharges.add(charge);
      }
      // Un pago por cuota; las dos primeras se pagan juntas para mostrar un
      // pago aplicado a varias obligaciones. Ningún pago queda en el futuro.
      final latest = now.subtract(const Duration(hours: 2));
      var i = 0;
      while (i < paidCharges.length) {
        final size = i == 0 && paidCharges.length > 1 ? 2 : 1;
        final group = paidCharges.sublist(i, i + size);
        final planned = group.first.dueDate
            .subtract(Duration(days: 1 + _random.nextInt(6)))
            .add(Duration(hours: 8 + _random.nextInt(9)));
        _recordSeedPayment(s, group, planned.isAfter(latest) ? latest : planned);
        i += size;
      }
    }
    payments.sort((a, b) => b.paidAt.compareTo(a.paidAt));

    final sibling = student(siblingId);
    evidences.addAll([
      PaymentEvidence(
        id: 'evd-001',
        studentId: siblingId,
        studentName: sibling.fullName,
        submittedBy: guardianName,
        amount: 550,
        reference: 'TRF-88214',
        bank: 'Banco Ejemplo',
        paidOn: _at(-1),
        submittedAt: _at(-1, 19, 42),
        fileName: 'comprobante_cuota05.jpg',
        status: EvidenceStatus.inReview,
      ),
      for (final s in students.where((s) => s.id != studentId && s.id != siblingId).take(2))
        PaymentEvidence(
          id: nextId('evd'),
          studentId: s.id,
          studentName: s.fullName,
          submittedBy: guardians[s.id]!.first.name,
          amount: s.level == 'Primaria' ? 550 : 650,
          reference: 'DEP-${40000 + _random.nextInt(9999)}',
          bank: 'Banco Ejemplo',
          paidOn: _at(-2),
          submittedAt: _at(-2, 8 + _random.nextInt(10), 5),
          fileName: 'deposito.jpg',
          status: EvidenceStatus.inReview,
        ),
    ]);
  }

  void _recordSeedPayment(StudentSummary s, List<Charge> paid, DateTime when) {
    payments.add(
      Payment(
        id: nextId('pay'),
        receiptNumber: nextReceiptNumber(),
        studentId: s.id,
        studentName: s.fullName,
        amount: paid.fold(0, (sum, c) => sum + c.amount),
        method: PaymentMethod.values[_random.nextInt(PaymentMethod.values.length)],
        paidAt: when,
        reference: 'REF-${100000 + _random.nextInt(899999)}',
        allocations: [
          for (final c in paid) PaymentAllocation(chargeId: c.id, label: c.label, amount: c.amount),
        ],
        registeredBy: 'Caja',
      ),
    );
  }

  void _seedNotices() {
    Notice notice(
      String id,
      String title,
      String body,
      int dayOffset,
      int hour,
      String sender,
      NoticeChannel channel, {
      bool read = false,
      bool important = false,
    }) => Notice(
      id: id,
      title: title,
      body: body,
      sentAt: _at(dayOffset, hour, 10),
      sender: sender,
      channel: channel,
      read: read,
      important: important,
    );

    final shared = [
      notice(
        'ntc-01',
        'Semana cultural',
        '¡Te esperamos en las actividades del viernes! Habrá presentaciones, comida típica y muchas sorpresas.',
        0,
        7,
        'Dirección',
        NoticeChannel.school,
        important: true,
      ),
      notice(
        'ntc-02',
        'Reunión de familias',
        'Recordatorio del próximo encuentro de familias y docentes en el salón de usos múltiples.',
        -5,
        10,
        'Dirección',
        NoticeChannel.school,
        read: true,
      ),
    ];
    notices[UserRole.student] = [
      ...shared,
      notice(
        'ntc-11',
        'Guía de repaso de Ciencias',
        'La guía de repaso ya está disponible en tu curso de Ciencias Naturales. Revísala antes del jueves.',
        -1,
        15,
        'Prof. Luis Herrera',
        NoticeChannel.teacher,
      ),
      notice(
        'ntc-12',
        '¡Buen trabajo!',
        'Desbloqueaste una insignia por completar todas tus actividades de la semana.',
        -2,
        18,
        'Plataforma',
        NoticeChannel.system,
        read: true,
      ),
      notice(
        'ntc-13',
        'Tarea revisada: Mapa de regiones',
        'Tu docente dejó comentarios en tu entrega.',
        -4,
        13,
        'Profa. Rocío Méndez',
        NoticeChannel.teacher,
        read: true,
      ),
    ];
    notices[UserRole.guardian] = [
      ...shared,
      notice(
        'ntc-21',
        'Notas del 2.º bimestre publicadas',
        'Las calificaciones están disponibles en el portal para estudiantes con colegiaturas al día hasta la cuota 05.',
        -3,
        9,
        'Coordinación académica',
        NoticeChannel.school,
        important: true,
      ),
      notice(
        'ntc-22',
        'Comprobante recibido',
        'Recibimos tu comprobante de la cuota 05 de Diego. Caja lo revisará en un máximo de dos días hábiles.',
        -1,
        19,
        'Caja',
        NoticeChannel.system,
      ),
      notice(
        'ntc-23',
        'Excursión al museo',
        'Gracias por autorizar la salida educativa. Los estudiantes regresaron a las 12:30.',
        -5,
        13,
        'Prof. Luis Herrera',
        NoticeChannel.teacher,
        read: true,
      ),
    ];
    notices[UserRole.teacher] = [
      ...shared,
      notice(
        'ntc-31',
        'Cierre de captura',
        'La captura del 3.er bimestre cierra en 20 días. Recuerda guardar tus borradores.',
        -1,
        8,
        'Coordinación académica',
        NoticeChannel.school,
        important: true,
      ),
      notice(
        'ntc-32',
        'Nuevas entregas',
        'Tienes entregas nuevas de "Problemas de fracciones" por revisar.',
        0,
        6,
        'Plataforma',
        NoticeChannel.system,
      ),
    ];
    notices[UserRole.admin] = [
      ...shared,
      notice(
        'ntc-41',
        'Matrículas con incidencias',
        'Hay expedientes con documentos pendientes que requieren seguimiento.',
        0,
        7,
        'Secretaría',
        NoticeChannel.system,
      ),
    ];
    notices[UserRole.finance] = [
      ...shared,
      notice(
        'ntc-51',
        'Comprobantes en revisión',
        'Hay comprobantes enviados por familias pendientes de validar.',
        0,
        7,
        'Plataforma',
        NoticeChannel.system,
        important: true,
      ),
    ];

    announcements.addAll([
      Announcement(
        id: 'ann-01',
        title: 'Semana cultural',
        body: shared[0].body,
        audience: AnnouncementAudience.everyone,
        publishedAt: shared[0].sentAt,
        author: 'Dirección',
        recipients: 96,
        readCount: 41,
        important: true,
      ),
      Announcement(
        id: 'ann-02',
        title: 'Notas del 2.º bimestre publicadas',
        body: notices[UserRole.guardian]![2].body,
        audience: AnnouncementAudience.families,
        publishedAt: _at(-3, 9, 10),
        author: 'Coordinación académica',
        recipients: 58,
        readCount: 49,
        important: true,
      ),
      Announcement(
        id: 'ann-03',
        title: 'Reunión de familias',
        body: shared[1].body,
        audience: AnnouncementAudience.families,
        publishedAt: shared[1].sentAt,
        author: 'Dirección',
        recipients: 58,
        readCount: 52,
        important: false,
      ),
    ]);
  }

  void _seedTeaching() {
    final math = subjects['mat']!;
    final tech = subjects['tec']!;
    List<StudentSummary> roster(String grade, String section) => students
        .where(
          (s) =>
              s.gradeLabel == grade &&
              s.section == section &&
              s.status != EnrollmentStatus.withdrawn,
        )
        .toList();

    TeachingGroup group(String id, Subject subject, String grade, String section, String room) =>
        TeachingGroup(
          id: id,
          subject: subject,
          gradeLabel: grade,
          section: section,
          studentCount: roster(grade, section).length,
          room: room,
        );

    groups.addAll([
      group('grp-1a', math, '1.º Básico', 'A', 'Aula 12'),
      group('grp-1b', math, '1.º Básico', 'B', 'Aula 13'),
      group('grp-2a', math, '2.º Básico', 'A', 'Aula 21'),
      group('grp-3a', tech, '3.º Básico', 'A', 'Laboratorio de cómputo'),
    ]);

    for (final g in groups) {
      final members = roster(g.gradeLabel, g.section);
      for (var period = 1; period <= 4; period++) {
        final published = period <= 2;
        final scores = <String, double?>{};
        for (final s in members) {
          final seeded = grades[s.id]
              ?.where((grade) => grade.subject.id == g.subject.id)
              .firstOrNull
              ?.periods[period - 1];
          scores[s.id] = switch (period) {
            1 || 2 => seeded ?? (62 + _random.nextInt(37)).toDouble(),
            3 => _random.nextInt(3) == 0 ? null : (60 + _random.nextInt(39)).toDouble(),
            _ => null,
          };
        }
        gradebooks['${g.id}|$period'] = GradebookSheet(
          groupId: g.id,
          period: period,
          state: published ? GradebookState.published : GradebookState.open,
          roster: members,
          scores: scores,
          savedAt: period == 3 ? _at(-1, 18, 20) : (published ? _at(-30) : null),
        );
      }
    }

    final g1a = groups.first;
    final members = roster(g1a.gradeLabel, g1a.section);
    var i = 0;
    for (final s in [
      ...members.where((m) => m.id == studentId),
      ...members.where((m) => m.id != studentId),
    ].take(6)) {
      final isSofia = s.id == studentId;
      final id = 'rev-${(++i).toString().padLeft(2, '0')}';
      reviewQueue.add(
        ReviewItem(
          id: id,
          assignmentTitle: 'Problemas de fracciones',
          group: g1a,
          student: s,
          submittedAt: _at(-2 - _random.nextInt(2), 14 + _random.nextInt(6), _random.nextInt(59)),
          late: i == 4,
          files: ['problemas_fracciones_${s.firstName.toLowerCase()}.pdf'],
          comment: isSofia ? 'Profe, en el problema 4 usé un dibujo para explicar.' : '',
          maxScore: 10,
          score: i == 5 || i == 6 ? (7 + _random.nextInt(4)).toDouble() : null,
          feedback: i == 5 || i == 6 ? 'Buen procedimiento.' : null,
        ),
      );
      if (isSofia) reviewToAssignment[id] = 'asg-105';
    }
  }

  void _seedAdministration() {
    enrollments.addAll([
      EnrollmentCase(
        id: 'enr-01',
        studentName: 'Emma Cifuentes',
        gradeLabel: '1.º Primaria',
        guardianName: 'Laura Cifuentes',
        stage: EnrollmentStage.documents,
        submittedAt: _at(-6, 10),
        issues: const ['Falta certificado de nacimiento'],
      ),
      EnrollmentCase(
        id: 'enr-02',
        studentName: 'Lucas Barrios',
        gradeLabel: '2.º Básico',
        guardianName: 'Ricardo Barrios',
        stage: EnrollmentStage.financialPlan,
        submittedAt: _at(-9, 9),
        issues: const ['Plan financiero sin asignar'],
      ),
      EnrollmentCase(
        id: 'enr-03',
        studentName: 'Ana Belén Solís',
        gradeLabel: 'Preparatoria',
        guardianName: 'Mónica Solís',
        stage: EnrollmentStage.application,
        submittedAt: _at(-1, 16),
        issues: const [],
      ),
      EnrollmentCase(
        id: 'enr-04',
        studentName: 'Diego Estrada',
        gradeLabel: '3.º Básico',
        guardianName: 'Héctor Estrada',
        stage: EnrollmentStage.validation,
        submittedAt: _at(-14, 11),
        issues: const [],
      ),
      EnrollmentCase(
        id: 'enr-05',
        studentName: 'Camila Recinos',
        gradeLabel: '5.º Primaria',
        guardianName: 'Sandra Recinos',
        stage: EnrollmentStage.documents,
        submittedAt: _at(-4, 15),
        issues: const ['Constancia del colegio anterior pendiente', 'Encargado sin verificar'],
      ),
      EnrollmentCase(
        id: 'enr-06',
        studentName: 'Pedro Gálvez',
        gradeLabel: '1.º Básico',
        guardianName: 'Elisa Gálvez',
        stage: EnrollmentStage.active,
        submittedAt: _at(-30, 9),
        issues: const [],
      ),
    ]);

    accounts.addAll([
      AccountSummary(
        id: 'acc-01',
        displayName: 'Roberto Castillo',
        username: 'direccion.demo',
        profileLabel: 'Dirección',
        state: AccountState.active,
        lastSignIn: _at(0, 7, 2),
      ),
      AccountSummary(
        id: 'acc-02',
        displayName: 'Ana Lucía Pérez',
        username: 'caja.demo',
        profileLabel: 'Caja',
        state: AccountState.active,
        lastSignIn: _at(0, 7, 40),
      ),
      AccountSummary(
        id: 'acc-03',
        displayName: 'Andrea Morales',
        username: 'docente.demo',
        profileLabel: 'Docente',
        state: AccountState.active,
        lastSignIn: _at(-1, 18, 5),
      ),
      AccountSummary(
        id: 'acc-04',
        displayName: 'Luis Herrera',
        username: 'lherrera',
        profileLabel: 'Docente',
        state: AccountState.active,
        lastSignIn: _at(-2, 12),
      ),
      const AccountSummary(
        id: 'acc-05',
        displayName: 'Carmen Ortiz',
        username: 'cortiz',
        profileLabel: 'Docente',
        state: AccountState.invited,
      ),
      AccountSummary(
        id: 'acc-06',
        displayName: guardianName,
        username: 'familia.demo',
        profileLabel: 'Encargado',
        state: AccountState.active,
        lastSignIn: _at(-1, 20, 11),
      ),
      AccountSummary(
        id: 'acc-07',
        displayName: 'Sofía Ramírez',
        username: 'estudiante.demo',
        profileLabel: 'Estudiante',
        state: AccountState.active,
        lastSignIn: _at(0, 6, 55),
      ),
      const AccountSummary(
        id: 'acc-08',
        displayName: 'Jorge Ramírez',
        username: 'jramirez',
        profileLabel: 'Encargado',
        state: AccountState.invited,
      ),
      AccountSummary(
        id: 'acc-09',
        displayName: 'Patricia Molina',
        username: 'pmolina',
        profileLabel: 'Encargado',
        state: AccountState.inactive,
        lastSignIn: _at(-90, 10),
      ),
      AccountSummary(
        id: 'acc-10',
        displayName: 'Kevin Juárez',
        username: 'kjuarez',
        profileLabel: 'Docente',
        state: AccountState.active,
        lastSignIn: _at(-3, 9),
      ),
      const AccountSummary(
        id: 'acc-11',
        displayName: 'Mateo Aguilar',
        username: 'maguilar',
        profileLabel: 'Estudiante',
        state: AccountState.invited,
      ),
      AccountSummary(
        id: 'acc-12',
        displayName: 'Secretaría',
        username: 'secretaria',
        profileLabel: 'Secretaría',
        state: AccountState.active,
        lastSignIn: _at(0, 7, 30),
      ),
    ]);
  }

  String _monthAbbrev(DateTime date) {
    const names = [
      'ene',
      'feb',
      'mar',
      'abr',
      'may',
      'jun',
      'jul',
      'ago',
      'sep',
      'oct',
      'nov',
      'dic',
    ];
    return names[date.month - 1];
  }
}
