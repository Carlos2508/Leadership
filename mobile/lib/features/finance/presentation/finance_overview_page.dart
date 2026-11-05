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
import '../../../core/widgets/states.dart';
import '../../../shared/page_layout.dart';
import '../../auth/application/auth_controller.dart';
import '../application/finance_providers.dart';
import 'finance_widgets.dart';

class FinanceOverviewPage extends ConsumerWidget {
  const FinanceOverviewPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(requireSessionProvider);
    final overview = ref.watch(financeOverviewProvider);
    final payments = ref.watch(paymentsProvider);
    final format = ref.watch(formattersProvider);
    final terms = ref.watch(institutionProvider).terms;
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return TabPage(
      subtitle: 'Control financiero',
      onRefresh: () => safeRefresh([
        ref.refresh(financeOverviewProvider.future),
        ref.refresh(paymentsProvider.future),
      ]),
      children: [
        PageIntro(
          eyebrow: 'Hola, ${session.firstName}',
          title: 'Pagos y ${terms.tuition.toLowerCase()}s',
          subtitle: 'Operaciones transparentes para familias y colegio.',
        ),
        AsyncValueView(
          value: overview,
          onRetry: () => ref.invalidate(financeOverviewProvider),
          loading: const SkeletonBox(height: 320),
          data: (data) {
            final delta = data.collectedLastMonth == 0
                ? null
                : (data.collectedThisMonth - data.collectedLastMonth) /
                      data.collectedLastMonth *
                      100;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ResponsiveGrid(
                  children: [
                    StatTile(
                      label: 'Recaudado este mes',
                      value: format.currency(data.collectedThisMonth),
                      caption: delta == null
                          ? null
                          : '${delta >= 0 ? '+' : ''}${format.decimal(delta)} % vs. mes anterior',
                      icon: Icons.trending_up_rounded,
                      tone: AppTone.brand,
                    ),
                    StatTile(
                      label: 'Por cobrar',
                      value: format.currency(data.pendingTotal),
                      caption: 'Vencido: ${format.currency(data.overdueTotal)}',
                      icon: Icons.schedule_rounded,
                      tone: AppTone.accent,
                    ),
                    StatTile(
                      label: 'Solvencia',
                      value: '${format.decimal(data.clearanceRate)} %',
                      caption: 'Para la publicación vigente',
                      icon: Icons.verified_rounded,
                      tone: AppTone.blue,
                    ),
                    StatTile(
                      label: 'Comprobantes',
                      value: '${data.evidencesInReview}',
                      caption: 'Por revisar',
                      icon: Icons.receipt_long_rounded,
                      tone: AppTone.coral,
                      onTap: () => context.go('/finance/evidences'),
                    ),
                  ],
                ),
                const SectionHeader(title: 'Recaudación por mes'),
                AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Pagos confirmados de los últimos seis meses',
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                      const SizedBox(height: Spacing.lg),
                      BarChart(
                        values: [for (final m in data.monthlyCollections) m.amount],
                        labels: [for (final m in data.monthlyCollections) m.label],
                        valueLabel: (v) =>
                            v >= 1000 ? '${format.decimal(v / 1000)}k' : format.decimal(v),
                        semanticsLabel: 'Recaudación mensual',
                      ),
                    ],
                  ),
                ),
              ],
            );
          },
        ),
        SectionHeader(
          title: 'Últimos movimientos',
          actionLabel: 'Ver todos',
          onAction: () => context.go('/finance/payments'),
        ),
        AsyncValueView(
          value: payments,
          onRetry: () => ref.invalidate(paymentsProvider),
          loading: const SkeletonBox(),
          data: (list) => AppCard(
            padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
            child: Column(
              children: [
                for (final p in list.take(5))
                  PaymentTile(
                    payment: p,
                    showStudent: true,
                    onTap: () => context.push('/receipts/${p.id}'),
                  ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
