import 'package:flutter/material.dart';
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

/// Same arithmetic as the server and the web: period i starts at floor(i·days/count); the last ends with the cycle.
List<(DateTime, DateTime)> splitCalendar(DateTime start, DateTime end, int count) {
  final s = DateTime.utc(start.year, start.month, start.day);
  final days = DateTime.utc(end.year, end.month, end.day).difference(s).inDays + 1;
  if (count < 1 || days < count) return const [];
  return [
    for (var i = 0; i < count; i++)
      (
        s.add(Duration(days: i * days ~/ count)),
        i == count - 1
            ? DateTime.utc(end.year, end.month, end.day)
            : s.add(Duration(days: (i + 1) * days ~/ count - 1)),
      ),
  ];
}

/// «2026» → «2027», «Ciclo 2026» → «Ciclo 2027»; other labels are left for the user to write.
String nextCycleLabel(String label) {
  final year = RegExp(r'\b(19|20)\d{2}\b');
  final match = year.firstMatch(label);
  if (match == null) return '';
  return label.replaceFirst(year, '${int.parse(match.group(0)!) + 1}');
}

DateTime _date(String iso) => DateTime.parse(iso);
String _iso(DateTime d) => DateFormat('yyyy-MM-dd').format(d);
String _short(DateTime d) => DateFormat('d MMM yyyy', 'es').format(d);

const _statusLabels = {
  'Draft': 'Borrador',
  'EnrollmentOpen': 'Inscripciones abiertas',
  'Active': 'En curso',
  'Closed': 'Cerrado',
};
const _nextStatus = {'Draft': 'EnrollmentOpen', 'EnrollmentOpen': 'Active', 'Active': 'Closed'};
const _nextAction = {
  'Draft': 'Abrir inscripciones',
  'EnrollmentOpen': 'Iniciar ciclo',
  'Active': 'Cerrar ciclo',
};

/// School year setup from the phone: same steps and shortcuts as «Ciclos y grados» on the web. Read with
/// academic.read; every change needs academic.manage on the server.
class AcademicYearPage extends ConsumerStatefulWidget {
  const AcademicYearPage({super.key});

  @override
  ConsumerState<AcademicYearPage> createState() => _AcademicYearPageState();
}

class _AcademicYearPageState extends ConsumerState<AcademicYearPage> {
  static const yearsPath = '/api/v1/academic/years';
  String? _selected;

  void _reload([String? yearId]) {
    ref.invalidate(apiGetProvider(yearsPath));
    if (yearId != null) {
      ref.invalidate(apiGetProvider('$yearsPath/$yearId'));
      ref.invalidate(apiGetProvider('/api/v1/years/$yearId/courses'));
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(connectedControllerProvider)!;
    final canManage = session.can('academic.manage');
    return DetailPage(
      title: 'Ciclo escolar',
      onRefresh: () async => _reload(_selected),
      floatingActionButton: canManage
          ? FloatingActionButton.extended(
              onPressed: () async {
                final years = await ref.read(apiGetProvider(yearsPath).future) as List<dynamic>;
                if (!context.mounted) return;
                final created = await showModalBottomSheet<String>(
                  context: context,
                  isScrollControlled: true,
                  builder: (_) => _NewYearSheet(years: years.cast<Map<String, dynamic>>()),
                );
                if (created != null) {
                  setState(() => _selected = created);
                  _reload(created);
                }
              },
              icon: const Icon(Icons.add),
              label: const Text('Nuevo ciclo'),
            )
          : null,
      children: [
        const PageIntro(
          eyebrow: 'Organización académica',
          title: 'Ciclo escolar',
          subtitle: 'Prepara el ciclo paso a paso.',
        ),
        ApiBuilder(
          path: yearsPath,
          builder: (context, data) {
            final years = (data as List<dynamic>).cast<Map<String, dynamic>>();
            if (years.isEmpty) {
              return const EmptyState(
                icon: Icons.event_note_outlined,
                title: 'Aún no hay ciclos',
                message: 'Crea el primer ciclo escolar con «Nuevo ciclo».',
              );
            }
            final selected =
                years.where((y) => y['id'] == _selected).firstOrNull ??
                years.where((y) => y['isDefault'] == true).firstOrNull ??
                years.first;
            final previous =
                (years
                        .where(
                          (y) =>
                              (y['startsOn'] as String).compareTo(selected['startsOn'] as String) <
                              0,
                        )
                        .toList()
                      ..sort(
                        (a, b) => (b['startsOn'] as String).compareTo(a['startsOn'] as String),
                      ))
                    .firstOrNull;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (years.length > 1) ...[
                  Wrap(
                    spacing: Spacing.sm,
                    runSpacing: Spacing.sm,
                    children: [
                      for (final y in years)
                        _GoldChip(
                          label: y['label'] as String,
                          selected: y['id'] == selected['id'],
                          onSelected: () => setState(() => _selected = y['id'] as String),
                        ),
                    ],
                  ),
                  const SizedBox(height: Spacing.lg),
                ],
                _YearDetail(
                  key: ValueKey(selected['id']),
                  yearId: selected['id'] as String,
                  previous: previous,
                  canManage: canManage,
                  onChanged: () => _reload(selected['id'] as String),
                ),
              ],
            );
          },
        ),
        const SizedBox(height: 72),
      ],
    );
  }
}

