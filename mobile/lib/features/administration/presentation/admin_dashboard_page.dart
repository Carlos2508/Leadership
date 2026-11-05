import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/charts.dart';
import '../../../core/widgets/responsive_grid.dart';
import '../../../shared/page_layout.dart';
import '../../auth/application/auth_controller.dart';
import '../application/administration_providers.dart';

class AdminDashboardPage extends ConsumerWidget {
  const AdminDashboardPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(requireSessionProvider);
    final kpis = ref.watch(kpisProvider);
    final format = ref.watch(formattersProvider);
    final terms = ref.watch(institutionProvider).terms;
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final now = ref.watch(clockProvider)();

    return TabPage(
      subtitle: session.roleTitle,
      onRefresh: () => safeRefresh([ref.refresh(kpisProvider.future)]),
      floatingActionButton: FloatingActionButton.extended(
        heroTag: null,
        onPressed: () => context.push('/announcements/new'),
        icon: const Icon(Icons.campaign_rounded),
        label: const Text('Comunicado'),
      ),
      children: [
        PageIntro(
          eyebrow: 'Panel de dirección · ${format.dayMonth(now)}',
          title: 'Resumen del colegio',
          subtitle: 'Indicadores para orientar decisiones, no solo datos.',
        ),
        AsyncValueView(
          value: kpis,
          onRetry: () => ref.invalidate(kpisProvider),
          data: (data) => Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              ResponsiveGrid(
                children: [
                  StatTile(
                    label: '${terms.studentPlural} activos',
                    value: '${data.enrolledStudents}',
                    caption: '+${data.newStudents} nuevas matrículas',
                    icon: Icons.school_rounded,
                    tone: AppTone.brand,
                    onTap: () => context.go('/admin/students'),
                  ),
                  StatTile(
                    label: 'Asistencia',
                    value: '${format.decimal(data.attendanceRate)} %',
                    caption: 'Promedio del ciclo',
                    icon: Icons.how_to_reg_rounded,
                    tone: AppTone.blue,
                  ),
                  StatTile(
                    label: 'Secciones',
                    value: '${data.sections}',
                    caption: '${data.teachers} docentes',
                    icon: Icons.meeting_room_rounded,
                    tone: AppTone.purple,
                  ),
                  StatTile(
                    label: 'Matrículas',
                    value: '${data.pendingEnrollments}',
                    caption: 'En proceso',
                    icon: Icons.assignment_ind_rounded,
                    tone: AppTone.accent,
                    onTap: () => context.go('/admin/enrollment'),
                  ),
                ],
              ),
              const SectionHeader(title: 'Asistencia mensual'),
              AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Porcentaje de presencia por mes',
                      style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    ),
                    const SizedBox(height: Spacing.lg),
                    BarChart(
                      values: [for (final m in data.monthlyAttendance) m.value],
                      labels: [for (final m in data.monthlyAttendance) m.label],
                      valueLabel: (v) => '${format.decimal(v)}%',
                      semanticsLabel: 'Asistencia mensual',
                      minValue: 80,
                    ),
                  ],
                ),
              ),
              const SectionHeader(title: 'Lo importante hoy'),
              AppCard(
                padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
                child: Column(
                  children: [
                    _TodoTile(
                      icon: Icons.assignment_late_rounded,
                      tone: AppTone.accent,
                      title: '${data.pendingEnrollments} matrículas en proceso',
                      subtitle: 'Revisa documentos y planes financieros',
                      onTap: () => context.go('/admin/enrollment'),
                    ),
                    _TodoTile(
                      icon: Icons.link_rounded,
                      tone: AppTone.purple,
                      title: '${data.pendingLinks} encargados sin cuenta',
                      subtitle: 'Genera invitaciones verificadas',
                      onTap: () => context.go('/admin/accounts'),
                    ),
                    _TodoTile(
                      icon: Icons.receipt_long_rounded,
                      tone: AppTone.coral,
                      title: '${data.evidencesInReview} comprobantes en caja',
                      subtitle: 'Pendientes de validación por finanzas',
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _TodoTile extends StatelessWidget {
  const _TodoTile({
    required this.icon,
    required this.tone,
    required this.title,
    required this.subtitle,
    this.onTap,
  });

  final IconData icon;
  final AppTone tone;
  final String title;
  final String subtitle;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      onTap: onTap,
      leading: ToneIcon(icon: icon, tone: tone, size: 40),
      title: Text(title),
      subtitle: Text(subtitle),
      trailing: onTap == null ? null : const Icon(Icons.chevron_right_rounded),
    );
  }
}
