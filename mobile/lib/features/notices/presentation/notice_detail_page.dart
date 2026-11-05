import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/format/formatters.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/states.dart';
import '../../../shared/page_layout.dart';
import '../application/notices_providers.dart';

class NoticeDetailPage extends ConsumerStatefulWidget {
  const NoticeDetailPage({required this.noticeId, super.key});

  final String noticeId;

  @override
  ConsumerState<NoticeDetailPage> createState() => _NoticeDetailPageState();
}

class _NoticeDetailPageState extends ConsumerState<NoticeDetailPage> {
  bool _marked = false;

  void _markReadOnce() {
    if (_marked) return;
    _marked = true;
    // Se marca como leído después del primer cuadro para no modificar
    // proveedores durante la construcción.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(noticeActionsProvider).markRead(widget.noticeId).ignore();
    });
  }

  @override
  Widget build(BuildContext context) {
    final notices = ref.watch(noticesProvider);
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return DetailPage(
      title: 'Aviso',
      children: [
        AsyncValueView(
          value: notices,
          onRetry: () => ref.invalidate(noticesProvider),
          data: (list) {
            final notice = list.where((n) => n.id == widget.noticeId).firstOrNull;
            if (notice == null) {
              return const EmptyState(
                icon: Icons.search_off_rounded,
                title: 'Este aviso ya no está disponible',
              );
            }
            if (!notice.read) _markReadOnce();
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Wrap(
                  spacing: Spacing.sm,
                  children: [
                    StatusPill(label: notice.channel.label, tone: AppTone.brand),
                    if (notice.important)
                      const StatusPill(label: 'Importante', tone: AppTone.accent),
                  ],
                ),
                const SizedBox(height: Spacing.lg),
                Semantics(
                  header: true,
                  child: Text(notice.title, style: theme.textTheme.headlineSmall),
                ),
                const SizedBox(height: Spacing.sm),
                Text(
                  '${notice.sender} · ${Formatters.capitalize(format.weekdayDayMonth(notice.sentAt))}, ${format.time(notice.sentAt)}',
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
                const SizedBox(height: Spacing.xl),
                AppCard(
                  child: Text(
                    notice.body,
                    style: theme.textTheme.bodyLarge?.copyWith(height: 1.55),
                  ),
                ),
              ],
            );
          },
        ),
      ],
    );
  }
}