class _YearDetail extends ConsumerWidget {
  const _YearDetail({
    required this.yearId,
    required this.previous,
    required this.canManage,
    required this.onChanged,
    super.key,
  });

  final String yearId;
  final Map<String, dynamic>? previous;
  final bool canManage;
  final VoidCallback onChanged;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final coursesValue = ref.watch(apiGetProvider('/api/v1/years/$yearId/courses'));
    return ApiBuilder(
      path: '/api/v1/academic/years/$yearId',
      builder: (context, data) {
        final d = data as Map<String, dynamic>;
        final year = d['year'] as Map<String, dynamic>;
        final periods = (d['periods'] as List<dynamic>).cast<Map<String, dynamic>>();
        final sections = (d['sections'] as List<dynamic>).cast<Map<String, dynamic>>();
        final courses = ((coursesValue.value as List<dynamic>?) ?? const [])
            .cast<Map<String, dynamic>>();
        final status = year['status'] as String;
        final open = status == 'Draft' || status == 'EnrollmentOpen';
        final withoutTeacher = courses.where((c) => (c['teachers'] as List).isEmpty).length;
        final enrolled = year['enrollments'] as int;

        Future<void> act(Widget sheet) async {
          final done = await showModalBottomSheet<bool>(
            context: context,
            isScrollControlled: true,
            builder: (_) => sheet,
          );
          if (done == true) onChanged();
        }

        final steps = <_Step>[
          _Step(
            'Bimestres y períodos',
            periods.isEmpty ? 'Sin fechas todavía' : '${periods.length} definidos',
            periods.isNotEmpty,
            periods.isNotEmpty || !open || !canManage
                ? null
                : ('Generar', () => act(_GenerateSheet(year: year))),
          ),
          _Step(
            'Secciones',
            sections.isEmpty
                ? 'Opcionales'
                : '${sections.length} ${sections.length == 1 ? 'sección' : 'secciones'}',
            sections.isNotEmpty,
            sections.isEmpty && previous != null && canManage && status != 'Closed'
                ? (
                    'Copiar',
                    () => act(_CopySheet(yearId: yearId, source: previous!, sections: true)),
                  )
                : null,
          ),
          _Step(
            'Cursos y docentes',
            courses.isEmpty
                ? 'Aún no hay cursos'
                : withoutTeacher > 0
                ? '${courses.length} cursos · $withoutTeacher sin docente'
                : '${courses.length} cursos con docente',
            courses.isNotEmpty && withoutTeacher == 0,
            courses.isEmpty && previous != null && canManage && status != 'Closed'
                ? (
                    'Copiar',
                    () => act(
                      _CopySheet(yearId: yearId, source: previous!, sections: sections.isEmpty),
                    ),
                  )
                : null,
          ),
          _Step(
            'Inscripciones',
            status == 'Draft' ? 'Se habilitan al abrir inscripciones' : '$enrolled inscritos',
            enrolled > 0,
            null,
          ),
          _Step(
            'Inicio de clases',
            status == 'Active'
                ? 'El ciclo está en curso'
                : status == 'Closed'
                ? 'Ciclo cerrado'
                : 'Inicia cuando todo esté listo',
            status == 'Active' || status == 'Closed',
            open && canManage ? (_nextAction[status]!, () => act(_StatusSheet(year: year))) : null,
          ),
        ];
        final ready = steps.where((s) => s.done).length;

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(year['label'] as String, style: theme.textTheme.titleLarge),
                      ),
                      StatusPill(
                        label: _statusLabels[status] ?? status,
                        tone: status == 'Active'
                            ? AppTone.success
                            : status == 'Closed'
                            ? AppTone.neutral
                            : AppTone.accent,
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${_short(_date(year['startsOn'] as String))} – ${_short(_date(year['endsOn'] as String))}',
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                  ),
                ],
              ),
            ),
            if (status != 'Closed') ...[
              SectionHeader(title: 'Paso a paso · $ready de ${steps.length}'),
              AppCard(
                padding: const EdgeInsets.symmetric(horizontal: Spacing.lg, vertical: Spacing.sm),
                child: Column(
                  children: [
                    for (final (i, step) in steps.indexed) ...[
                      if (i > 0) Divider(height: 1, color: colors.border),
                      _StepRow(index: i + 1, step: step),
                    ],
                  ],
                ),
              ),
            ],
            const SectionHeader(title: 'Bimestres y períodos'),
            if (periods.isEmpty)
              Text(
                'Sin períodos. Son necesarios para iniciar el ciclo.',
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
              )
            else
              AppCard(
                padding: const EdgeInsets.symmetric(horizontal: Spacing.lg, vertical: Spacing.sm),
                child: Column(
                  children: [
                    for (final (i, p) in periods.indexed) ...[
                      if (i > 0) Divider(height: 1, color: colors.border),
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        leading: CircleAvatar(
                          radius: 16,
                          backgroundColor: colors.accent.withValues(alpha: 0.25),
                          child: Text('${p['sequence']}', style: theme.textTheme.labelLarge),
                        ),
                        title: Text(p['name'] as String),
                        subtitle: Text(
                          '${_short(_date(p['startsOn'] as String))} – ${_short(_date(p['endsOn'] as String))}',
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            if (status == 'Active' && canManage) ...[
              const SizedBox(height: Spacing.lg),
              OutlinedButton.icon(
                onPressed: () => act(_StatusSheet(year: year)),
                icon: const Icon(Icons.lock_outline),
                label: const Text('Cerrar ciclo'),
              ),
            ],
          ],
        );
      },
    );
  }
}

