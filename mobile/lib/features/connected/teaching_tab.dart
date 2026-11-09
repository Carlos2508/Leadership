import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/cards.dart';
import '../../core/widgets/states.dart';
import '../../shared/page_layout.dart';
import 'connected_session.dart';
import 'connected_shell.dart';
import 'school_api.dart';

/// Courses assigned to the signed-in teacher. Gradebook and attendance use the same endpoints as the web.
class TeachingTab extends ConsumerWidget {
  const TeachingTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(connectedControllerProvider)!;
    // Today's status per course (pending reviews, attendance) comes from the same dashboard as the web.
    final dashboard =
        ref.watch(apiGetProvider(ConnectedHomeTab.path)).value as Map<String, dynamic>?;
    final status = {
      for (final c
          in (dashboard?['teaching'] as List<dynamic>? ?? const []).cast<Map<String, dynamic>>())
        c['id'] as String: c,
    };
    return ConnectedTabPage(
      onRefresh: () async {
        ref.invalidate(apiGetProvider(ConnectedHomeTab.path));
        await ref.read(connectedControllerProvider.notifier).refresh();
      },
      children: [
        const PageIntro(
          eyebrow: 'Docencia',
          title: 'Mis cursos',
          subtitle: 'Toca un botón para ir directo a la asistencia o la libreta.',
        ),
        if (session.courses.isEmpty)
          const EmptyState(
            icon: Icons.menu_book_outlined,
            title: 'Sin cursos asignados',
            message: 'Dirección asigna los cursos desde la plataforma.',
          ),
        for (final course in session.courses)
          Padding(
            padding: const EdgeInsets.only(bottom: Spacing.md),
            child: CourseCard(course: course, status: status[course.id]),
          ),
      ],
    );
  }
}

/// Course with today's status and direct buttons to the two daily tasks of a teacher.
class CourseCard extends StatelessWidget {
  const CourseCard({required this.course, this.status, super.key});

