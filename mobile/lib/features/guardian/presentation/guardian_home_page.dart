import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/format/formatters.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/responsive_grid.dart';
import '../../../core/widgets/states.dart';
import '../../../domain/models/finance.dart';
import '../../../shared/page_layout.dart';
import '../../academics/application/academic_providers.dart';
import '../../academics/presentation/academic_widgets.dart';
import '../../auth/application/auth_controller.dart';
import '../../finance/application/finance_providers.dart';
import 'assignment_preview_sheet.dart';
import 'child_selector.dart';

class GuardianHomePage extends ConsumerWidget {
  const GuardianHomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(requireSessionProvider);
    final childId = ref.watch(selectedChildProvider);
    final terms = ref.watch(institutionProvider).terms;

    return TabPage(
      subtitle: 'Portal de ${terms.guardianPlural.toLowerCase()}',
      onRefresh: childId == null
          ? null
          : () => safeRefresh([
              ref.refresh(homeSummaryProvider(childId).future),
              ref.refresh(statementProvider(childId).future),
              ref.refresh(assignmentsProvider(childId).future),
              ref.refresh(eventsProvider.future),
            ]),
      children: [
        PageIntro(
          eyebrow: 'Hola, ${session.firstName}',
          title: 'Acompaña su aprendizaje',
          subtitle: 'Todos sus avances en un mismo lugar.',
        ),
        if (childId == null)
          const EmptyState(
            icon: Icons.link_off_rounded,
            title: 'Sin estudiantes vinculados',
            message: 'Secretaría debe aprobar el vínculo familiar antes de mostrar información.',
          )
        else ...[
          const ChildSelector(),
          _ChildSummary(studentId: childId),
          const SectionHeader(title: 'Accesos rápidos'),
          ResponsiveGrid(
            children: [
              _QuickAccess(
                icon: Icons.insights_rounded,
                label: 'Calificaciones',
                tone: AppTone.blue,
                onTap: () => context.go('/family/grades'),
              ),
              _QuickAccess(
                icon: Icons.account_balance_wallet_rounded,
                label: 'Estado de cuenta',
                tone: AppTone.accent,
                onTap: () => context.go('/family/payments'),
              ),
              _QuickAccess(
                icon: Icons.event_note_rounded,
                label: 'Calendario',
                tone: AppTone.purple,
                onTap: () => context.go('/family/agenda'),
              ),
              _QuickAccess(
                icon: Icons.notifications_rounded,
                label: 'Avisos',
                tone: AppTone.coral,
                onTap: () => context.go('/family/notices'),
              ),
            ],
          ),
          const SectionHeader(title: 'Tareas pendientes'),
          _PendingAssignments(studentId: childId),
          const SectionHeader(title: 'Próximos compromisos'),
          _UpcomingEvents(),
        ],
      ],
    );
  }
}