class _Step {
  const _Step(this.title, this.detail, this.done, this.action);
  final String title;
  final String detail;
  final bool done;
  final (String, VoidCallback)? action;
}

class _StepRow extends StatelessWidget {
  const _StepRow({required this.index, required this.step});

  final int index;
  final _Step step;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: Spacing.md),
      child: Row(
        children: [
          Semantics(
            label: step.done ? 'Listo' : 'Pendiente',
            child: CircleAvatar(
              radius: 15,
              backgroundColor: step.done ? colors.tone(AppTone.success).background : colors.border,
              child: step.done
                  ? Icon(Icons.check, size: 16, color: colors.tone(AppTone.success).foreground)
                  : Text('$index', style: theme.textTheme.labelMedium),
            ),
          ),
          const SizedBox(width: Spacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(step.title, style: theme.textTheme.titleSmall),
                Text(step.detail, style: theme.textTheme.bodySmall?.copyWith(color: colors.muted)),
              ],
            ),
          ),
          if (step.action case (final label, final onTap))
            FilledButton(
              onPressed: onTap,
              style: FilledButton.styleFrom(minimumSize: const Size(48, 40)),
              child: Text(label),
            ),
        ],
      ),
    );
  }
}

/// Shared shell of the sheets: title, body, error and the main action with a busy state.
class _SheetFrame extends StatelessWidget {
  const _SheetFrame({
    required this.title,
    required this.children,
    required this.action,
    required this.onAction,
    required this.busy,
    this.error,
  });

  final String title;
  final List<Widget> children;
  final String action;
  final VoidCallback onAction;
  final bool busy;
  final String? error;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: EdgeInsets.fromLTRB(
        Spacing.page,
        0,
        Spacing.page,
        Spacing.lg + MediaQuery.viewInsetsOf(context).bottom,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(title, style: theme.textTheme.titleLarge),
            const SizedBox(height: Spacing.md),
            ...children,
            if (error != null) ...[
              const SizedBox(height: Spacing.md),
              Text(error!, style: TextStyle(color: theme.colorScheme.error)),
            ],
            const SizedBox(height: Spacing.lg),
            FilledButton(
              onPressed: busy ? null : onAction,
              child: Text(busy ? 'Guardando…' : action),
            ),
          ],
        ),
      ),
    );
  }
}

