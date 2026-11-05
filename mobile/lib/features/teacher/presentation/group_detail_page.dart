import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/format/formatters.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/teaching.dart';
import '../../../shared/visuals.dart';
import '../application/teaching_providers.dart';

enum _GroupTab { attendance, gradebook }

/// Detalle de grupo con asistencia y libreta. Advierte antes de salir si hay
/// cambios sin guardar.
class GroupDetailPage extends ConsumerStatefulWidget {
  const GroupDetailPage({required this.groupId, super.key});

  final String groupId;

  @override
  ConsumerState<GroupDetailPage> createState() => _GroupDetailPageState();
}

class _GroupDetailPageState extends ConsumerState<GroupDetailPage> {
  final Set<_GroupTab> _dirty = {};

  void _setDirty(_GroupTab tab, bool dirty) {
    final changed = dirty ? _dirty.add(tab) : _dirty.remove(tab);
    if (changed) setState(() {});
  }

  Future<bool> _confirmDiscard() async {
    final discard = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('¿Descartar cambios?'),
        content: const Text('Tienes cambios sin guardar en este grupo.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Seguir editando'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Descartar'),
          ),
        ],
      ),
    );
    return discard ?? false;
  }

  @override
  Widget build(BuildContext context) {
    final group = ref.watch(groupProvider(widget.groupId));
    return PopScope<Object?>(
      canPop: _dirty.isEmpty,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        final navigator = Navigator.of(context);
        if (await _confirmDiscard()) {
          setState(_dirty.clear);
          navigator.pop();
        }
      },
      child: DefaultTabController(
        length: 2,
        child: Scaffold(
          appBar: AppBar(
            title: Text(
              group.value == null
                  ? 'Grupo'
                  : '${group.value!.subject.name} · ${group.value!.placement}',
            ),
            bottom: PreferredSize(
              preferredSize: const Size.fromHeight(56),
              child: Padding(
                padding: const EdgeInsets.fromLTRB(Spacing.page, 0, Spacing.page, Spacing.sm),
                child: Container(
                  padding: const EdgeInsets.all(4),
                  decoration: BoxDecoration(
                    color: AppColors.of(context).tone(AppTone.neutral).background,
                    borderRadius: BorderRadius.circular(14),
                  ),
                  child: TabBar(
                    tabs: [
                      Tab(
                        text: _dirty.contains(_GroupTab.attendance) ? 'Asistencia •' : 'Asistencia',
                        height: 40,
                      ),
                      Tab(
                        text: _dirty.contains(_GroupTab.gradebook) ? 'Libreta •' : 'Libreta',
                        height: 40,
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
          body: AsyncValueView(
            value: group,
            onRetry: () => ref.invalidate(groupProvider(widget.groupId)),
            data: (g) => TabBarView(
              children: [
                _AttendanceTab(group: g, onDirtyChanged: (d) => _setDirty(_GroupTab.attendance, d)),
                _GradebookTab(
                  group: g,
                  onDirtyChanged: (d) => _setDirty(_GroupTab.gradebook, d),
                  confirmDiscard: _confirmDiscard,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Asistencia
// ---------------------------------------------------------------------------

class _AttendanceTab extends ConsumerStatefulWidget {
  const _AttendanceTab({required this.group, required this.onDirtyChanged});

  final TeachingGroup group;
  final ValueChanged<bool> onDirtyChanged;

  @override
  ConsumerState<_AttendanceTab> createState() => _AttendanceTabState();
}

class _AttendanceTabState extends ConsumerState<_AttendanceTab> with AutomaticKeepAliveClientMixin {
  Map<String, AttendanceMark>? _marks;
  DateTime? _savedAt;
  bool _dirty = false;
  bool _saving = false;

  @override
  bool get wantKeepAlive => true;

  void _setMark(String studentId, AttendanceMark mark) {
    setState(() {
      _marks![studentId] = mark;
      _markDirty();
    });
  }

  void _markDirty() {
    if (!_dirty) {
      _dirty = true;
      widget.onDirtyChanged(true);
    }
  }

  Future<void> _save(AttendanceSheet sheet) async {
    setState(() => _saving = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      final saved = await ref
          .read(teachingActionsProvider)
          .saveAttendance(
            AttendanceSheet(
              groupId: sheet.groupId,
              date: sheet.date,
              roster: sheet.roster,
              marks: Map.of(_marks!),
              savedAt: sheet.savedAt,
            ),
          );
      if (!mounted) return;
      setState(() {
        _dirty = false;
        _savedAt = saved.savedAt;
      });
      widget.onDirtyChanged(false);
      messenger.showSnackBar(const SnackBar(content: Text('Asistencia guardada.')));
    } on Object catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(friendlyError(error))));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final today = ref.watch(todayProvider);
    final arg = (groupId: widget.group.id, date: today);
    final sheet = ref.watch(attendanceSheetProvider(arg));
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return AsyncValueView(
      value: sheet,
      onRetry: () => ref.invalidate(attendanceSheetProvider(arg)),
      data: (data) {
        _marks ??= Map.of(data.marks);
        _savedAt ??= data.savedAt;
        final counts = {
          for (final mark in AttendanceMark.values)
            mark: _marks!.values.where((m) => m == mark).length,
        };
        return Column(
          children: [
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(
                  Spacing.page,
                  Spacing.md,
                  Spacing.page,
                  Spacing.lg,
                ),
                children: [
                  Text(
                    Formatters.capitalize(format.weekdayDayMonth(data.date)),
                    style: theme.textTheme.titleMedium,
                  ),
                  Text(
                    _savedAt == null ? 'Sin registrar' : 'Guardada a las ${format.time(_savedAt!)}',
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                  ),
                  const SizedBox(height: Spacing.md),
                  Wrap(
                    spacing: Spacing.sm,
                    runSpacing: Spacing.sm,
                    children: [
                      for (final mark in AttendanceMark.values)
                        StatusPill(label: '${mark.label}: ${counts[mark]}', tone: mark.tone),
                    ],
                  ),
                  const SizedBox(height: Spacing.md),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: TextButton.icon(
                      onPressed: _saving
                          ? null
                          : () => setState(() {
                              for (final s in data.roster) {
                                _marks![s.id] = AttendanceMark.present;
                              }
                              _markDirty();
                            }),
                      icon: const Icon(Icons.done_all_rounded),
                      label: const Text('Marcar todos presentes'),
                    ),
                  ),
                  const SizedBox(height: Spacing.sm),
                  AppCard(
                    padding: const EdgeInsets.symmetric(vertical: Spacing.xs),
                    child: Column(
                      children: [
                        for (var i = 0; i < data.roster.length; i++) ...[
                          if (i > 0) const Divider(indent: Spacing.lg, endIndent: Spacing.lg),
                          Padding(
                            padding: const EdgeInsets.symmetric(
                              horizontal: Spacing.md,
                              vertical: Spacing.sm,
                            ),
                            child: Row(
                              children: [
                                InitialsAvatar(
                                  initials: data.roster[i].initials,
                                  size: 36,
                                  tone: AppTone.neutral,
                                ),
                                const SizedBox(width: Spacing.sm),
                                Expanded(
                                  child: Text(
                                    data.roster[i].fullName,
                                    maxLines: 2,
                                    overflow: TextOverflow.ellipsis,
                                    style: theme.textTheme.bodyMedium?.copyWith(
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                ),
                                _MarkSelector(
                                  studentName: data.roster[i].fullName,
                                  value: _marks![data.roster[i].id] ?? AttendanceMark.present,
                                  enabled: !_saving,
                                  onChanged: (mark) => _setMark(data.roster[i].id, mark),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
            ),
            _SaveBar(
              label: 'Guardar asistencia',
              saving: _saving,
              enabled: _dirty || _savedAt == null,
              onPressed: () => _save(data),
            ),
          ],
        );
      },
    );
  }
}

class _MarkSelector extends StatelessWidget {
  const _MarkSelector({
    required this.studentName,
    required this.value,
    required this.onChanged,
    required this.enabled,
  });

  final String studentName;
  final AttendanceMark value;
  final ValueChanged<AttendanceMark> onChanged;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (final mark in AttendanceMark.values)
          Padding(
            padding: const EdgeInsets.only(left: 4),
            child: Semantics(
              button: true,
              selected: mark == value,
              label: '$studentName: ${mark.label}',
              excludeSemantics: true,
              child: Tooltip(
                message: mark.label,
                child: InkWell(
                  borderRadius: BorderRadius.circular(10),
                  onTap: enabled ? () => onChanged(mark) : null,
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 150),
                    width: 34,
                    height: 34,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: mark == value
                          ? colors.tone(mark.tone).foreground
                          : colors.tone(AppTone.neutral).background,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Text(
                      mark.short,
                      style: TextStyle(
                        fontWeight: FontWeight.w800,
                        color: mark == value ? colors.cardBackground : colors.muted,
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Libreta
// ---------------------------------------------------------------------------

class _GradebookTab extends ConsumerStatefulWidget {
  const _GradebookTab({
    required this.group,
    required this.onDirtyChanged,
    required this.confirmDiscard,
  });

  final TeachingGroup group;
  final ValueChanged<bool> onDirtyChanged;
  final Future<bool> Function() confirmDiscard;

  @override
  ConsumerState<_GradebookTab> createState() => _GradebookTabState();
}

class _GradebookTabState extends ConsumerState<_GradebookTab> with AutomaticKeepAliveClientMixin {
  int _period = 3;
  final Map<String, TextEditingController> _controllers = {};
  String? _loadedKey;
  bool _dirty = false;
  bool _saving = false;
  final _formKey = GlobalKey<FormState>();

  @override
  bool get wantKeepAlive => true;

  @override
  void dispose() {
    for (final c in _controllers.values) {
      c.dispose();
    }
    super.dispose();
  }

  /// Crea controladores nuevos al cambiar de período. No modifica controladores
  /// ya montados durante la construcción (evita reconstrucciones inválidas).
  void _load(GradebookSheet sheet) {
    final key = '${sheet.groupId}|${sheet.period}';
    if (_loadedKey == key) return;
    _loadedKey = key;
    final previous = Map.of(_controllers);
    _controllers
      ..clear()
      ..addAll({
        for (final student in sheet.roster)
          student.id: TextEditingController(
            text: sheet.scores[student.id] == null ? '' : _formatScore(sheet.scores[student.id]!),
          ),
      });
    if (previous.isNotEmpty) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        for (final c in previous.values) {
          c.dispose();
        }
      });
    }
  }

  static String _formatScore(double score) =>
      score == score.roundToDouble() ? score.toInt().toString() : score.toStringAsFixed(2);

  void _setDirty(bool dirty) {
    if (_dirty != dirty) {
      setState(() => _dirty = dirty);
      widget.onDirtyChanged(dirty);
    }
  }

  Future<void> _changePeriod(int period) async {
    if (period == _period) return;
    if (_dirty && !await widget.confirmDiscard()) return;
    setState(() {
      _period = period;
      _loadedKey = null;
    });
    _setDirty(false);
  }

  static String? _validate(String? value) {
    final text = (value ?? '').trim().replaceAll(',', '.');
    if (text.isEmpty) return null;
    final number = double.tryParse(text);
    if (number == null) return 'Número';
    if (number < 0 || number > 100) return '0–100';
    if (!RegExp(r'^\d{1,3}(\.\d{1,2})?$').hasMatch(text)) return '2 decimales';
    return null;
  }

  Future<void> _save(GradebookSheet sheet) async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() => _saving = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref
          .read(teachingActionsProvider)
          .saveGradebook(
            GradebookSheet(
              groupId: sheet.groupId,
              period: sheet.period,
              state: sheet.state,
              roster: sheet.roster,
              scores: {
                for (final s in sheet.roster)
                  s.id: double.tryParse(_controllers[s.id]!.text.trim().replaceAll(',', '.')),
              },
              savedAt: sheet.savedAt,
            ),
          );
      _setDirty(false);
      messenger.showSnackBar(
        const SnackBar(content: Text('Borrador guardado. No se publicó a familias.')),
      );
    } on Object catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(friendlyError(error))));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final arg = (groupId: widget.group.id, period: _period);
    final sheet = ref.watch(gradebookProvider(arg));
    final format = ref.watch(formattersProvider);
    final terms = ref.watch(institutionProvider).terms;
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return Column(
      children: [
        SizedBox(
          height: 56,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.fromLTRB(Spacing.page, Spacing.md, Spacing.page, 0),
            children: [
              for (var p = 1; p <= 4; p++)
                Padding(
                  padding: const EdgeInsets.only(right: Spacing.sm),
                  child: ChoiceChip(
                    label: Text('$p.º ${terms.period.toLowerCase()}'),
                    selected: _period == p,
                    onSelected: _saving ? null : (_) => _changePeriod(p),
                  ),
                ),
            ],
          ),
        ),
        Expanded(
          child: AsyncValueView(
            value: sheet,
            onRetry: () => ref.invalidate(gradebookProvider(arg)),
            data: (data) {
              if (data.period != _period) return const SizedBox.shrink();
              _load(data);
              final locked = data.state == GradebookState.published;
              final captured = data.roster
                  .where((s) => _controllers[s.id]!.text.trim().isNotEmpty)
                  .length;
              return Column(
                children: [
                  Expanded(
                    child: Form(
                      key: _formKey,
                      child: ListView(
                        padding: const EdgeInsets.fromLTRB(
                          Spacing.page,
                          Spacing.md,
                          Spacing.page,
                          Spacing.lg,
                        ),
                        children: [
                          Row(
                            children: [
                              StatusPill(
                                label: data.state.label,
                                tone: locked ? AppTone.success : AppTone.blue,
                                icon: locked ? Icons.lock_rounded : Icons.edit_rounded,
                              ),
                              const SizedBox(width: Spacing.sm),
                              Expanded(
                                child: Text(
                                  'Capturadas $captured de ${data.roster.length}',
                                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                                  textAlign: TextAlign.end,
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: Spacing.sm),
                          Text(
                            locked
                                ? 'Período publicado. Las correcciones requieren autorización de coordinación.'
                                : 'Escribe notas de 0 a 100. Vacío significa pendiente, no cero. Guardar no publica.',
                            style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                          ),
                          const SizedBox(height: Spacing.md),
                          AppCard(
                            padding: const EdgeInsets.symmetric(vertical: Spacing.xs),
                            child: Column(
                              children: [
                                for (var i = 0; i < data.roster.length; i++) ...[
                                  if (i > 0)
                                    const Divider(indent: Spacing.lg, endIndent: Spacing.lg),
                                  Padding(
                                    padding: const EdgeInsets.symmetric(
                                      horizontal: Spacing.md,
                                      vertical: Spacing.sm,
                                    ),
                                    child: Row(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Padding(
                                          padding: const EdgeInsets.only(top: 4),
                                          child: InitialsAvatar(
                                            initials: data.roster[i].initials,
                                            size: 36,
                                            tone: AppTone.neutral,
                                          ),
                                        ),
                                        const SizedBox(width: Spacing.sm),
                                        Expanded(
                                          child: Padding(
                                            padding: const EdgeInsets.only(top: 12),
                                            child: Text(
                                              data.roster[i].fullName,
                                              maxLines: 2,
                                              overflow: TextOverflow.ellipsis,
                                              style: theme.textTheme.bodyMedium?.copyWith(
                                                fontWeight: FontWeight.w600,
                                              ),
                                            ),
                                          ),
                                        ),
                                        SizedBox(
                                          width: 92,
                                          child: TextFormField(
                                            controller: _controllers[data.roster[i].id],
                                            enabled: !locked && !_saving,
                                            textAlign: TextAlign.center,
                                            keyboardType: const TextInputType.numberWithOptions(
                                              decimal: true,
                                            ),
                                            textInputAction: i == data.roster.length - 1
                                                ? TextInputAction.done
                                                : TextInputAction.next,
                                            inputFormatters: [
                                              FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]')),
                                              LengthLimitingTextInputFormatter(6),
                                            ],
                                            decoration: InputDecoration(
                                              hintText: '—',
                                              isDense: true,
                                              contentPadding: const EdgeInsets.symmetric(
                                                horizontal: 8,
                                                vertical: 12,
                                              ),
                                              errorStyle: const TextStyle(fontSize: 11),
                                              semanticCounterText: data.roster[i].fullName,
                                            ),
                                            autovalidateMode: AutovalidateMode.onUserInteraction,
                                            validator: _validate,
                                            onChanged: (_) {
                                              _setDirty(true);
                                              setState(() {});
                                            },
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ],
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  if (!locked)
                    _SaveBar(
                      label: 'Guardar borrador',
                      saving: _saving,
                      enabled: _dirty,
                      onPressed: () => _save(data),
                      footnote: data.savedAt == null
                          ? null
                          : 'Último guardado: ${format.relativeDateTime(data.savedAt!, ref.read(clockProvider)())}',
                    ),
                ],
              );
            },
          ),
        ),
      ],
    );
  }
}

class _SaveBar extends StatelessWidget {
  const _SaveBar({
    required this.label,
    required this.saving,
    required this.enabled,
    required this.onPressed,
    this.footnote,
  });

  final String label;
  final bool saving;
  final bool enabled;
  final VoidCallback onPressed;
  final String? footnote;

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: colors.cardBackground,
        border: Border(top: BorderSide(color: colors.border)),
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(Spacing.page, Spacing.md, Spacing.page, Spacing.md),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (footnote != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: Spacing.sm),
                  child: Text(
                    footnote!,
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(color: colors.muted),
                  ),
                ),
              FilledButton(
                onPressed: saving || !enabled ? null : onPressed,
                child: saving
                    ? const SizedBox.square(
                        dimension: 22,
                        child: CircularProgressIndicator(strokeWidth: 2.5),
                      )
                    : Text(label),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
