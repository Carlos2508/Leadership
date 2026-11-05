import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/terms.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/grades.dart';
import '../../../shared/visuals.dart';

/// Presenta la respuesta del servidor sobre calificaciones. Si están
/// bloqueadas por solvencia, la respuesta no contiene notas que mostrar.
class GradeReportView extends StatelessWidget {
  const GradeReportView({required this.report, required this.studentId, super.key});

  final GradeReport report;
  final String studentId;

  @override
  Widget build(BuildContext context) {
    return switch (report) {
      final GradesAvailable available => _AvailableGrades(report: available),
      final GradesBlocked blocked => _BlockedGrades(report: blocked, studentId: studentId),
      GradesPendingSetup() => const _InfoCard(
        icon: Icons.pending_actions_rounded,
        tone: AppTone.blue,
        title: 'Plan financiero en configuración',
        message: 'Las calificaciones se habilitarán cuando administración complete el plan de pagos de esta matrícula.',
      ),
      GradesNotPublished() => const _InfoCard(
        icon: Icons.hourglass_empty_rounded,
        tone: AppTone.neutral,
        title: 'Aún no hay notas publicadas',
        message: 'Te avisaremos cuando coordinación académica publique el período.',
      ),
    };
  }
}

class _AvailableGrades extends ConsumerStatefulWidget {
  const _AvailableGrades({required this.report});

  final GradesAvailable report;

  @override
  ConsumerState<_AvailableGrades> createState() => _AvailableGradesState();
}

class _AvailableGradesState extends ConsumerState<_AvailableGrades> {
  late int _period = widget.report.publishedPeriods - 1;

  @override
  Widget build(BuildContext context) {
    final report = widget.report;
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final terms = ref.watch(institutionProvider).terms;
    final format = ref.watch(formattersProvider);
    final delta = report.overallPartial != null && report.previousOverall != null
        ? report.overallPartial! - report.previousOverall!
        : null;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        HeroBanner(
          tag: 'Tu esfuerzo se nota',
          title: report.overallPartial == null
              ? 'Sin promedio'
              : '${format.decimal(report.overallPartial!)} / 100',
          message: [
            'Promedio parcial · ${report.cycleLabel}',
            if (delta != null)
              '${delta >= 0 ? '+' : ''}${format.decimal(delta)} respecto al ${terms.period.toLowerCase()} anterior',
          ].join('\n'),
          icon: Icons.emoji_events_rounded,
        ),
        const SizedBox(height: Spacing.lg),
        Semantics(
          label: 'Elegir ${terms.period.toLowerCase()}',
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                for (var i = 0; i < 4; i++)
                  Padding(
                    padding: const EdgeInsets.only(right: Spacing.sm),
                    child: ChoiceChip(
                      label: Text('${i + 1}.º ${terms.period.toLowerCase()}'),
                      selected: _period == i,
                      onSelected: i < report.publishedPeriods
                          ? (_) => setState(() => _period = i)
                          : null,
                    ),
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(height: Spacing.md),
        AppCard(
          padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(Spacing.lg, Spacing.sm, Spacing.lg, Spacing.xs),
                child: Wrap(
                  alignment: WrapAlignment.spaceBetween,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  spacing: Spacing.md,
                  runSpacing: Spacing.xs,
                  children: [
                    Text('Mis materias', style: theme.textTheme.titleMedium),
                    Text(
                      'Publicado ${format.shortDate(report.publishedAt)}',
                      style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    ),
                  ],
                ),
              ),
              for (final grade in report.subjects)
                _SubjectGradeTile(grade: grade, period: _period, periodTerm: terms.period),
            ],
          ),
        ),
        const SizedBox(height: Spacing.md),
        Text(
          'Las notas pendientes se muestran como “—”; no equivalen a cero.',
          style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
        ),
      ],
    );
  }
}

class _SubjectGradeTile extends ConsumerWidget {
  const _SubjectGradeTile({required this.grade, required this.period, required this.periodTerm});

