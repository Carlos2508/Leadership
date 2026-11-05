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
import '../../../domain/models/finance.dart';
import '../../../shared/page_layout.dart';
import '../../academics/application/academic_providers.dart';
import '../../guardian/presentation/child_selector.dart';
import '../application/finance_providers.dart';
import 'finance_widgets.dart';

class GuardianPaymentsPage extends ConsumerWidget {
  const GuardianPaymentsPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final childId = ref.watch(selectedChildProvider);
    final terms = ref.watch(institutionProvider).terms;

    return TabPage(
      subtitle: 'Estado de cuenta',
      onRefresh: childId == null
          ? null
          : () => safeRefresh([ref.refresh(statementProvider(childId).future)]),
      children: [
        PageIntro(
          eyebrow: '${terms.tuition} y pagos',
          title: 'Cuentas claras',
          subtitle: 'Consulta cargos, pagos y comprobantes de cada estudiante.',
        ),
        const ChildSelector(),
        if (childId == null)
          const EmptyState(icon: Icons.link_off_rounded, title: 'Sin estudiantes vinculados')
        else
          _Statement(studentId: childId),
      ],
    );
  }
}

class _Statement extends ConsumerWidget {
  const _Statement({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final statement = ref.watch(statementProvider(studentId));
    final format = ref.watch(formattersProvider);
    return AsyncValueView(
      value: statement,
      onRetry: () => ref.invalidate(statementProvider(studentId)),
      data: (data) {
        final hasEvidenceInReview = data.evidences.any((e) => e.status == EvidenceStatus.inReview);
        final open = data.openCharges;
        final paid = data.charges.where((c) => !c.status.isOpen).toList().reversed.toList();
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            ClearanceBanner(
              clearance: data.clearance,
              action: data.clearance.state == ClearanceState.notCleared && !hasEvidenceInReview
                  ? FilledButton.icon(
                      onPressed: () => context.push('/evidence/$studentId'),
                      icon: const Icon(Icons.upload_file_rounded),
                      label: const Text('Registrar comprobante'),
                    )
                  : null,
            ),
            const SizedBox(height: Spacing.md),
            ResponsiveGrid(
              children: [
                StatTile(
                  label: 'Por pagar',
                  value: format.currency(data.totalPending),
                  icon: Icons.account_balance_wallet_rounded,
                  tone: data.totalPending > 0 ? AppTone.accent : AppTone.success,
                ),
                StatTile(
                  label: 'Saldo a favor',
                  value: format.currency(data.creditBalance),
                  icon: Icons.savings_rounded,
                  tone: AppTone.blue,
                ),
              ],
            ),
            if (data.evidences.isNotEmpty) ...[
              const SectionHeader(title: 'Comprobantes enviados'),
              AppCard(
                padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
                child: Column(
                  children: [for (final e in data.evidences) EvidenceStatusTile(evidence: e)],
                ),
              ),
            ],
            SectionHeader(
              title: 'Pendientes',
              actionLabel: open.isEmpty || hasEvidenceInReview ? null : 'Enviar comprobante',
              onAction: () => context.push('/evidence/$studentId'),
            ),
            AppCard(
              padding: const EdgeInsets.symmetric(vertical: Spacing.xs),
              child: open.isEmpty
                  ? const Padding(
                      padding: EdgeInsets.all(Spacing.lg),
                      child: Text('No hay cargos pendientes.'),
                    )
                  : Column(
                      children: [
                        for (var i = 0; i < open.length; i++) ...[
                          if (i > 0) const Divider(indent: Spacing.lg, endIndent: Spacing.lg),
                          ChargeTile(charge: open[i]),
                        ],
                      ],
                    ),
            ),
            const SectionHeader(title: 'Pagados'),
            AppCard(
              padding: const EdgeInsets.symmetric(vertical: Spacing.xs),
              child: Column(
                children: [
                  for (var i = 0; i < paid.length; i++) ...[
                    if (i > 0) const Divider(indent: Spacing.lg, endIndent: Spacing.lg),
                    ChargeTile(charge: paid[i]),
                  ],
                ],
              ),
            ),
            const SectionHeader(title: 'Recibos'),
            AppCard(
              padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
              child: Column(
                children: [
                  for (final p in data.payments)
                    PaymentTile(payment: p, onTap: () => context.push('/receipts/${p.id}')),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}
