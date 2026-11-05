import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/format/formatters.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/assignments.dart';
import '../../../shared/page_layout.dart';
import '../../academics/application/academic_providers.dart';
import '../../academics/presentation/academic_widgets.dart';
import 'assignment_preview_sheet.dart';
import 'child_selector.dart';

class GuardianAgendaPage extends ConsumerStatefulWidget {
  const GuardianAgendaPage({super.key});

  @override
  ConsumerState<GuardianAgendaPage> createState() => _GuardianAgendaPageState();
}

class _GuardianAgendaPageState extends ConsumerState<GuardianAgendaPage> {
  bool _calendar = true;

  @override
  Widget build(BuildContext context) {
    final childId = ref.watch(selectedChildProvider);
    final events = ref.watch(eventsProvider);
    final assignments = childId == null ? null : ref.watch(assignmentsProvider(childId));

    return TabPage(
      subtitle: 'Agenda del colegio',
      onRefresh: () => safeRefresh([
        ref.refresh(eventsProvider.future),
        if (childId != null) ref.refresh(assignmentsProvider(childId).future),
      ]),
      children: [
        const PageIntro(eyebrow: 'Calendario y tareas', title: 'Todo lo que viene'),
        const ChildSelector(),
        SegmentedButton<bool>(
          showSelectedIcon: false,
          segments: const [
            ButtonSegment(
              value: true,
              label: Text('Calendario'),
              icon: Icon(Icons.calendar_month_rounded),
            ),
            ButtonSegment(value: false, label: Text('Tareas'), icon: Icon(Icons.checklist_rounded)),
          ],
          selected: {_calendar},
          onSelectionChanged: (value) => setState(() => _calendar = value.first),
        ),
        const SizedBox(height: Spacing.lg),
        if (_calendar)
          AsyncValueView(
            value: events,
            onRetry: () => ref.invalidate(eventsProvider),
            data: (list) => AgendaCalendar(
              events: list,
              extraMarkedDays: {
                for (final a in assignments?.value ?? const <Assignment>[])
                  if (a.status.isOpen) dateOnly(a.dueAt),
              },
            ),
          )
        else if (assignments != null)
          AsyncValueView(
            value: assignments,
            onRetry: () => ref.invalidate(assignmentsProvider(childId!)),
            data: (list) => list.isEmpty
                ? const AppCard(child: Text('Sin tareas asignadas.'))
                : Column(
                    children: [
                      for (final a in list.reversed) ...[
                        AssignmentTile(
                          assignment: a,
                          onTap: () => showAssignmentPreview(context, a),
                        ),
                        const SizedBox(height: Spacing.sm),
                      ],
                    ],
                  ),
          ),
      ],
    );
  }
}
