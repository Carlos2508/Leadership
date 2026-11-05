import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/states.dart';
import '../../../shared/page_layout.dart';
import '../application/teaching_providers.dart';
import 'review_tile.dart';

class ReviewQueuePage extends ConsumerStatefulWidget {
  const ReviewQueuePage({super.key});

  @override
  ConsumerState<ReviewQueuePage> createState() => _ReviewQueuePageState();
}

class _ReviewQueuePageState extends ConsumerState<ReviewQueuePage> {
  bool _pending = true;

  @override
  Widget build(BuildContext context) {
    final queue = ref.watch(reviewQueueProvider);
    final pendingCount = queue.value?.where((r) => !r.reviewed).length;
    return TabPage(
      subtitle: 'Revisión de entregas',
      onRefresh: () => safeRefresh([ref.refresh(reviewQueueProvider.future)]),
      children: [
        const PageIntro(eyebrow: 'Entregas', title: 'Retroalimenta a tiempo'),
        SegmentedButton<bool>(
          showSelectedIcon: false,
          segments: [
            ButtonSegment(
              value: true,
              label: Text(pendingCount == null ? 'Por revisar' : 'Por revisar ($pendingCount)'),
            ),
            const ButtonSegment(value: false, label: Text('Revisadas')),
          ],
          selected: {_pending},
          onSelectionChanged: (value) => setState(() => _pending = value.first),
        ),
        const SizedBox(height: Spacing.lg),
        AsyncValueView(
          value: queue,
          onRetry: () => ref.invalidate(reviewQueueProvider),
          data: (list) {
            final filtered = list.where((r) => r.reviewed != _pending).toList();
            if (filtered.isEmpty) {
              return EmptyState(
                icon: _pending ? Icons.task_alt_rounded : Icons.inbox_rounded,
                title: _pending ? 'Todo revisado' : 'Aún no hay revisiones',
              );
            }
            return Column(
              children: [
                for (final r in filtered) ...[
                  ReviewTile(item: r),
                  const SizedBox(height: Spacing.sm),
                ],
              ],
            );
          },
        ),
      ],
    );
  }
}
