import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/responsive_grid.dart';
import '../../../domain/models/people.dart';
import '../../../shared/page_layout.dart';
import '../application/administration_providers.dart';

class StudentProfilePage extends ConsumerWidget {
  const StudentProfilePage({required this.studentId, super.key});

  final String studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profile = ref.watch(studentProfileProvider(studentId));
    final format = ref.watch(formattersProvider);
    final terms = ref.watch(institutionProvider).terms;
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return DetailPage(
      title: 'Expediente',
      children: [
        AsyncValueView(
          value: profile,
          onRetry: () => ref.invalidate(studentProfileProvider(studentId)),
          data: (p) => Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  InitialsAvatar(initials: p.summary.initials, size: 64),
                  const SizedBox(width: Spacing.lg),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(p.summary.fullName, style: theme.textTheme.headlineSmall),
                        Text(
                          '${p.summary.placement} · ${p.summary.code}',
                          style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
                        ),
                        const SizedBox(height: 6),
                        Wrap(
                          spacing: Spacing.sm,
                          runSpacing: 4,
                          children: [
                            StatusPill(label: p.summary.status.label, tone: AppTone.success),
                            StatusPill(
                              label: p.hasPortalAccount ? 'Con cuenta propia' : 'Sin cuenta',
                              tone: p.hasPortalAccount ? AppTone.blue : AppTone.neutral,
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SectionHeader(title: 'Asistencia del ciclo'),
              ResponsiveGrid(
                minItemWidth: 120,
                children: [
                  StatTile(
                    label: 'Asistencia',
                    value: '${format.decimal(p.attendance.rate)} %',
                    icon: Icons.percent_rounded,
                    tone: AppTone.brand,
                  ),
                  StatTile(
                    label: 'Ausencias',
                    value: '${p.attendance.absent}',
                    icon: Icons.event_busy_rounded,
                    tone: AppTone.danger,
                  ),
                  StatTile(
                    label: 'Llegadas tarde',
                    value: '${p.attendance.late}',
                    icon: Icons.schedule_rounded,
                    tone: AppTone.accent,
                  ),
                  StatTile(
                    label: 'Justificadas',
                    value: '${p.attendance.excused}',
                    icon: Icons.verified_rounded,
                    tone: AppTone.blue,
                  ),
                ],
              ),
              SectionHeader(title: terms.guardianPlural),
              if (p.guardians.isEmpty)
                const AppCard(
                  child: Text('Sin encargados vinculados. Requiere seguimiento de secretaría.'),
                )
              else
                for (final g in p.guardians) ...[
                  AppCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            InitialsAvatar(
                              initials: initialsOf(g.name),
                              size: 40,
                              tone: AppTone.neutral,
                            ),
                            const SizedBox(width: Spacing.md),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(g.name, style: theme.textTheme.titleSmall),
                                  Text(
                                    g.relationship,
                                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                                  ),
                                ],
                              ),
                            ),
                            StatusPill(
                              label: g.hasAccount ? 'Con cuenta' : 'Sin cuenta',
                              tone: g.hasAccount ? AppTone.success : AppTone.neutral,
                            ),
                          ],
                        ),
                        const SizedBox(height: Spacing.md),
                        Text('Facultades', style: theme.textTheme.labelLarge),
                        const SizedBox(height: Spacing.xs),
                        Wrap(
                          spacing: Spacing.sm,
                          runSpacing: 4,
                          children: [
                            _Faculty(label: 'Notas', granted: g.canViewGrades),
                            _Faculty(label: 'Finanzas', granted: g.canViewFinance),
                            _Faculty(label: 'Recoger', granted: g.canPickUp),
                          ],
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: Spacing.sm),
                ],
              const SectionHeader(title: 'Historial de matrícula'),
              AppCard(
                padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
                child: Column(
                  children: [
                    for (final h in p.history)
                      ListTile(
                        leading: const Icon(Icons.history_edu_rounded),
                        title: Text('${h.year} · ${h.placement}'),
                        trailing: StatusPill(
                          label: h.result,
                          tone: h.result == 'En curso' ? AppTone.blue : AppTone.success,
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: Spacing.md),
              Text(
                'Los vínculos familiares se aprueban explícitamente; nunca se infieren por apellido.',
                style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _Faculty extends StatelessWidget {
  const _Faculty({required this.label, required this.granted});

  final String label;
  final bool granted;

  @override
  Widget build(BuildContext context) {
    return StatusPill(
      label: label,
      tone: granted ? AppTone.success : AppTone.neutral,
      icon: granted ? Icons.check_rounded : Icons.close_rounded,
    );
  }
}
