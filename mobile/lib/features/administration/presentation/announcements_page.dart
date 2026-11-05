import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/states.dart';
import '../../../shared/page_layout.dart';
import '../application/administration_providers.dart';

class AnnouncementsPage extends ConsumerWidget {
  const AnnouncementsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final announcements = ref.watch(announcementsProvider);
    final format = ref.watch(formattersProvider);
    final now = ref.watch(clockProvider)();
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return TabPage(
      subtitle: 'Comunicación',
      onRefresh: () => safeRefresh([ref.refresh(announcementsProvider.future)]),
      floatingActionButton: FloatingActionButton.extended(
        heroTag: null,
        onPressed: () => context.push('/announcements/new'),
        icon: const Icon(Icons.add_rounded),
        label: const Text('Nuevo'),
      ),
      children: [
        const PageIntro(
          eyebrow: 'Comunicados',
          title: 'Cercanía con cada familia',
          subtitle: 'Mensajes claros para la audiencia correcta.',
        ),
        AsyncValueView(
          value: announcements,
          onRetry: () => ref.invalidate(announcementsProvider),
          data: (list) {
            if (list.isEmpty) {
              return const EmptyState(
                icon: Icons.campaign_outlined,
                title: 'Aún no hay comunicados',
              );
            }
            return Column(
              children: [
                for (final a in list) ...[
                  AppCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(child: Text(a.title, style: theme.textTheme.titleSmall)),
                            if (a.important)
                              const StatusPill(label: 'Importante', tone: AppTone.accent),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '${a.author} · ${format.relativeDateTime(a.publishedAt, now)}',
                          style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                        ),
                        const SizedBox(height: Spacing.sm),
                        Text(
                          a.body,
                          maxLines: 3,
                          overflow: TextOverflow.ellipsis,
                          style: theme.textTheme.bodyMedium,
                        ),
                        const SizedBox(height: Spacing.md),
                        Wrap(
                          spacing: Spacing.md,
                          runSpacing: Spacing.xs,
                          crossAxisAlignment: WrapCrossAlignment.center,
                          children: [
                            StatusPill(
                              label: a.audience.label,
                              tone: AppTone.blue,
                              icon: Icons.groups_rounded,
                            ),
                            Text(
                              'Leído por ${a.readCount} de ${a.recipients}',
                              style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                            ),
                          ],
                        ),
                        const SizedBox(height: Spacing.sm),
                        ProgressBar(
                          value: a.recipients == 0 ? 0 : a.readCount / a.recipients,
                          height: 6,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: Spacing.md),
                ],
              ],
            );
          },
        ),
      ],
    );
  }
}