  final SubjectGrade grade;
  final int period;
  final String periodTerm;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final format = ref.watch(formattersProvider);
    final score = grade.periods[period];
    final scoreText = score == null ? '—' : format.decimal(score);
    return Semantics(
      button: true,
      label: '${grade.subject.name}: ${score == null ? 'pendiente' : '$scoreText de 100'}',
      excludeSemantics: true,
      child: InkWell(
        onTap: () => _showDetail(context, format.decimal),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: Spacing.lg, vertical: Spacing.md),
          child: Row(
            children: [
              ToneIcon(icon: grade.subject.area.icon, tone: grade.subject.area.tone, size: 42),
              const SizedBox(width: Spacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      grade.subject.name,
                      style: theme.textTheme.titleSmall,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    Text(
                      '${period + 1}.º ${periodTerm.toLowerCase()}',
                      style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    ),
                  ],
                ),
              ),
              Text(scoreText, style: theme.textTheme.titleLarge),
              Text(' /100', style: theme.textTheme.bodySmall?.copyWith(color: colors.muted)),
            ],
          ),
        ),
      ),
    );
  }

  void _showDetail(BuildContext context, String Function(num) decimal) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (context) {
        final theme = Theme.of(context);
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(Spacing.xl, 0, Spacing.xl, Spacing.xl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    ToneIcon(icon: grade.subject.area.icon, tone: grade.subject.area.tone),
                    const SizedBox(width: Spacing.md),
                    Expanded(child: Text(grade.subject.name, style: theme.textTheme.titleLarge)),
                  ],
                ),
                const SizedBox(height: Spacing.sm),
                Text(grade.subject.teacherName, style: theme.textTheme.bodyMedium),
                const SizedBox(height: Spacing.lg),
                for (var i = 0; i < grade.periods.length; i++)
                  InfoRow(
                    label: '${i + 1}.º ${periodTerm.toLowerCase()}',
                    value: grade.periods[i] == null
                        ? 'Pendiente'
                        : '${decimal(grade.periods[i]!)} / 100',
                  ),
                const Divider(height: Spacing.xl),
                InfoRow(
                  label: 'Promedio parcial',
                  value: grade.partialAverage == null ? '—' : decimal(grade.partialAverage!),
                ),
                const SizedBox(height: Spacing.sm),
                StatusPill(label: grade.status.label, tone: grade.status.tone),
                const SizedBox(height: Spacing.md),
                Text(
                  'El resultado final se calcula al publicar los cuatro períodos.',
                  style: theme.textTheme.bodySmall?.copyWith(color: AppColors.of(context).muted),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _BlockedGrades extends ConsumerWidget {
  const _BlockedGrades({required this.report, required this.studentId});

  final GradesBlocked report;
  final String studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final terms = ref.watch(institutionProvider).terms;
    final format = ref.watch(formattersProvider);
    final tuitionPlural = pluralizeEs(terms.tuition).toLowerCase();
    final warning = colors.tone(AppTone.accent);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const ToneIcon(icon: Icons.lock_clock_rounded, tone: AppTone.accent),
                  const SizedBox(width: Spacing.md),
                  Expanded(
                    child: Text(
                      'Las calificaciones del ${report.publicationLabel} están publicadas',
                      style: theme.textTheme.titleMedium,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: Spacing.md),
              Text(
                'Para consultarlas se necesitan las $tuitionPlural requeridas para esta entrega de notas.',
                style: theme.textTheme.bodyMedium,
              ),
              if (report.canSeeFinance && report.pendingPeriods.isNotEmpty) ...[
                const SizedBox(height: Spacing.lg),
                Text('Pendiente', style: theme.textTheme.labelLarge),
                const SizedBox(height: Spacing.sm),
                Wrap(
                  spacing: Spacing.sm,
                  runSpacing: Spacing.sm,
                  children: [
                    for (final period in report.pendingPeriods)
                      StatusPill(label: period, tone: AppTone.danger),
                  ],
                ),
                if (report.pendingAmount != null) ...[
                  const SizedBox(height: Spacing.sm),
                  Text(
                    'Saldo requerido: ${format.currency(report.pendingAmount!)}',
                    style: theme.textTheme.titleSmall,
                  ),
                ],
              ],
              if (report.evidenceInReview) ...[
                const SizedBox(height: Spacing.lg),
                Container(
                  padding: const EdgeInsets.all(Spacing.md),
                  decoration: BoxDecoration(
                    color: warning.background,
                    borderRadius: BorderRadius.circular(Radii.sm),
                  ),
                  child: Row(
                    children: [
                      Icon(Icons.hourglass_top_rounded, color: warning.foreground, size: 20),
                      const SizedBox(width: Spacing.sm),
                      Expanded(
                        child: Text(
                          'Hay un comprobante en revisión. Las notas se habilitan cuando caja lo valida.',
                          style: theme.textTheme.bodySmall?.copyWith(
                            color: warning.foreground,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
              const SizedBox(height: Spacing.lg),
              if (report.canSeeFinance)
                Wrap(
                  spacing: Spacing.sm,
                  runSpacing: Spacing.sm,
                  children: [
                    FilledButton.icon(
                      onPressed: () => context.go('/family/payments'),
                      icon: const Icon(Icons.account_balance_wallet_rounded),
                      label: const Text('Ver estado de cuenta'),
                    ),
                    if (!report.evidenceInReview)
                      OutlinedButton.icon(
                        onPressed: () => context.push('/evidence/$studentId'),
                        icon: const Icon(Icons.upload_file_rounded),
                        label: const Text('Registrar comprobante'),
                      ),
                  ],
                )
              else
                Text(
                  'Consulta con tu encargado o con caja.',
                  style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
                ),
            ],
          ),
        ),
        const SizedBox(height: Spacing.md),
        Row(
          children: [
            Icon(Icons.check_circle_outline_rounded, size: 18, color: colors.muted),
            const SizedBox(width: Spacing.sm),
            Expanded(
              child: Text(
                'El acceso a tareas y entregas continúa disponible.',
                style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({
    required this.icon,
    required this.tone,
    required this.title,
    required this.message,
  });

  final IconData icon;
  final AppTone tone;
  final String title;
  final String message;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return AppCard(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ToneIcon(icon: icon, tone: tone),
          const SizedBox(width: Spacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: theme.textTheme.titleMedium),
                const SizedBox(height: Spacing.xs),
                Text(message, style: theme.textTheme.bodyMedium),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
