import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/responsive_grid.dart';
import '../../../shared/page_layout.dart';
import '../../../shared/visuals.dart';
import '../application/teaching_providers.dart';

class GroupsPage extends ConsumerWidget {
  const GroupsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final groups = ref.watch(groupsProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return TabPage(
      subtitle: 'Mis grupos',
      onRefresh: () => safeRefresh([ref.refresh(groupsProvider.future)]),
      children: [
        const PageIntro(
          eyebrow: 'Espacio docente',
          title: 'Acompaña cada logro',
          subtitle: 'Asistencia, libreta y estudiantes de cada grupo.',
        ),
        AsyncValueView(
          value: groups,
          onRetry: () => ref.invalidate(groupsProvider),
          data: (list) => ResponsiveGrid(
            minItemWidth: 260,
            maxColumns: 2,
            children: [
              for (final g in list)
                AppCard(
                  onTap: () => context.push('/groups/${g.id}'),
                  semanticLabel: '${g.subject.name}, ${g.placement}, ${g.studentCount} estudiantes',
                  child: ExcludeSemantics(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            ToneIcon(icon: g.subject.area.icon, tone: g.subject.area.tone),
                            const Spacer(),
                            StatusPill(
                              label: '${g.studentCount} estudiantes',
                              tone: AppTone.neutral,
                            ),
                          ],
                        ),
                        const SizedBox(height: Spacing.lg),
                        Text(g.placement, style: theme.textTheme.titleLarge),
                        Text(g.subject.name, style: theme.textTheme.bodyMedium),
                        const SizedBox(height: Spacing.sm),
                        Row(
                          children: [
                            Icon(Icons.place_outlined, size: 16, color: colors.muted),
                            const SizedBox(width: 4),
                            Expanded(
                              child: Text(
                                g.room,
                                style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }
}
