import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:printing/printing.dart';

import '../../core/theme/app_colors.dart';
import '../../core/widgets/cards.dart';
import '../../core/widgets/states.dart';
import '../../shared/page_layout.dart';
import 'connected_session.dart';
import 'connected_shell.dart';
import 'school_api.dart';

typedef TimeMap = Map<String, dynamic>;
const timeDays = ['', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
String timeClock(int minutes) =>
    '${(minutes ~/ 60).toString().padLeft(2, '0')}:${(minutes % 60).toString().padLeft(2, '0')}';

class TimetablePage extends ConsumerStatefulWidget {
  const TimetablePage({this.studentId, super.key});
  final String? studentId;
  @override
  ConsumerState<TimetablePage> createState() => _TimetablePageState();
}

class _TimetablePageState extends ConsumerState<TimetablePage> {
  String _year = '';
  String _on = '';
  String _viewer = '';
  String _grade = '';
  String _section = '';
  List<TimeMap> _years = [];
  List<TimeMap> _grades = [];
  List<TimeMap> _sections = [];
  List<TimeMap> _slots = [];
  int _day = DateTime.now().weekday;
  bool _loading = true;
  bool _printing = false;
  String? _error;
  int _request = 0;
  ConnectedSession get session => ref.read(connectedControllerProvider)!;
  bool get manager => session.can('academic.read');
  @override
  void initState() {
    super.initState();
    _viewer = widget.studentId != null
        ? 'student:${widget.studentId}'
        : manager
        ? 'group'
        : session.courses.isNotEmpty
        ? 'teacher'
        : session.students.isNotEmpty
        ? 'student:${session.students.first.id}'
        : '';
    Future.microtask(_init);
  }

  Future<void> _init() async {
    setState(() => _loading = true);
    try {
      final years = (await session.api.get(
        manager ? '/api/v1/academic/years' : '/api/v1/me/timetables/years',
      ) as List).cast<TimeMap>();
      if (!mounted) return;
      _years = years;
      _year =
          years.where((y) => y['isDefault'] == true).firstOrNull?['id'] as String? ??
          years.firstOrNull?['id'] as String? ??
          '';
      if (_year.isEmpty) {
        setState(() => _loading = false);
        return;
      }
      await _yearChanged();
    } catch (e) {
      _fail(e);
    }
  }

  Future<void> _yearChanged() async {
    _request++;
    setState(() {
      _loading = true;
      _slots = [];
      _error = null;
    });
    final year = _years.firstWhere((y) => y['id'] == _year);
    final today = DateFormat('yyyy-MM-dd').format(DateTime.now());
    _on = today.compareTo(year['startsOn'] as String) < 0
        ? year['startsOn'] as String
        : today.compareTo(year['endsOn'] as String) > 0
        ? year['endsOn'] as String
        : today;
    _grade = '';
    _section = '';
    try {
      if (manager) {
        final values = await Future.wait([
          session.api.get('/api/v1/academic/grades'),
          session.api.get('/api/v1/academic/years/$_year'),
        ]);
        if (!mounted) return;
        _grades = (values[0] as List).cast<TimeMap>();
        _grade = _grades.firstOrNull?['id'] as String? ?? '';
        _sections = ((values[1] as TimeMap)['sections'] as List).cast<TimeMap>();
      }
      await _load();
    } catch (e) {
      _fail(e);
    }
  }

  String _path({bool pdf = false}) {
    final query = Uri(queryParameters: {'yearId': _year, 'on': _on, if (pdf) 'paper': 'letter'})
        .query;
    if (_viewer == 'group') {
      return '/api/v1/years/$_year/timetables/published${pdf ? '.pdf' : ''}?on=$_on${_grade.isEmpty ? '' : '&gradeId=$_grade'}${_section.isEmpty ? '' : '&sectionId=$_section'}${pdf ? '&paper=letter' : ''}';
    }
    if (_viewer == 'teacher') return '/api/v1/me/timetables/teacher${pdf ? '.pdf' : ''}?$query';
    return '/api/v1/me/timetables/students/${_viewer.substring(8)}${pdf ? '.pdf' : ''}?$query';
  }

  Future<void> _load() async {
    final request = ++_request;
    setState(() {
      _loading = true;
      _error = null;
      _slots = [];
    });
    try {
      final data = await session.api.get(_path()) as TimeMap;
      if (!mounted || request != _request) return;
      final slots = (data['slots'] as List).cast<TimeMap>();
      setState(() {
        _slots = slots;
        _loading = false;
        if (!slots.any((s) => s['day'] == _day) && slots.isNotEmpty) {
          _day = slots.first['day'] as int;
        }
      });
    } catch (e) {
      if (request == _request) _fail(e);
    }
  }

  void _fail(Object error) {
    if (!mounted) return;
    if (error is ApiFailure && error.unauthorized) {
      ref.read(connectedControllerProvider.notifier).expire();
    }
    setState(() {
      _loading = false;
      _error = error is ApiFailure
          ? error.message
          : 'No pudimos consultar el horario. Intenta de nuevo.';
      _slots = [];
    });
  }

  Future<void> _print() async {
    if (_printing) return;
    setState(() => _printing = true);
    try {
      final bytes = await session.api.pdf(_path(pdf: true));
      if (!mounted) return;
      await Printing.layoutPdf(
        onLayout: (_) async => bytes,
        name: 'Horario escolar',
        dynamicLayout: false,
      );
    } catch (e) {
      if (mounted) {
        if (e is ApiFailure && e.unauthorized) {
          ref.read(connectedControllerProvider.notifier).expire();
        }
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              e is ApiFailure ? e.message : 'No pudimos abrir la impresión. Intenta de nuevo.',
            ),
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _printing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final viewerOptions = <String, String>{
      if (manager) 'group': 'Grado y sección',
      if (session.courses.isNotEmpty) 'teacher': 'Mi semana docente',
      for (final s in session.students.where((s) => s.canAcademic)) 'student:${s.id}': s.name,
    };
    final days = _slots.map((s) => s['day'] as int).toSet().toList()..sort();
    final slots = _slots.where((s) => s['day'] == _day).toList();
    return DetailPage(
      title: 'Horarios',
      onRefresh: _year.isEmpty ? _init : _load,
      children: [
        const PageIntro(
          eyebrow: 'Organización académica',
          title: 'Tu semana, en orden',
          subtitle: 'Consulta las clases del horario publicado.',
        ),
        if (_years.isNotEmpty)
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                DropdownButtonFormField<String>(
                  key: ValueKey('year-$_year'),
                  initialValue: _year,
                  decoration: const InputDecoration(labelText: 'Ciclo escolar'),
                  items: [
                    for (final y in _years)
                      DropdownMenuItem(value: y['id'] as String, child: Text(y['label'] as String)),
                  ],
                  onChanged: _loading || _printing
                      ? null
                      : (v) async {
                          if (v != null) {
                            _year = v;
                            await _yearChanged();
                          }
                        },
                ),
                const SizedBox(height: 16),
                DropdownButtonFormField<String>(
                  key: ValueKey('viewer-$_viewer'),
                  initialValue: viewerOptions.containsKey(_viewer) ? _viewer : null,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Ver horario de'),
                  items: [
                    for (final v in viewerOptions.entries)
                      DropdownMenuItem(value: v.key, child: Text(v.value)),
                  ],
                  onChanged: _loading || _printing
                      ? null
                      : (v) async {
                          if (v != null) {
                            _viewer = v;
                            await _load();
                          }
                        },
                ),
                if (manager && _viewer == 'group') ...[
                  const SizedBox(height: 16),
                  DropdownButtonFormField<String>(
                    key: ValueKey('grade-$_grade'),
                    initialValue: _grade.isEmpty ? null : _grade,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Grado'),
                    items: [
                      for (final g in _grades)
                        DropdownMenuItem(
                          value: g['id'] as String,
                          child: Text(g['name'] as String),
                        ),
                    ],
                    onChanged: _loading || _printing
                        ? null
                        : (v) async {
                            if (v != null) {
                              _grade = v;
                              _section = '';
                              await _load();
                            }
                          },
                  ),
                  const SizedBox(height: 16),
                  DropdownButtonFormField<String>(
                    key: ValueKey('section-$_grade-$_section'),
                    initialValue: _section,
                    decoration: const InputDecoration(labelText: 'Sección'),
                    items: [
                      const DropdownMenuItem(value: '', child: Text('Todo el grado')),
                      for (final s in _sections.where((s) => s['gradeLevelId'] == _grade))
                        DropdownMenuItem(
                          value: s['id'] as String,
                          child: Text(s['name'] as String),
                        ),
                    ],
                    onChanged: _loading || _printing
                        ? null
                        : (v) async {
                            if (v != null) {
                              _section = v;
                              await _load();
                            }
                          },
                  ),
                ],
                const SizedBox(height: 16),
                OutlinedButton.icon(
                  onPressed: _loading || _printing
                      ? null
                      : () async {
                          final y = _years.firstWhere((y) => y['id'] == _year);
                          final date = await showDatePicker(
                            context: context,
                            initialDate: DateTime.parse(_on),
                            firstDate: DateTime.parse(y['startsOn'] as String),
                            lastDate: DateTime.parse(y['endsOn'] as String),
                          );
                          if (date != null && mounted) {
                            _on = DateFormat('yyyy-MM-dd').format(date);
                            await _load();
                          }
                        },
                  icon: const Icon(Icons.event_outlined),
                  label: Text('Vigente al ${_on.isEmpty ? '' : shortDate(_on)}'),
                ),
              ],
            ),
          ),
        const SizedBox(height: 24),
        if (_loading)
          const LoadingState(itemCount: 3)
        else if (_error != null)
          ErrorState(message: _error!, onRetry: _year.isEmpty ? _init : _load)
        else if (_slots.isEmpty)
          const EmptyState(
            icon: Icons.calendar_month_outlined,
            title: 'Sin horario publicado',
            message: 'El horario aparecerá cuando el colegio publique la semana para este grupo y fecha.',
          )
        else ...[
          Row(
            children: [
              Expanded(child: Text('Horario publicado', style: theme.textTheme.titleMedium)),
              IconButton(
                onPressed: _printing ? null : _print,
                tooltip: _printing ? 'Preparando documento' : 'Imprimir horario',
                icon: Icon(_printing ? Icons.hourglass_empty : Icons.print_outlined),
              ),
            ],
          ),
          const SizedBox(height: 12),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                for (final d in days)
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(timeDays[d]),
                      selected: _day == d,
                      onSelected: (_) => setState(() => _day = d),
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(height: 20),
          for (final slot in slots)
            Padding(
              padding: const EdgeInsets.only(bottom: 14),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  SizedBox(
                    width: 61,
                    child: Padding(
                      padding: const EdgeInsets.only(top: 16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(timeClock(slot['start'] as int), style: theme.textTheme.labelLarge),
                          Text(
                            timeClock(slot['end'] as int),
                            style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                          ),
                        ],
                      ),
                    ),
                  ),
                  Expanded(
                    child: AppCard(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            slot['kind'] == 'Break' ? 'DESCANSO' : 'CLASE',
                            style: theme.textTheme.labelSmall?.copyWith(color: colors.muted),
                          ),
                          const SizedBox(height: 8),
                          Text(slot['subject'] as String, style: theme.textTheme.titleSmall),
                          if ((slot['teacher'] as String).isNotEmpty) ...[
                            const SizedBox(height: 7),
                            Text(slot['teacher'] as String, style: theme.textTheme.bodySmall),
                          ],
                          const SizedBox(height: 7),
                          Text(
                            '${slot['grade']}${slot['section'] == null ? '' : ' · ${slot['section']}'}',
                            style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                          ),
                          if ((slot['room'] as String).isNotEmpty) ...[
                            const SizedBox(height: 7),
                            Text(
                              slot['room'] as String,
                              style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ],
    );
  }
}
