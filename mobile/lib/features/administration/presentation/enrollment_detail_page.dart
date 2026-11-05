import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/administration.dart';
import '../../../shared/page_layout.dart';
import '../../../shared/visuals.dart';
import '../application/administration_providers.dart';

class EnrollmentDetailPage extends ConsumerStatefulWidget {
  const EnrollmentDetailPage({required this.caseId, super.key});

  final String caseId;

  @override
  ConsumerState<EnrollmentDetailPage> createState() => _EnrollmentDetailPageState();
}

class _EnrollmentDetailPageState extends ConsumerState<EnrollmentDetailPage> {
  bool _busy = false;

  Future<void> _run(Future<Object?> Function() action, String success) async {
    setState(() => _busy = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await action();
      messenger.showSnackBar(SnackBar(content: Text(success)));
    } on Object catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(friendlyError(error))));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final enrollment = ref.watch(enrollmentProvider(widget.caseId));
    final format = ref.watch(formattersProvider);
    final actions = ref.read(administrationActionsProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final data = enrollment.value;
    final next = data?.stage.next;

    return DetailPage(
      title: 'Expediente de matrícula',
      bottomBar: data == null || next == null
          ? null
          : FilledButton(
              onPressed: _busy || data.issues.isNotEmpty
                  ? null
                  : () => _run(() => actions.advance(data.id), 'Etapa actualizada: ${next.label}.'),
              child: _busy
                  ? const SizedBox.square(
                      dimension: 22,
                      child: CircularProgressIndicator(strokeWidth: 2.5),
                    )
                  : Text(
                      data.issues.isEmpty
                          ? 'Avanzar a «${next.label}»'
                          : 'Resuelve las incidencias para avanzar',
                    ),
            ),
      children: [
        AsyncValueView(
          value: enrollment,
          onRetry: () => ref.invalidate(enrollmentProvider(widget.caseId)),
          data: (c) => Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(c.studentName, style: theme.textTheme.headlineSmall),
              Text(
                '${c.gradeLabel} · Encargado: ${c.guardianName}',
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
              ),
              Text(
                'Solicitud del ${format.shortDate(c.submittedAt)}',
                style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
              ),
              const SectionHeader(title: 'Etapas'),
              AppCard(
                padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
                child: Column(
                  children: [
                    for (final stage in EnrollmentStage.values)
                      ListTile(
                        leading: Icon(
                          stage.index < c.stage.index
                              ? Icons.check_circle_rounded
                              : stage == c.stage
                              ? Icons.radio_button_checked_rounded
                              : Icons.radio_button_unchecked_rounded,
                          color: stage.index <= c.stage.index
                              ? colors.tone(stage.tone).foreground
                              : colors.muted,
                        ),
                        title: Text(stage.label),
                        subtitle: Text(
                          stage.index < c.stage.index
                              ? 'Completada'
                              : stage == c.stage
                              ? 'Etapa actual'
                              : 'Pendiente',
                        ),
                      ),
                  ],
                ),
              ),
              SectionHeader(title: c.issues.isEmpty ? 'Sin incidencias' : 'Incidencias'),
              if (c.issues.isEmpty)
                const AppCard(
                  tone: AppTone.success,
                  child: Text(
                    'El expediente puede avanzar. La activación académica y la cuenta del portal son pasos distintos.',
                  ),
                )
              else
                for (final issue in c.issues) ...[
                  AppCard(
                    tone: AppTone.warning,
                    padding: const EdgeInsets.all(Spacing.md),
                    child: Row(
                      children: [
                        const Icon(Icons.warning_amber_rounded),
                        const SizedBox(width: Spacing.md),
                        Expanded(child: Text(issue, style: theme.textTheme.bodyMedium)),
                        TextButton(
                          onPressed: _busy
                              ? null
                              : () => _run(
                                  () => ref
                                      .read(administrationActionsProvider)
                                      .resolveIssue(c.id, issue),
                                  'Incidencia resuelta.',
                                ),
                          child: const Text('Resolver'),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: Spacing.sm),
                ],
            ],
          ),
        ),
      ],
    );
  }
}
