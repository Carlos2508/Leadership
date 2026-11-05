import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/states.dart';
import '../../academics/application/academic_providers.dart';
import '../../auth/application/auth_controller.dart';

/// Selector visible del hijo o hija: toda acción familiar indica a quién aplica.
class ChildSelector extends ConsumerWidget {
  const ChildSelector({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(requireSessionProvider);
    final selected = ref.watch(selectedChildProvider);
    if (session.linkedStudentIds.length < 2) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.only(bottom: Spacing.lg),
      child: Semantics(
        label: 'Elegir estudiante',
        container: true,
        child: SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              for (final id in session.linkedStudentIds)
                Padding(
                  padding: const EdgeInsets.only(right: Spacing.sm),
                  child: _ChildChip(studentId: id, selected: id == selected),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ChildChip extends ConsumerWidget {
  const _ChildChip({required this.studentId, required this.selected});

  final String studentId;
  final bool selected;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final student = ref.watch(studentProvider(studentId));
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final data = student.value;
    if (data == null) {
      return const SkeletonBox(height: 56, width: 170);
    }
    final pair = colors.tone(AppTone.brand);
    return Semantics(
      selected: selected,
      button: true,
      label: '${data.fullName}, ${data.placement}',
      excludeSemantics: true,
      child: Material(
        color: selected ? pair.background : colors.cardBackground,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(Radii.md),
          side: BorderSide(
            color: selected ? pair.foreground : colors.border,
            width: selected ? 1.6 : 1,
          ),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(Radii.md),
          onTap: () => ref.read(selectedChildProvider.notifier).select(studentId),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(8, 8, 14, 8),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                InitialsAvatar(
                  initials: data.initials,
                  size: 38,
                  tone: selected ? AppTone.accent : AppTone.neutral,
                ),
                const SizedBox(width: Spacing.sm),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(data.firstName, style: theme.textTheme.titleSmall),
                    Text(
                      data.placement,
                      style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
