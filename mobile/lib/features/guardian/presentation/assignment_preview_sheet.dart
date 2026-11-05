import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/format/formatters.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/assignments.dart';
import '../../../shared/visuals.dart';

/// Vista de solo lectura de una tarea para la familia.
Future<void> showAssignmentPreview(BuildContext context, Assignment assignment) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (context) => Consumer(
      builder: (context, ref, _) {
        final theme = Theme.of(context);
        final format = ref.watch(formattersProvider);
        return SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(Spacing.xl, 0, Spacing.xl, Spacing.xl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    ToneIcon(
                      icon: assignment.subject.area.icon,
                      tone: assignment.subject.area.tone,
                    ),
                    const SizedBox(width: Spacing.md),
                    Expanded(
                      child: Text(assignment.subject.name, style: theme.textTheme.titleSmall),
                    ),
                    StatusPill(label: assignment.status.label, tone: assignment.status.tone),
                  ],
                ),
                const SizedBox(height: Spacing.lg),
                Text(assignment.title, style: theme.textTheme.headlineSmall),
                const SizedBox(height: Spacing.md),
                InfoRow(
                  icon: Icons.event_rounded,
                  label: 'Entrega',
                  value:
                      '${Formatters.capitalize(format.weekdayDayMonth(assignment.dueAt))}, ${format.time(assignment.dueAt)}',
                ),
                const SizedBox(height: Spacing.md),
                Text(
                  assignment.instructions,
                  style: theme.textTheme.bodyMedium?.copyWith(height: 1.5),
                ),
                const SizedBox(height: Spacing.lg),
                Text(
                  'La entrega la realiza el estudiante desde su propia cuenta.',
                  style: theme.textTheme.bodySmall,
                ),
              ],
            ),
          ),
        );
      },
    ),
  );
}