class _PeriodPreview extends StatelessWidget {
  const _PeriodPreview({required this.start, required this.end, required this.count});

  final DateTime start;
  final DateTime end;
  final int count;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Container(
      padding: const EdgeInsets.all(Spacing.md),
      decoration: BoxDecoration(
        color: colors.tone(AppTone.neutral).background,
        borderRadius: BorderRadius.circular(Radii.md),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (final (i, (from, to)) in splitCalendar(start, end, count).indexed)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Text(
                'Período ${i + 1}:  ${_short(from)} – ${_short(to)}',
                style: theme.textTheme.bodySmall,
              ),
            ),
        ],
      ),
    );
  }
}

class _CountChips extends StatelessWidget {
  const _CountChips({required this.value, required this.onChanged});

  final int value;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) => Wrap(
    spacing: Spacing.sm,
    children: [
      for (final n in const [2, 3, 4, 5, 6])
        _GoldChip(label: '$n', selected: n == value, onSelected: () => onChanged(n)),
    ],
  );
}

class _GenerateSheet extends ConsumerStatefulWidget {
  const _GenerateSheet({required this.year});

  final Map<String, dynamic> year;

  @override
  ConsumerState<_GenerateSheet> createState() => _GenerateSheetState();
}

class _GenerateSheetState extends ConsumerState<_GenerateSheet> {
  int _count = 4;
  bool _busy = false;
  String? _error;

