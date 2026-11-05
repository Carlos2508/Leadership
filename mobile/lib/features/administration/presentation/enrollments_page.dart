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
import '../../../domain/models/administration.dart';
import '../../../shared/page_layout.dart';
import '../../../shared/visuals.dart';
import '../application/administration_providers.dart';

class EnrollmentsPage extends ConsumerStatefulWidget {
  const EnrollmentsPage({super.key});

  @override
  ConsumerState<EnrollmentsPage> createState() => _EnrollmentsPageState();
}

class _EnrollmentsPageState extends ConsumerState<EnrollmentsPage> {
  EnrollmentStage? _stage;

  @override
  Widget build(BuildContext context) {
    final cases = ref.watch(enrollmentsProvider);
    return TabPage(
      subtitle: 'Secretaría',
      onRefresh: () => safeRefresh([ref.refresh(enrollmentsProvider.future)]),
      children: [
        const PageIntro(
          eyebrow: 'Admisiones',
          title: 'Matrícula',
          subtitle: 'Solicitud → documentos → plan financiero → validación → activa.',
        ),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              for (final stage in [null, ...EnrollmentStage.values])
                Padding(
                  padding: const EdgeInsets.only(right: Spacing.sm),
                  child: ChoiceChip(
                    label: Text(stage?.label ?? 'Todas'),
                    selected: _stage == stage,
                    onSelected: (_) => setState(() => _stage = stage),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: Spacing.lg),
        AsyncValueView(
          value: cases,
          onRetry: () => ref.invalidate(enrollmentsProvider),
          data: (list) {
            final filtered = list.where((c) => _stage == null || c.stage == _stage).toList();
            if (filtered.isEmpty) {
              return const EmptyState(
                icon: Icons.inbox_rounded,
                title: 'No hay expedientes en esta etapa',
              );
            }
            return Column(
              children: [
                for (final c in filtered) ...[
                  _CaseCard(enrollment: c),
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

class _CaseCard extends ConsumerWidget {
  const _CaseCard({required this.enrollment});

  final EnrollmentCase enrollment;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final format = ref.watch(formattersProvider);
    final stageIndex = enrollment.stage.index;
    final total = EnrollmentStage.values.length - 1;
    return AppCard(
      onTap: () => context.push('/enrollments/${enrollment.id}'),
      semanticLabel:
          '${enrollment.studentName}, ${enrollment.gradeLabel}, etapa ${enrollment.stage.label}'
          '${enrollment.issues.isEmpty ? '' : ', ${enrollment.issues.length} incidencias'}',
      child: ExcludeSemantics(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(enrollment.studentName, style: theme.textTheme.titleSmall),
                      Text(
                        '${enrollment.gradeLabel} · Solicitud ${format.shortDate(enrollment.submittedAt)}',
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                    ],
                  ),
                ),
                StatusPill(label: enrollment.stage.label, tone: enrollment.stage.tone),
              ],
            ),
            const SizedBox(height: Spacing.md),
            ProgressBar(value: stageIndex / total, height: 6),
            if (enrollment.issues.isNotEmpty) ...[
              const SizedBox(height: Spacing.sm),
              Row(
                children: [
                  Icon(
                    Icons.warning_amber_rounded,
                    size: 16,
                    color: colors.tone(AppTone.warning).foreground,
                  ),
                  const SizedBox(width: 4),
                  Expanded(
                    child: Text(
                      enrollment.issues.join(' · '),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colors.tone(AppTone.warning).foreground,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}