class _ChildSummary extends ConsumerWidget {
  const _ChildSummary({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summary = ref.watch(homeSummaryProvider(studentId));
    final statement = ref.watch(statementProvider(studentId));
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return AsyncValueView(
      value: summary,
      onRetry: () => ref.invalidate(homeSummaryProvider(studentId)),
      loading: const SkeletonBox(height: 220),
      data: (data) {
        final clearance = statement.value?.clearance;
        return AppCard(
          tone: AppTone.brand,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  InitialsAvatar(initials: data.student.initials, size: 52),
                  const SizedBox(width: Spacing.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(data.student.fullName, style: theme.textTheme.titleMedium),
                        Text(
                          '${data.student.placement} · ${data.student.code}',
                          style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                        ),
                        const SizedBox(height: 6),
                        StatusPill(
                          label: 'Estudiante ${data.student.status.label.toLowerCase()}',
                          tone: AppTone.success,
                          icon: Icons.circle,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: Spacing.lg),
              Row(
                children: [
                  Expanded(
                    child: _MiniStat(
                      label: 'Asistencia',
                      value: '${format.decimal(data.attendance.rate)} %',
                    ),
                  ),
                  const SizedBox(width: Spacing.md),
                  Expanded(
                    child: _MiniStat(
                      label: 'Tareas pendientes',
                      value: '${data.pendingAssignments}',
                    ),
                  ),
                ],
              ),
              const SizedBox(height: Spacing.md),
              if (clearance != null)
                Row(
                  children: [
                    Icon(
                      clearance.state == ClearanceState.cleared
                          ? Icons.verified_rounded
                          : Icons.info_outline_rounded,
                      size: 18,
                      color: clearance.state == ClearanceState.cleared
                          ? colors.tone(AppTone.success).foreground
                          : colors.tone(AppTone.warning).foreground,
                    ),
                    const SizedBox(width: Spacing.sm),
                    Expanded(
                      child: Text(switch (clearance.state) {
                        ClearanceState.cleared => 'Pagos al día para la publicación vigente',
                        ClearanceState.notCleared =>
                          'Pendiente: ${clearance.pendingPeriods.join(', ')}',
                        ClearanceState.pendingSetup => 'Plan financiero en configuración',
                      }, style: theme.textTheme.bodySmall?.copyWith(fontWeight: FontWeight.w600)),
                    ),
                  ],
                ),
              const SizedBox(height: Spacing.md),
              FilledButton.icon(
                onPressed: () => context.go('/family/grades'),
                icon: const Icon(Icons.arrow_forward_rounded),
                iconAlignment: IconAlignment.end,
                label: Text('Consultar avances de ${data.student.firstName}'),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _MiniStat extends StatelessWidget {
  const _MiniStat({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Container(
      padding: const EdgeInsets.all(Spacing.md),
      decoration: BoxDecoration(
        color: colors.cardBackground,
        borderRadius: BorderRadius.circular(Radii.md),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
          const SizedBox(height: 4),
          Text(value, style: theme.textTheme.headlineSmall),
        ],
      ),
    );
  }
}

class _QuickAccess extends StatelessWidget {
  const _QuickAccess({
    required this.icon,
    required this.label,
    required this.tone,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final AppTone tone;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      onTap: onTap,
      padding: const EdgeInsets.all(Spacing.md),
      semanticLabel: label,
      child: ExcludeSemantics(
        child: Row(
          children: [
            ToneIcon(icon: icon, tone: tone, size: 40),
            const SizedBox(width: Spacing.md),
            Expanded(
              child: Text(label, style: Theme.of(context).textTheme.titleSmall, maxLines: 2),
            ),
          ],
        ),
      ),
    );
  }
}

class _PendingAssignments extends ConsumerWidget {
  const _PendingAssignments({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final assignments = ref.watch(assignmentsProvider(studentId));
    return AsyncValueView(
      value: assignments,
      onRetry: () => ref.invalidate(assignmentsProvider(studentId)),
      loading: const SkeletonBox(),
      data: (list) {
        final pending = list.where((a) => a.status.isOpen).toList();
        if (pending.isEmpty) {
          return const AppCard(child: Text('No hay tareas pendientes. ¡Excelente!'));
        }
        return Column(
          children: [
            for (final a in pending) ...[
              AssignmentTile(assignment: a, onTap: () => showAssignmentPreview(context, a)),
              const SizedBox(height: Spacing.sm),
            ],
          ],
        );
      },
    );
  }
}

class _UpcomingEvents extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final events = ref.watch(eventsProvider);
    final today = dateOnly(ref.watch(clockProvider)());
    return AsyncValueView(
      value: events,
      onRetry: () => ref.invalidate(eventsProvider),
      loading: const SkeletonBox(),
      data: (list) => Column(
        children: [
          for (final e in list.where((e) => !e.start.isBefore(today)).take(3)) ...[
            EventTile(event: e),
            const SizedBox(height: Spacing.sm),
          ],
        ],
      ),
    );
  }
}
