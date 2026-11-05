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
import '../../../shared/page_layout.dart';
import '../application/finance_providers.dart';
import 'finance_widgets.dart';

/// Cuenta de un estudiante vista por caja.
class StudentAccountPage extends ConsumerWidget {
  const StudentAccountPage({required this.studentId, super.key});

  final String studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final statement = ref.watch(statementProvider(studentId));
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final hasOpen = statement.value?.openCharges.isNotEmpty ?? false;

    return DetailPage(
      title: 'Cuenta del estudiante',
      onRefresh: () => safeRefresh([ref.refresh(statementProvider(studentId).future)]),
      bottomBar: hasOpen
          ? FilledButton.icon(
              onPressed: () => context.push('/accounts/$studentId/pay'),
              icon: const Icon(Icons.point_of_sale_rounded),
              label: const Text('Registrar pago'),
            )
          : null,
      children: [
        AsyncValueView(
          value: statement,
          onRetry: () => ref.invalidate(statementProvider(studentId)),
          data: (data) {
            final open = data.openCharges;
            return Column(
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
                          Text(data.student.fullName, style: theme.textTheme.titleLarge),
                          Text(
                            '${data.student.code} · ${data.student.placement}',
                            style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: Spacing.lg),
                ClearanceBanner(clearance: data.clearance),
                const SizedBox(height: Spacing.md),
                ResponsiveGrid(
                  children: [
                    StatTile(
                      label: 'Saldo abierto',
                      value: format.currency(open.fold<double>(0, (s, c) => s + c.balance)),
                      icon: Icons.account_balance_wallet_rounded,
                      tone: AppTone.accent,
                    ),
                    StatTile(
                      label: 'Saldo a favor',
                      value: format.currency(data.creditBalance),
                      icon: Icons.savings_rounded,
                      tone: AppTone.blue,
                    ),
                  ],
                ),
                const SectionHeader(title: 'Cargos abiertos'),
                AppCard(
                  padding: const EdgeInsets.symmetric(vertical: Spacing.xs),
                  child: open.isEmpty
                      ? const Padding(
                          padding: EdgeInsets.all(Spacing.lg),
                          child: Text('Sin cargos pendientes.'),
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
                if (data.evidences.isNotEmpty) ...[
                  const SectionHeader(title: 'Comprobantes'),
                  AppCard(
                    padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
                    child: Column(
                      children: [for (final e in data.evidences) EvidenceStatusTile(evidence: e)],
                    ),
                  ),
                ],
                const SectionHeader(title: 'Historial de pagos'),
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
        ),
      ],
    );
  }
}