  Future<void> _save() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(connectedControllerProvider)!.api.post(
        '/api/v1/academic/years/${widget.year['id']}/periods/generate',
        {'version': widget.year['version'], 'count': _count},
      );
      if (!mounted) return;
      Navigator.of(context).pop(true);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Se crearon $_count períodos. Puedes ajustar sus fechas en la web.'),
        ),
      );
    } on ApiFailure catch (e) {
      setState(() {
        _error = e.message;
        _busy = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) => _SheetFrame(
    title: 'Generar períodos',
    action: 'Generar $_count períodos',
    onAction: _save,
    busy: _busy,
    error: _error,
    children: [
      const Text('Divide el ciclo en períodos del mismo largo.'),
      const SizedBox(height: Spacing.md),
      _CountChips(value: _count, onChanged: (n) => setState(() => _count = n)),
      const SizedBox(height: Spacing.md),
      _PeriodPreview(
        start: _date(widget.year['startsOn'] as String),
        end: _date(widget.year['endsOn'] as String),
        count: _count,
      ),
    ],
  );
}

class _CopySheet extends ConsumerStatefulWidget {
  const _CopySheet({required this.yearId, required this.source, required this.sections});

  final String yearId;
  final Map<String, dynamic> source;
  final bool sections;

  @override
  ConsumerState<_CopySheet> createState() => _CopySheetState();
}

class _CopySheetState extends ConsumerState<_CopySheet> {
  late bool _sections = widget.sections;
  bool _courses = true;
  bool _teachers = true;
  bool _busy = false;
  String? _error;

  Future<void> _save() async {
    if (!_sections && !_courses) {
      setState(() => _error = 'Elige qué copiar.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final summary = await copyFromYear(
        ref.read(connectedControllerProvider)!.api,
        widget.yearId,
        widget.source['id'] as String,
        sections: _sections,
        courses: _courses,
        teachers: _teachers,
      );
      if (!mounted) return;
      Navigator.of(context).pop(true);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(summary.isEmpty ? 'No había nada nuevo que copiar.' : summary)),
      );
    } on ApiFailure catch (e) {
      setState(() {
        _error = e.message;
        _busy = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) => _SheetFrame(
    title: 'Copiar de ${widget.source['label']}',
    action: 'Copiar',
    onAction: _save,
    busy: _busy,
    error: _error,
    children: [
      const Text('No mueve estudiantes ni notas, y no cambia lo que ya existe.'),
      SwitchListTile(
        contentPadding: EdgeInsets.zero,
        title: const Text('Secciones'),
        value: _sections,
        onChanged: (v) => setState(() => _sections = v),
      ),
      SwitchListTile(
        contentPadding: EdgeInsets.zero,
        title: const Text('Cursos'),
        value: _courses,
        onChanged: (v) => setState(() => _courses = v),
      ),
      SwitchListTile(
        contentPadding: EdgeInsets.zero,
        title: const Text('Con los mismos docentes'),
        value: _courses && _teachers,
        onChanged: _courses ? (v) => setState(() => _teachers = v) : null,
      ),
    ],
  );
}

/// Sections, then courses (optionally with teachers), from another year. Returns a short summary for the user.
Future<String> copyFromYear(
  SchoolApi api,
  String yearId,
  String sourceId, {
  required bool sections,
  required bool courses,
  required bool teachers,
}) async {
  final done = <String>[];
  if (sections) {
    final r = await api.post('/api/v1/academic/years/$yearId/sections/copy', {
      'sourceYearId': sourceId,
    }) as Map<String, dynamic>;
    if ((r['created'] as int) > 0) done.add('${r['created']} secciones');
  }
  if (courses) {
    final r = await api.post('/api/v1/years/$yearId/courses/copy', {
      'sourceYearId': sourceId,
      'teachers': teachers,
    }) as Map<String, dynamic>;
    if ((r['created'] as int) > 0) done.add('${r['created']} cursos');
    if ((r['teachers'] as int) > 0) done.add('${r['teachers']} docentes');
  }
  return done.isEmpty ? '' : 'Copiado: ${done.join(', ')}.';
}

class _StatusSheet extends ConsumerStatefulWidget {
  const _StatusSheet({required this.year});

  final Map<String, dynamic> year;

  @override
  ConsumerState<_StatusSheet> createState() => _StatusSheetState();
}

class _StatusSheetState extends ConsumerState<_StatusSheet> {
  late final TextEditingController _reason;
  bool _busy = false;
  String? _error;

  String get _status => widget.year['status'] as String;

  @override
  void initState() {
    super.initState();
    final label = widget.year['label'];
    _reason = TextEditingController(
      text: switch (_status) {
        'Draft' => 'Apertura de inscripciones del ciclo $label',
        'EnrollmentOpen' => 'Inicio de clases del ciclo $label',
        _ => '',
      },
    );
  }

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_reason.text.trim().length < 8) {
      setState(() => _error = 'Describe el motivo (al menos 8 caracteres).');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(connectedControllerProvider)!.api.post(
        '/api/v1/academic/years/${widget.year['id']}/status',
        {
          'version': widget.year['version'],
          'status': _nextStatus[_status],
          'reason': _reason.text.trim(),
        },
      );
      if (!mounted) return;
      Navigator.of(context).pop(true);
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('El estado del ciclo cambió.')));
    } on ApiFailure catch (e) {
      setState(() {
        _error = e.message;
        _busy = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) => _SheetFrame(
    title: _nextAction[_status] ?? '',
    action: _nextAction[_status] ?? 'Guardar',
    onAction: _save,
    busy: _busy,
    error: _error,
    children: [
      Text(switch (_status) {
        'Draft' => 'Se podrán registrar inscripciones en este ciclo.',
        'EnrollmentOpen' => 'El ciclo inicia. Sus fechas y períodos quedarán fijos.',
        _ =>
          'El ciclo se cerrará: no admitirá inscripciones ni cambios. No se revierte desde aquí.',
      }),
      const SizedBox(height: Spacing.md),
      TextField(
        controller: _reason,
        minLines: 2,
        maxLines: 4,
        maxLength: 500,
        decoration: const InputDecoration(labelText: 'Motivo del cambio'),
      ),
    ],
  );
}

class _NewYearSheet extends ConsumerStatefulWidget {
  const _NewYearSheet({required this.years});

  final List<Map<String, dynamic>> years;

  @override
  ConsumerState<_NewYearSheet> createState() => _NewYearSheetState();
}

class _NewYearSheetState extends ConsumerState<_NewYearSheet> {
  late final Map<String, dynamic>? _latest = (List.of(
    widget.years,
  )..sort((a, b) => (b['startsOn'] as String).compareTo(a['startsOn'] as String))).firstOrNull;
  late final TextEditingController _label = TextEditingController(
    text: _latest == null ? '' : nextCycleLabel(_latest['label'] as String),
  );
  late DateTime? _start = _latest == null ? null : _shift(_date(_latest['startsOn'] as String));
  late DateTime? _end = _latest == null ? null : _shift(_date(_latest['endsOn'] as String));
  bool _generate = true;
  int _periods = 4;
  bool _copy = true;
  bool _teachers = true;
  bool _busy = false;
  String? _error;

  static DateTime _shift(DateTime d) =>
      DateTime(d.year + 1, d.month, d.month == 2 && d.day == 29 ? 28 : d.day);

  @override
  void dispose() {
    _label.dispose();
    super.dispose();
  }

  Future<void> _pick(bool start) async {
    final initial = (start ? _start : _end) ?? DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (picked != null) setState(() => start ? _start = picked : _end = picked);
  }

  Future<void> _save() async {
    if (_label.text.trim().isEmpty || _start == null || _end == null) {
      setState(() => _error = 'Completa el nombre y las fechas.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    final api = ref.read(connectedControllerProvider)!.api;
    String? id;
    try {
      id =
          (await api.post('/api/v1/academic/years', {
                'label': _label.text.trim(),
                'startsOn': _iso(_start!),
                'endsOn': _iso(_end!),
              }) as Map<String, dynamic>)['id']
              as String;
      final done = <String>['Ciclo creado en borrador.'];
      if (_generate) {
        final detail = await api.get('/api/v1/academic/years/$id') as Map<String, dynamic>;
        await api.post('/api/v1/academic/years/$id/periods/generate', {
          'version': (detail['year'] as Map<String, dynamic>)['version'],
          'count': _periods,
        });
        done.add('$_periods períodos.');
      }
      if (_copy && _latest != null) {
        final summary = await copyFromYear(
          api,
          id,
          _latest['id'] as String,
          sections: true,
          courses: true,
          teachers: _teachers,
        );
        if (summary.isNotEmpty) done.add(summary);
      }
      if (!mounted) return;
      Navigator.of(context).pop(id);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(done.join(' '))));
    } on ApiFailure catch (e) {
      if (id != null && mounted) {
        // The cycle exists; the user finishes the remaining step from the guide.
        Navigator.of(context).pop(id);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('El ciclo se creó, pero falta un paso: ${e.message}')),
        );
        return;
      }
      setState(() {
        _error = e.message;
        _busy = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final latestLabel = _latest?['label'];
    return _SheetFrame(
      title: 'Nuevo ciclo escolar',
      action: 'Crear ciclo',
      onAction: _save,
      busy: _busy,
      error: _error,
      children: [
        TextField(
          controller: _label,
          maxLength: 40,
          decoration: const InputDecoration(labelText: 'Nombre del ciclo'),
        ),
        Row(
          children: [
            Expanded(
              child: OutlinedButton.icon(
                onPressed: () => _pick(true),
                icon: const Icon(Icons.event),
                label: Text(_start == null ? 'Inicio' : _short(_start!)),
              ),
            ),
            const SizedBox(width: Spacing.sm),
            Expanded(
              child: OutlinedButton.icon(
                onPressed: () => _pick(false),
                icon: const Icon(Icons.event),
                label: Text(_end == null ? 'Fin' : _short(_end!)),
              ),
            ),
          ],
        ),
        const SizedBox(height: Spacing.md),
        SwitchListTile(
          contentPadding: EdgeInsets.zero,
          title: const Text('Generar los períodos'),
          subtitle: const Text('Del mismo largo; después puedes ajustarlos.'),
          value: _generate,
          onChanged: (v) => setState(() => _generate = v),
        ),
        if (_generate) ...[
          _CountChips(value: _periods, onChanged: (n) => setState(() => _periods = n)),
          if (_start != null && _end != null) ...[
            const SizedBox(height: Spacing.sm),
            _PeriodPreview(start: _start!, end: _end!, count: _periods),
          ],
        ],
        if (latestLabel != null) ...[
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: Text('Copiar secciones y cursos de $latestLabel'),
            value: _copy,
            onChanged: (v) => setState(() => _copy = v),
          ),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('Con los mismos docentes'),
            value: _copy && _teachers,
            onChanged: _copy ? (v) => setState(() => _teachers = v) : null,
          ),
        ],
      ],
    );
  }
}

/// Same selected look as [SectionChips]: gold tint and border.
class _GoldChip extends StatelessWidget {
  const _GoldChip({required this.label, required this.selected, required this.onSelected});

  final String label;
  final bool selected;
  final VoidCallback onSelected;

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    return ChoiceChip(
      label: Text(label),
      selected: selected,
      showCheckmark: false,
      selectedColor: colors.accent.withValues(alpha: 0.28),
      side: BorderSide(color: selected ? colors.accent : colors.border),
      onSelected: (_) => onSelected(),
    );
  }
}