  final TeachingCourse course;
  final Map<String, dynamic>? status;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final reviews = status?['pendingReviews'] as int? ?? 0;
    final recorded = status?['attendanceRecorded'] as int?;
    final weekday = DateTime.now().weekday <= DateTime.friday;
    final attendancePending = weekday && recorded == 0 && course.students > 0;
    void open(CourseSection section) => Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => CourseDetailPage(course: course, initial: section),
      ),
    );
    return AppCard(
      onTap: () => open(attendancePending ? CourseSection.attendance : CourseSection.grades),
      semanticLabel: '${course.subject}, ${course.groupLabel}',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              const ToneIcon(icon: Icons.menu_book_outlined, tone: AppTone.accent),
              const SizedBox(width: Spacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(course.subject, style: theme.textTheme.titleSmall),
                    Text(
                      '${course.groupLabel} · ${course.students} estudiantes',
                      style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (attendancePending || reviews > 0) ...[
            const SizedBox(height: Spacing.sm),
            Wrap(
              spacing: Spacing.sm,
              runSpacing: Spacing.xs,
              children: [
                if (attendancePending)
                  const StatusPill(label: 'Asistencia de hoy pendiente', tone: AppTone.accent),
                if (reviews > 0)
                  StatusPill(
                    label: '$reviews ${reviews == 1 ? 'entrega' : 'entregas'} por revisar',
                    tone: AppTone.blue,
                  ),
              ],
            ),
          ],
          const SizedBox(height: Spacing.md),
          Row(
            children: [
              Expanded(
                child: (attendancePending ? FilledButton.icon : OutlinedButton.icon)(
                  onPressed: () => open(CourseSection.attendance),
                  icon: const Icon(Icons.event_available, size: 18),
                  label: const Text('Asistencia'),
                ),
              ),
              const SizedBox(width: Spacing.sm),
              Expanded(
                child: (attendancePending ? OutlinedButton.icon : FilledButton.icon)(
                  onPressed: () => open(CourseSection.grades),
                  icon: const Icon(Icons.grade_outlined, size: 18),
                  label: const Text('Libreta'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

enum CourseSection { grades, attendance, tasks }

class CourseDetailPage extends ConsumerStatefulWidget {
  const CourseDetailPage({required this.course, this.initial = CourseSection.grades, super.key});

  final TeachingCourse course;
  final CourseSection initial;

  @override
  ConsumerState<CourseDetailPage> createState() => _CourseDetailPageState();
}

class _CourseDetailPageState extends ConsumerState<CourseDetailPage> {
  late CourseSection _section = widget.initial;
  late TeachingCourse _course = widget.course;

  @override
  Widget build(BuildContext context) {
    final course = _course;
    final courses = ref.watch(connectedControllerProvider)?.courses ?? const <TeachingCourse>[];
    return DetailPage(
      title: course.subject,
      children: [
        PageIntro(eyebrow: course.groupLabel, title: course.subject),
        if (courses.length > 1) ...[
          // Switch course without going back; the same tool stays open.
          DropdownButtonFormField<String>(
            initialValue: course.id,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'Cambiar de curso'),
            items: [
              for (final c in courses)
                DropdownMenuItem(
                  value: c.id,
                  child: Text('${c.subject} · ${c.groupLabel}', overflow: TextOverflow.ellipsis),
                ),
            ],
            onChanged: (id) {
              final next = courses.where((c) => c.id == id).firstOrNull;
              if (next != null) setState(() => _course = next);
            },
          ),
          const SizedBox(height: Spacing.md),
        ],
        SectionChips<CourseSection>(
          options: const {
            CourseSection.attendance: ('Asistencia', Icons.event_available),
            CourseSection.grades: ('Libreta', Icons.grade_outlined),
            CourseSection.tasks: ('Tareas', Icons.assignment_outlined),
          },
          selected: _section,
          onSelected: (v) => setState(() => _section = v),
        ),
        const SizedBox(height: Spacing.lg),
        KeyedSubtree(
          key: ValueKey('${course.id}-$_section'),
          child: switch (_section) {
            CourseSection.grades => GradebookView(courseId: course.id),
            CourseSection.attendance => AttendanceView(courseId: course.id),
            CourseSection.tasks => _TasksView(courseId: course.id),
          },
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------- Libreta

class GradebookView extends ConsumerStatefulWidget {
  const GradebookView({required this.courseId, super.key});

  final String courseId;

  @override
  ConsumerState<GradebookView> createState() => _GradebookViewState();
}

class _Row {
  _Row(Map<String, dynamic> r)
    : studentId = r['studentId'] as String,
      name = r['name'] as String,
      code = r['code'] as String?,
      version = r['version'] as String?,
      originalStatus = (r['status'] as String?) ?? '',
      originalScore = r['score'] == null ? '' : '${r['score']}' {
    status = originalStatus;
    score = TextEditingController(text: originalScore);
  }

  final String studentId;
  final String name;
  final String? code;
  final String? version;
  final String originalStatus;
  final String originalScore;
  late String status;
  late final TextEditingController score;

  bool get changed =>
      status != originalStatus || (status == 'Scored' && score.text.trim() != originalScore);

  /// Same rule as the server: 0–100 with up to two decimals.
  bool get invalid {
    if (status != 'Scored') return false;
    final value = num.tryParse(score.text.trim().replaceAll(',', '.'));
    return value == null || value < 0 || value > 100 || (value * 100).round() != value * 100;
  }
}

class _GradebookViewState extends ConsumerState<GradebookView> {
  String? _periodId;
  Object? _loadedFor;
  List<_Row> _rows = [];
  final _reason = TextEditingController();
  bool _busy = false;
  String? _error;

  String get _path =>
      '/api/v1/courses/${widget.courseId}/grades${_periodId == null ? '' : '?periodId=$_periodId'}';

  @override
  void dispose() {
    for (final r in _rows) {
      r.score.dispose();
    }
    _reason.dispose();
    super.dispose();
  }

  void _bind(Map<String, dynamic> data) {
    if (identical(_loadedFor, data)) return;
    _loadedFor = data;
    for (final r in _rows) {
      r.score.dispose();
    }
    _rows = [for (final r in (data['rows'] as List<dynamic>).cast<Map<String, dynamic>>()) _Row(r)];
  }

  Future<void> _save(Map<String, dynamic> data) async {
    final changed = _rows.where((r) => r.changed).toList();
    if (changed.isEmpty) return;
    final wrong = changed.where((r) => r.invalid).toList();
    if (wrong.isNotEmpty) {
      // Name the students: on a phone the marked boxes are usually out of sight.
      final who =
          wrong.take(3).map((r) => r.name).join(', ') +
          (wrong.length > 3 ? ' y ${wrong.length - 3} más' : '');
      setState(
        () => _error = 'Revisa la nota de $who: debe estar entre 0 y 100, con hasta dos decimales.',
      );
      return;
    }
    final requiresReason = data['requiresReason'] == true;
    if (requiresReason && _reason.text.trim().length < 8) {
      setState(() => _error = 'Escribe el motivo de la corrección (al menos 8 caracteres).');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(connectedControllerProvider)!.api.post(
        '/api/v1/courses/${widget.courseId}/grades',
        {
          'periodId': (data['period'] as Map<String, dynamic>)['id'],
          'entries': [
            for (final r in changed)
              {
                'studentId': r.studentId,
                'score': r.status == 'Scored'
                    ? num.parse(r.score.text.trim().replaceAll(',', '.'))
                    : null,
                'status': r.status,
                'version': r.version,
              },
          ],
          'reason': requiresReason ? _reason.text.trim() : null,
        },
      );
      _reason.clear();
      ref.invalidate(apiGetProvider(_path));
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('Notas guardadas (${changed.length}).')));
      }
    } on ApiFailure catch (e) {
      setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return ApiBuilder(
      path: _path,
      builder: (context, raw) {
        final data = raw as Map<String, dynamic>;
        _bind(data);
        final period = data['period'] as Map<String, dynamic>;
        final periods = (data['periods'] as List<dynamic>).cast<Map<String, dynamic>>();
        final canEdit = data['canEdit'] == true;
        final dirty = _rows.any((r) => r.changed);
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  for (final p in periods)
                    Padding(
                      padding: const EdgeInsets.only(right: Spacing.sm),
                      child: ChoiceChip(
                        label: Text('${p['name']}${p['published'] == true ? ' · publicado' : ''}'),
                        selected: p['id'] == period['id'],
                        onSelected: _busy || dirty
                            ? null
                            : (_) => setState(() {
                                _periodId = p['id'] as String;
                                _error = null;
                              }),
                      ),
                    ),
                ],
              ),
            ),
            const SizedBox(height: Spacing.md),
            if (period['published'] == true)
              Padding(
                padding: const EdgeInsets.only(bottom: Spacing.md),
                child: StatusPill(
                  label: canEdit
                      ? 'Publicado: los cambios quedan como corrección con motivo.'
                      : 'Publicado: solo coordinación puede corregir.',
                  tone: AppTone.warning,
                  icon: Icons.info_outline,
                ),
              ),
            if (_rows.isEmpty)
              const EmptyState(icon: Icons.people_outline, title: 'Sin estudiantes en el curso'),
            for (final r in _rows)
              Padding(
                padding: const EdgeInsets.only(bottom: Spacing.sm),
                child: AppCard(
                  padding: const EdgeInsets.symmetric(horizontal: Spacing.md, vertical: Spacing.sm),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          Expanded(child: Text(r.name, style: theme.textTheme.titleSmall)),
                          if (r.code != null)
                            Text(
                              r.code!,
                              style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                            ),
                        ],
                      ),
                      const SizedBox(height: Spacing.xs),
                      Row(
                        children: [
                          Expanded(
                            child: Semantics(
                              label: 'Estado de ${r.name}',
                              child: DropdownButton<String>(
                                value: r.status,
                                isExpanded: true,
                                underline: const SizedBox.shrink(),
                                onChanged: !canEdit || _busy
                                    ? null
                                    : (v) => setState(() => r.status = v ?? ''),
                                items: const [
                                  DropdownMenuItem(value: '', child: Text('Sin registrar')),
                                  DropdownMenuItem(value: 'Scored', child: Text('Con nota')),
                                  DropdownMenuItem(
                                    value: 'Missing',
                                    child: Text('Pendiente de entregar'),
                                  ),
                                  DropdownMenuItem(value: 'Excused', child: Text('Exonerado')),
                                ],
                              ),
                            ),
                          ),
                          const SizedBox(width: Spacing.md),
                          SizedBox(
                            width: 84,
                            child: Semantics(
                              label: 'Nota de ${r.name}',
                              child: TextField(
                                controller: r.score,
                                enabled:
                                    canEdit && !_busy && (r.status == 'Scored' || r.status == ''),
                                textAlign: TextAlign.center,
                                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                                inputFormatters: [
                                  FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]')),
                                ],
                                onChanged: (_) => setState(() {
                                  if (r.status == '') r.status = 'Scored';
                                }),
                                decoration: InputDecoration(
                                  isDense: true,
                                  hintText: '—',
                                  errorText: r.invalid && r.changed ? '' : null,
                                  errorStyle: const TextStyle(height: 0, fontSize: 0),
                                ),
                                style: theme.textTheme.titleMedium,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            if (canEdit && data['requiresReason'] == true && dirty) ...[
              const SizedBox(height: Spacing.sm),
              TextField(
                controller: _reason,
                enabled: !_busy,
                decoration: const InputDecoration(labelText: 'Motivo de la corrección'),
              ),
            ],
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: Spacing.sm),
                child: Text(
                  _error!,
                  style: TextStyle(color: theme.colorScheme.error, fontWeight: FontWeight.w600),
                ),
              ),
            if (canEdit) ...[
              const SizedBox(height: Spacing.md),
              FilledButton.icon(
                onPressed: _busy || !dirty ? null : () => _save(data),
                icon: const Icon(Icons.save_outlined),
                label: Text(_busy ? 'Guardando…' : 'Guardar notas'),
              ),
            ],
          ],
        );
      },
    );
  }
}

// ---------------------------------------------------------------- Asistencia

class AttendanceView extends ConsumerStatefulWidget {
  const AttendanceView({required this.courseId, super.key});

  final String courseId;

  @override
  ConsumerState<AttendanceView> createState() => _AttendanceViewState();
}

class _AttendanceViewState extends ConsumerState<AttendanceView> {
  DateTime _date = DateTime.now();
  final Map<String, String> _draft = {};
  // The response object the draft was built from; a reload (new object) rebuilds the draft from the server.
  Object? _loadedFor;
  bool _busy = false;
  String? _error;

  String get _iso => DateFormat('yyyy-MM-dd').format(_date);
  String get _path => '/api/v1/courses/${widget.courseId}/attendance?date=$_iso';

  static const _labels = {
    'Present': ('Pres.', 'Presente'),
    'Absent': ('Aus.', 'Ausente'),
    'Late': ('Tarde', 'Llegó tarde'),
    'Excused': ('Just.', 'Justificado'),
  };

  Future<void> _save(List<Map<String, dynamic>> rows) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(connectedControllerProvider)!.api.post(
        '/api/v1/courses/${widget.courseId}/attendance',
        {
          'date': _iso,
          'entries': [
            for (final r in rows)
              if (_draft[r['studentId']] != null)
                {
                  'studentId': r['studentId'],
                  'status': _draft[r['studentId']],
                  'note': r['note'] ?? '',
                  'version': r['version'],
                },
          ],
        },
      );
      ref.invalidate(apiGetProvider(_path));
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('Asistencia guardada.')));
      }
    } on ApiFailure catch (e) {
      setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        OutlinedButton.icon(
          onPressed: _busy
              ? null
              : () async {
                  final picked = await showDatePicker(
                    context: context,
                    initialDate: _date,
                    firstDate: DateTime(_date.year - 1),
                    lastDate: DateTime.now(),
                  );
                  if (picked != null) {
                    setState(() {
                      _date = picked;
                      _draft.clear();
                      _loadedFor = null;
                    });
                  }
                },
          icon: const Icon(Icons.calendar_today_outlined),
          label: Text(DateFormat("EEEE d 'de' MMMM", 'es').format(_date)),
        ),
        const SizedBox(height: Spacing.md),
        ApiBuilder(
          path: _path,
          builder: (context, raw) {
            final data = raw as Map<String, dynamic>;
            final rows = (data['rows'] as List<dynamic>).cast<Map<String, dynamic>>();
            if (!identical(_loadedFor, raw)) {
              _loadedFor = raw;
              _draft
                ..clear()
                ..addAll({
                  for (final r in rows)
                    if (r['status'] != null) r['studentId'] as String: r['status'] as String,
                });
            }
            final canEdit = data['canEdit'] == true;
            final recorded = rows.where((r) => _draft[r['studentId']] != null).length;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  '$recorded de ${rows.length} registrados',
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
                const SizedBox(height: Spacing.sm),
                for (final r in rows)
                  Padding(
                    padding: const EdgeInsets.only(bottom: Spacing.sm),
                    child: AppCard(
                      padding: const EdgeInsets.symmetric(
                        horizontal: Spacing.md,
                        vertical: Spacing.sm,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Text(r['name'] as String, style: theme.textTheme.titleSmall),
                          const SizedBox(height: Spacing.sm),
                          SegmentedButton<String>(
                            showSelectedIcon: false,
                            emptySelectionAllowed: true,
                            expandedInsets: EdgeInsets.zero,
                            style: const ButtonStyle(visualDensity: VisualDensity.compact),
                            segments: [
                              for (final e in _labels.entries)
                                ButtonSegment(
                                  value: e.key,
                                  label: Text(e.value.$1),
                                  tooltip: e.value.$2,
                                ),
                            ],
                            selected: {if (_draft[r['studentId']] != null) _draft[r['studentId']]!},
                            onSelectionChanged: !canEdit || _busy
                                ? null
                                : (v) => setState(() {
                                    if (v.isEmpty) {
                                      _draft.remove(r['studentId']);
                                    } else {
                                      _draft[r['studentId'] as String] = v.first;
                                    }
                                  }),
                          ),
                        ],
                      ),
                    ),
                  ),
                if (_error != null)
                  Text(
                    _error!,
                    style: TextStyle(color: theme.colorScheme.error, fontWeight: FontWeight.w600),
                  ),
                if (canEdit) ...[
                  const SizedBox(height: Spacing.sm),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed: _busy
                              ? null
                              : () => setState(() {
                                  for (final r in rows) {
                                    _draft.putIfAbsent(r['studentId'] as String, () => 'Present');
                                  }
                                }),
                          child: const Text('Resto presentes'),
                        ),
                      ),
                      const SizedBox(width: Spacing.md),
                      Expanded(
                        child: FilledButton(
                          onPressed: _busy || recorded == 0 ? null : () => _save(rows),
                          child: Text(_busy ? 'Guardando…' : 'Guardar'),
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            );
          },
        ),
      ],
    );
  }
}

