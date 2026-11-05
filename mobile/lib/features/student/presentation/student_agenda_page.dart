import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/format/formatters.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/states.dart';
import '../../../domain/models/assignments.dart';
import '../../../shared/page_layout.dart';
import '../../academics/application/academic_providers.dart';
import '../../academics/presentation/academic_widgets.dart';
import '../../auth/application/auth_controller.dart';

enum _AgendaView { pending, done, calendar }

class StudentAgendaPage extends ConsumerStatefulWidget {
  const StudentAgendaPage({super.key});

  @override
  ConsumerState<StudentAgendaPage> createState() => _StudentAgendaPageState();
}

class _StudentAgendaPageState extends ConsumerState<StudentAgendaPage> {
  _AgendaView _view = _AgendaView.pending;

  @override
  Widget build(BuildContext context) {
    final studentId = ref.watch(requireSessionProvider).studentId!;
    final assignments = ref.watch(assignmentsProvider(studentId));
    final events = ref.watch(eventsProvider);
    final pendingCount = assignments.value?.where((a) => a.status.isOpen).length;

    return TabPage(
      subtitle: 'Tareas y calendario',
      onRefresh: () => safeRefresh([
        ref.refresh(assignmentsProvider(studentId).future),
        ref.refresh(eventsProvider.future),
      ]),
      children: [
        const PageIntro(eyebrow: 'Mi plan de la semana', title: 'A tu ritmo, sin olvidar nada'),
        SegmentedButton<_AgendaView>(
          showSelectedIcon: false,
          segments: [
            ButtonSegment(
              value: _AgendaView.pending,
              label: Text(pendingCount == null ? 'Pendientes' : 'Pendientes ($pendingCount)'),
            ),
            const ButtonSegment(value: _AgendaView.done, label: Text('Terminadas')),
            const ButtonSegment(value: _AgendaView.calendar, label: Text('Calendario')),
          ],
          selected: {_view},
          onSelectionChanged: (value) => setState(() => _view = value.first),
        ),
        const SizedBox(height: Spacing.lg),
        switch (_view) {
          _AgendaView.calendar => AsyncValueView(
            value: events,
            onRetry: () => ref.invalidate(eventsProvider),
            data: (list) => AgendaCalendar(
              events: list,
              extraMarkedDays: {
                for (final a in assignments.value ?? const <Assignment>[])
                  if (a.status.isOpen) dateOnly(a.dueAt),
              },
            ),
          ),
          _ => AsyncValueView(
            value: assignments,
            onRetry: () => ref.invalidate(assignmentsProvider(studentId)),
            data: (list) => _AssignmentList(
              assignments: _view == _AgendaView.pending
                  ? list.where((a) => a.status.isOpen).toList()
                  : list.where((a) => !a.status.isOpen).toList().reversed.toList(),
              pending: _view == _AgendaView.pending,
            ),
          ),
        },
      ],
    );
  }
}

class _AssignmentList extends StatelessWidget {
  const _AssignmentList({required this.assignments, required this.pending});

  final List<Assignment> assignments;
  final bool pending;

  @override
  Widget build(BuildContext context) {
    if (assignments.isEmpty) {
      return AppCard(
        child: EmptyState(
          icon: pending ? Icons.celebration_rounded : Icons.inbox_rounded,
          title: pending ? '¡Todo entregado!' : 'Aún no hay tareas terminadas',
          message: pending ? 'Disfruta tu tiempo libre o adelanta lectura.' : null,
        ),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (pending) ...[
          HeroBanner(
            tag: 'Esta semana',
            title:
                '${assignments.length} ${assignments.length == 1 ? 'tarea' : 'tareas'} por completar',
            message: 'Organízate y date tiempo para lo que te gusta.',
            icon: Icons.edit_calendar_rounded,
          ),
          const SizedBox(height: Spacing.lg),
        ],
        for (final a in assignments) ...[
          AssignmentTile(assignment: a, onTap: () => context.push('/tasks/${a.id}')),
          const SizedBox(height: Spacing.sm),
        ],
      ],
    );
  }
}
