import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/format/formatters.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/month_calendar.dart';
import '../../../domain/models/academics.dart';
import '../../../domain/models/assignments.dart';
import '../../../shared/visuals.dart';

/// Fila de tarea con materia, fecha de entrega y estado.
class AssignmentTile extends ConsumerWidget {
  const AssignmentTile({required this.assignment, this.onTap, super.key});

  final Assignment assignment;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final format = ref.watch(formattersProvider);
    final now = ref.watch(clockProvider)();
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final due = assignment.status.isOpen
        ? 'Entrega: ${format.relativeDateTime(assignment.dueAt, now)}'
        : assignment.submission != null
        ? 'Entregada: ${format.relativeDay(assignment.submission!.submittedAt, now)}'
        : 'Venció: ${format.relativeDay(assignment.dueAt, now)}';
    return AppCard(
      onTap: onTap,
      padding: const EdgeInsets.all(Spacing.md),
      semanticLabel:
          '${assignment.title}, ${assignment.subject.name}. $due. ${assignment.status.label}',
      child: ExcludeSemantics(
        child: Row(
          children: [
            ToneIcon(icon: assignment.subject.area.icon, tone: assignment.subject.area.tone),
            const SizedBox(width: Spacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    assignment.title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleSmall,
                  ),
                  const SizedBox(height: 2),
                  Text(
                    '${assignment.subject.name} · $due',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                  ),
                ],
              ),
            ),
            const SizedBox(width: Spacing.sm),
            StatusPill(label: _shortStatus(assignment, now), tone: _tone(assignment, now)),
          ],
        ),
      ),
    );
  }

  static String _shortStatus(Assignment a, DateTime now) {
    if (!a.status.isOpen) return a.status.label;
    final days = dateOnly(a.dueAt).difference(dateOnly(now)).inDays;
    if (a.dueAt.isBefore(now)) return 'Atrasada';
    return switch (days) {
      0 => 'Hoy',
      1 => 'Mañana',
      _ => 'En $days días',
    };
  }

  static AppTone _tone(Assignment a, DateTime now) {
    if (a.status.isOpen) {
      final days = dateOnly(a.dueAt).difference(dateOnly(now)).inDays;
      if (a.dueAt.isBefore(now)) return AppTone.danger;
      return days <= 1 ? AppTone.coral : AppTone.accent;
    }
    return a.status.tone;
  }
}

/// Fila de evento del calendario escolar.
class EventTile extends ConsumerWidget {
  const EventTile({required this.event, super.key});

  final SchoolEvent event;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final format = ref.watch(formattersProvider);
    final now = ref.watch(clockProvider)();
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final when = event.allDay
        ? '${format.relativeDay(event.start, now)} · Todo el día'
        : format.relativeDateTime(event.start, now);
    return AppCard(
      padding: const EdgeInsets.all(Spacing.md),
      onTap: () => showEventSheet(context, event, format),
      semanticLabel: '${event.title}, $when, ${event.location}',
      child: ExcludeSemantics(
        child: Row(
          children: [
            ToneIcon(icon: event.category.icon, tone: event.category.tone),
            const SizedBox(width: Spacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    event.title,
                    style: theme.textTheme.titleSmall,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 2),
                  Text(
                    when,
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
            const SizedBox(width: Spacing.sm),
            StatusPill(label: event.category.label, tone: event.category.tone),
          ],
        ),
      ),
    );
  }
}

Future<void> showEventSheet(BuildContext context, SchoolEvent event, Formatters format) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (context) {
      final theme = Theme.of(context);
      final time = event.allDay
          ? 'Todo el día'
          : '${format.time(event.start)}${event.end == null ? '' : ' – ${format.time(event.end!)}'}';
      return SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(Spacing.xl, 0, Spacing.xl, Spacing.xl),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              StatusPill(
                label: event.category.label,
                tone: event.category.tone,
                icon: event.category.icon,
              ),
              const SizedBox(height: Spacing.md),
              Text(event.title, style: theme.textTheme.headlineSmall),
              const SizedBox(height: Spacing.lg),
              InfoRow(
                icon: Icons.calendar_today_rounded,
                label: 'Fecha',
                value: Formatters.capitalize(format.weekdayDayMonth(event.start)),
              ),
              InfoRow(icon: Icons.schedule_rounded, label: 'Hora', value: time),
              InfoRow(icon: Icons.place_outlined, label: 'Lugar', value: event.location),
              const SizedBox(height: Spacing.md),
              Text(event.description, style: theme.textTheme.bodyMedium?.copyWith(height: 1.5)),
            ],
          ),
        ),
      );
    },
  );
}

/// Calendario + lista de eventos del día o del mes visible.
class AgendaCalendar extends ConsumerStatefulWidget {
  const AgendaCalendar({required this.events, this.extraMarkedDays = const {}, super.key});

  final List<SchoolEvent> events;

  /// Días adicionales a marcar (p. ej. entregas de tareas).
  final Set<DateTime> extraMarkedDays;

  @override
  ConsumerState<AgendaCalendar> createState() => _AgendaCalendarState();
}

class _AgendaCalendarState extends ConsumerState<AgendaCalendar> {
  late DateTime _month;
  DateTime? _selected;

  @override
  void initState() {
    super.initState();
    _month = dateOnly(ref.read(clockProvider)());
  }

  @override
  Widget build(BuildContext context) {
    final format = ref.watch(formattersProvider);
    final today = dateOnly(ref.watch(clockProvider)());
    final theme = Theme.of(context);
    final marked = {for (final e in widget.events) dateOnly(e.start), ...widget.extraMarkedDays};
    final visible = widget.events.where((e) {
      if (_selected != null) return isSameDay(e.start, _selected!);
      return e.start.year == _month.year &&
          e.start.month == _month.month &&
          !e.start.isBefore(today);
    }).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        AppCard(
          child: MonthCalendar(
            month: _month,
            today: today,
            selectedDay: _selected,
            markedDays: marked,
            formatters: format,
            onDaySelected: (day) => setState(
              () => _selected = _selected != null && isSameDay(_selected!, day) ? null : day,
            ),
            onMonthChanged: (month) => setState(() {
              _month = month;
              _selected = null;
            }),
          ),
        ),
        SectionHeader(
          title: _selected == null
              ? 'Próximos eventos'
              : Formatters.capitalize(format.weekdayDayMonth(_selected!)),
          actionLabel: _selected == null ? null : 'Ver mes',
          onAction: _selected == null ? null : () => setState(() => _selected = null),
        ),
        if (visible.isEmpty)
          AppCard(
            child: Text(
              _selected == null ? 'No hay más eventos este mes.' : 'Sin eventos para este día.',
              style: theme.textTheme.bodyMedium?.copyWith(color: AppColors.of(context).muted),
            ),
          )
        else
          for (final event in visible) ...[
            EventTile(event: event),
            const SizedBox(height: Spacing.sm),
          ],
      ],
    );
  }
}