// ---------------------------------------------------------------- Tareas

class _TasksView extends StatelessWidget {
  const _TasksView({required this.courseId});

  final String courseId;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return ApiBuilder(
      path: '/api/v1/courses/$courseId/assignments',
      builder: (context, data) {
        final items = (data as List<dynamic>).cast<Map<String, dynamic>>();
        if (items.isEmpty) {
          return const EmptyState(
            icon: Icons.assignment_outlined,
            title: 'Sin tareas',
            message: 'Crea y publica tareas desde la web; aquí verás su avance.',
          );
        }
        return Column(
          children: [
            for (final t in items)
              Padding(
                padding: const EdgeInsets.only(bottom: Spacing.md),
                child: AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(t['title'] as String, style: theme.textTheme.titleSmall),
                          ),
                          StatusPill(
                            label:
                                const {
                                  'Published': 'Publicada',
                                  'Draft': 'Borrador',
                                  'Closed': 'Cerrada',
                                }[t['status']] ??
                                '${t['status']}',
                            tone: t['status'] == 'Published' ? AppTone.success : AppTone.neutral,
                          ),
                        ],
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '${t['submitted']} de ${t['recipients']} entregas · '
                        '${t['dueAt'] == null ? 'sin fecha' : 'vence ${shortDate(t['dueAt'] as String)}'}',
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                      const SizedBox(height: Spacing.sm),
                      ProgressBar(
                        value: (t['recipients'] as num) == 0
                            ? 0
                            : (t['submitted'] as num) / (t['recipients'] as num),
                      ),
                    ],
                  ),
                ),
              ),
          ],
        );
      },
    );
  }
}
