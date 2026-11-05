import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/responsive_grid.dart';
import '../../../core/widgets/states.dart';
import '../../../domain/models/teaching.dart';
import '../../../shared/page_layout.dart';
import '../../../shared/visuals.dart';
import '../../auth/application/auth_controller.dart';
import '../application/teaching_providers.dart';
import 'review_tile.dart';

class TeacherTodayPage extends ConsumerWidget {
  const TeacherTodayPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(requireSessionProvider);
    final format = ref.watch(formattersProvider);
    final sessions = ref.watch(todaySessionsProvider);
    final reviews = ref.watch(reviewQueueProvider);
    final groups = ref.watch(groupsProvider);
    final now = ref.watch(clockProvider)();
    final pendingReviews = reviews.value?.where((r) => !r.reviewed).toList();
    final pendingAttendance = sessions.value?.where((s) => !s.attendanceTaken).length;

    return TabPage(
      subtitle: session.roleTitle,
      onRefresh: () => safeRefresh([
        ref.refresh(todaySessionsProvider.future),
        ref.refresh(reviewQueueProvider.future),
        ref.refresh(groupsProvider.future),
      ]),
      children: [
        PageIntro(eyebrow: format.weekdayDayMonth(now), title: '¡Buen día, ${session.firstName}!'),
        HeroBanner(
          tag: 'Tu día',
          title: 'Enseñar también es inspirar.',
          message: sessions.value == null
              ? 'Preparando tu agenda…'
              : 'Hoy tienes ${sessions.value!.length} clases y ${pendingReviews?.length ?? 0} entregas por revisar.',
          icon: Icons.lightbulb_rounded,
        ),
        const SizedBox(height: Spacing.lg),
        ResponsiveGrid(
          children: [
            StatTile(
              label: 'Grupos',
              value: '${groups.value?.length ?? '—'}',
              caption: 'Asignados',
              icon: Icons.groups_rounded,
              tone: AppTone.blue,
              onTap: () => context.go('/teacher/groups'),
            ),
            StatTile(
              label: 'Por revisar',
              value: '${pendingReviews?.length ?? '—'}',
              caption: 'Entregas nuevas',
              icon: Icons.fact_check_rounded,
              tone: AppTone.accent,
              onTap: () => context.go('/teacher/reviews'),
            ),
            StatTile(
              label: 'Asistencia',
              value: '${pendingAttendance ?? '—'}',
              caption: 'Clases sin registrar',
              icon: Icons.how_to_reg_rounded,
              tone: AppTone.brand,
            ),
          ],
        ),
        const SectionHeader(title: 'Hoy en mis clases'),
        AsyncValueView(
          value: sessions,
          onRetry: () => ref.invalidate(todaySessionsProvider),
          loading: const SkeletonBox(height: 200),
          data: (list) => Column(
            children: [
              for (final s in list) ...[
                _SessionCard(session: s),
                const SizedBox(height: Spacing.sm),
              ],
            ],
          ),
        ),
        SectionHeader(
          title: 'Entregas por revisar',
          actionLabel: 'Ver todas',
          onAction: () => context.go('/teacher/reviews'),
        ),
        AsyncValueView(
          value: reviews,
          onRetry: () => ref.invalidate(reviewQueueProvider),
          loading: const SkeletonBox(),
          data: (list) {
            final pending = list.where((r) => !r.reviewed).take(3).toList();
            if (pending.isEmpty) {
              return const AppCard(child: Text('No hay entregas pendientes. ¡Buen trabajo!'));
            }
            return Column(
              children: [
                for (final r in pending) ...[
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

class _SessionCard extends ConsumerWidget {
  const _SessionCard({required this.session});

  final ClassSession session;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final group = session.group;
    return AppCard(
      onTap: () => context.push('/groups/${group.id}'),
      semanticLabel:
          '${format.time(session.start)}, ${group.subject.name}, ${group.placement}, ${group.room}. ${session.attendanceTaken ? 'Asistencia registrada' : 'Asistencia pendiente'}',
      child: ExcludeSemantics(
        child: Row(
          children: [
            SizedBox(
              width: 64,
              child: Column(
                children: [
                  Text(
                    format.time(session.start).split(' ').first,
                    style: theme.textTheme.titleMedium,
                  ),
                  Text(
                    format.time(session.start).split(' ').skip(1).join(' '),
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                  ),
                ],
              ),
            ),
            Container(width: 1, height: 52, color: colors.border),
            const SizedBox(width: Spacing.md),
            ToneIcon(icon: group.subject.area.icon, tone: group.subject.area.tone, size: 40),
            const SizedBox(width: Spacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${group.subject.name} · ${group.placement}',
                    style: theme.textTheme.titleSmall,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(
                    '${group.room} · ${session.topic}',
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 6),
                  StatusPill(
                    label: session.attendanceTaken ? 'Asistencia registrada' : 'Tomar asistencia',
                    tone: session.attendanceTaken ? AppTone.success : AppTone.accent,
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
