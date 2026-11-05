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
import '../../../domain/models/communication.dart';
import '../../../shared/page_layout.dart';
import '../../auth/application/auth_controller.dart';
import '../application/notices_providers.dart';

class NoticesPage extends ConsumerStatefulWidget {
  const NoticesPage({super.key});

  @override
  ConsumerState<NoticesPage> createState() => _NoticesPageState();
}

class _NoticesPageState extends ConsumerState<NoticesPage> {
  NoticeChannel? _channel;
  bool _markingAll = false;

  Future<void> _markAll() async {
    setState(() => _markingAll = true);
    try {
      await ref.read(noticeActionsProvider).markAllRead(ref.read(requireSessionProvider).role);
    } on Object catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(friendlyError(error))));
      }
    } finally {
      if (mounted) setState(() => _markingAll = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final notices = ref.watch(noticesProvider);
    final unread = ref.watch(unreadNoticesProvider);

    return TabPage(
      subtitle: 'Centro de mensajes',
      onRefresh: () => safeRefresh([ref.refresh(noticesProvider.future)]),
      children: [
        PageIntro(
          eyebrow: 'Noticias para ti',
          title: 'Siempre conectados',
          subtitle: unread == 0
              ? 'Estás al día.'
              : 'Tienes $unread ${unread == 1 ? 'aviso sin leer' : 'avisos sin leer'}.',
        ),
        Row(
          children: [
            Expanded(
              child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    for (final channel in [null, ...NoticeChannel.values])
                      Padding(
                        padding: const EdgeInsets.only(right: Spacing.sm),
                        child: ChoiceChip(
                          label: Text(channel?.label ?? 'Todos'),
                          selected: _channel == channel,
                          onSelected: (_) => setState(() => _channel = channel),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            if (unread > 0)
              IconButton(
                tooltip: 'Marcar todo como leído',
                onPressed: _markingAll ? null : _markAll,
                icon: const Icon(Icons.done_all_rounded),
              ),
          ],
        ),
        const SizedBox(height: Spacing.md),
        AsyncValueView(
          value: notices,
          onRetry: () => ref.invalidate(noticesProvider),
          data: (list) {
            final filtered = list.where((n) => _channel == null || n.channel == _channel).toList();
            if (filtered.isEmpty) {
              return const EmptyState(
                icon: Icons.notifications_off_outlined,
                title: 'Sin avisos',
                message: 'Cuando haya novedades aparecerán aquí.',
              );
            }
            return Column(
              children: [
                for (final notice in filtered) ...[
                  NoticeTile(notice: notice),
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

class NoticeTile extends ConsumerWidget {
  const NoticeTile({required this.notice, super.key});

  final Notice notice;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final format = ref.watch(formattersProvider);
    final now = ref.watch(clockProvider)();
    final tone = switch (notice.channel) {
      NoticeChannel.school => AppTone.coral,
      NoticeChannel.teacher => AppTone.blue,
      NoticeChannel.system => AppTone.purple,
    };
    final icon = switch (notice.channel) {
      NoticeChannel.school => Icons.campaign_rounded,
      NoticeChannel.teacher => Icons.co_present_rounded,
      NoticeChannel.system => Icons.auto_awesome_rounded,
    };
    return AppCard(
      padding: EdgeInsets.zero,
      onTap: () => context.push('/notices/${notice.id}'),
      semanticLabel:
          '${notice.read ? '' : 'Sin leer. '}${notice.title}. ${notice.sender}, ${format.relativeDay(notice.sentAt, now)}',
      child: ExcludeSemantics(
        child: IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Container(width: 4, color: notice.read ? Colors.transparent : colors.accent),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.all(Spacing.md),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      ToneIcon(icon: icon, tone: tone, size: 40),
                      const SizedBox(width: Spacing.md),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    notice.title,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: theme.textTheme.titleSmall?.copyWith(
                                      fontWeight: notice.read ? FontWeight.w600 : FontWeight.w800,
                                    ),
                                  ),
                                ),
                                const SizedBox(width: Spacing.sm),
                                Text(
                                  format.relativeDay(notice.sentAt, now),
                                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(
                              notice.body,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: theme.textTheme.bodySmall?.copyWith(height: 1.4),
                            ),
                            const SizedBox(height: Spacing.sm),
                            Wrap(
                              spacing: Spacing.sm,
                              runSpacing: 4,
                              children: [
                                StatusPill(label: notice.channel.label, tone: tone),
                                if (notice.important)
                                  const StatusPill(
                                    label: 'Importante',
                                    tone: AppTone.accent,
                                    icon: Icons.priority_high_rounded,
                                  ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
