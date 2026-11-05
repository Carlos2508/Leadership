import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/format/formatters.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/assignments.dart';
import '../../../shared/page_layout.dart';
import '../../../shared/visuals.dart';
import '../../academics/application/academic_providers.dart';
import 'submit_assignment_sheet.dart';

class TaskDetailPage extends ConsumerWidget {
  const TaskDetailPage({required this.taskId, super.key});

  final String taskId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final assignment = ref.watch(assignmentProvider(taskId));
    final now = ref.watch(clockProvider)();
    final canSubmit =
        assignment.value != null &&
        assignment.value!.status.isOpen &&
        (assignment.value!.allowsLate || !now.isAfter(assignment.value!.dueAt));

    return DetailPage(
      title: 'Tarea',
      bottomBar: canSubmit
          ? FilledButton.icon(
              onPressed: () => _openSubmit(context, assignment.value!),
              icon: const Icon(Icons.upload_rounded),
              label: const Text('Entregar tarea'),
            )
          : null,
      children: [
        AsyncValueView(
          value: assignment,
          onRetry: () => ref.invalidate(assignmentProvider(taskId)),
          data: (a) => _TaskBody(assignment: a),
        ),
      ],
    );
  }

  Future<void> _openSubmit(BuildContext context, Assignment assignment) async {
    final submitted = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => SubmitAssignmentSheet(assignment: assignment),
    );
    if (submitted == true && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('¡Tarea entregada! Tu docente ya puede revisarla.')),
      );
    }
  }
}

class _TaskBody extends ConsumerWidget {
  const _TaskBody({required this.assignment});

  final Assignment assignment;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final format = ref.watch(formattersProvider);
    final submission = assignment.submission;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            ToneIcon(icon: assignment.subject.area.icon, tone: assignment.subject.area.tone),
            const SizedBox(width: Spacing.md),
            Expanded(
              child: Text(
                assignment.subject.name,
                style: theme.textTheme.titleSmall?.copyWith(color: colors.muted),
              ),
            ),
            StatusPill(
              label: assignment.status.label,
              tone: assignment.status.tone,
              icon: assignment.status.icon,
            ),
          ],
        ),
        const SizedBox(height: Spacing.lg),
        Semantics(
          header: true,
          child: Text(assignment.title, style: theme.textTheme.headlineSmall),
        ),
        const SizedBox(height: Spacing.lg),
        AppCard(
          child: Column(
            children: [
              InfoRow(
                icon: Icons.event_rounded,
                label: 'Fecha de entrega',
                value:
                    '${Formatters.capitalize(format.weekdayDayMonth(assignment.dueAt))}, ${format.time(assignment.dueAt)}',
              ),
              InfoRow(
                icon: Icons.person_outline_rounded,
                label: 'Docente',
                value: assignment.subject.teacherName,
              ),
              InfoRow(
                icon: Icons.update_rounded,
                label: 'Entregas tardías',
                value: assignment.allowsLate ? 'Permitidas' : 'No permitidas',
              ),
            ],
          ),
        ),
        const SectionHeader(title: 'Instrucciones'),
        Text(assignment.instructions, style: theme.textTheme.bodyLarge?.copyWith(height: 1.5)),
        if (submission != null) ...[
          const SectionHeader(title: 'Mi entrega'),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Enviada ${format.relativeDateTime(submission.submittedAt, ref.watch(clockProvider)())}${submission.late ? ' · fuera de plazo' : ''}',
                  style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: Spacing.md),
                for (final file in submission.files)
                  Padding(
                    padding: const EdgeInsets.only(bottom: Spacing.sm),
                    child: Row(
                      children: [
                        Icon(Icons.attach_file_rounded, size: 18, color: colors.muted),
                        const SizedBox(width: Spacing.sm),
                        Expanded(child: Text(file, overflow: TextOverflow.ellipsis)),
                      ],
                    ),
                  ),
                if (submission.comment.isNotEmpty)
                  Text(
                    '“${submission.comment}”',
                    style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic),
                  ),
              ],
            ),
          ),
        ],
        if (assignment.teacherFeedback != null) ...[
          const SectionHeader(title: 'Comentario del docente'),
          AppCard(
            tone: AppTone.success,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  assignment.teacherFeedback!,
                  style: theme.textTheme.bodyMedium?.copyWith(height: 1.45),
                ),
                const SizedBox(height: Spacing.md),
                Text(
                  'El punteo se consulta en Notas cuando esté publicado.',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: colors.tone(AppTone.success).foreground,
                  ),
                ),
              ],
            ),
          ),
        ],
        if (assignment.status == AssignmentStatus.missing) ...[
          const SizedBox(height: Spacing.xl),
          AppCard(
            tone: AppTone.danger,
            child: Text(
              'El plazo terminó y esta tarea no admite entregas tardías. Si tuviste un inconveniente, habla con tu docente.',
              style: theme.textTheme.bodyMedium,
            ),
          ),
        ],
      ],
    );
  }
}
