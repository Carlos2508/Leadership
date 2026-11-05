import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/teaching.dart';

class ReviewTile extends ConsumerWidget {
  const ReviewTile({required this.item, super.key});

  final ReviewItem item;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final format = ref.watch(formattersProvider);
    final now = ref.watch(clockProvider)();
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return AppCard(
      padding: const EdgeInsets.all(Spacing.md),
      onTap: () => context.push('/reviews/${item.id}'),
      semanticLabel:
          '${item.student.fullName}, ${item.assignmentTitle}, ${item.reviewed ? 'revisada' : 'por revisar'}',
      child: ExcludeSemantics(
        child: Row(
          children: [
            InitialsAvatar(initials: item.student.initials, size: 42, tone: AppTone.blue),
            const SizedBox(width: Spacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    item.student.fullName,
                    style: theme.textTheme.titleSmall,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(
                    '${item.assignmentTitle} · ${item.group.placement}',
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(
                    'Entregada ${format.relativeDateTime(item.submittedAt, now)}',
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
            const SizedBox(width: Spacing.sm),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                if (item.reviewed)
                  StatusPill(
                    label: '${format.decimal(item.score!)}/${item.maxScore}',
                    tone: AppTone.success,
                  )
                else
                  const StatusPill(label: 'Por revisar', tone: AppTone.accent),
                if (item.late) ...[
                  const SizedBox(height: 4),
                  const StatusPill(label: 'Tarde', tone: AppTone.warning),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }
}
