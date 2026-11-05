import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/institution_logo.dart';
import '../../../shared/page_layout.dart';
import '../application/finance_providers.dart';
import 'finance_widgets.dart';

class ReceiptPage extends ConsumerWidget {
  const ReceiptPage({required this.paymentId, super.key});

  final String paymentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final payment = ref.watch(paymentProvider(paymentId));
    final institution = ref.watch(institutionProvider);
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return DetailPage(
      title: 'Recibo',
      actions: [
        IconButton(
          tooltip: 'Compartir recibo',
          onPressed: () => ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('En el prototipo no se generan archivos ni se envían mensajes.'),
            ),
          ),
          icon: const Icon(Icons.ios_share_rounded),
        ),
      ],
      children: [
        AsyncValueView(
          value: payment,
          onRetry: () => ref.invalidate(paymentProvider(paymentId)),
          data: (p) {
            final statement = ref.watch(statementProvider(p.studentId)).value;
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                AppCard(
                  padding: const EdgeInsets.all(Spacing.xl),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          InstitutionLogo(path: institution.mainLogo, size: 48),
                          const SizedBox(width: Spacing.md),
                          Expanded(
                            child: Text(
                              institution.displayName,
                              style: theme.textTheme.titleMedium,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: Spacing.xl),
                      const Center(
                        child: ToneIcon(icon: Icons.check_rounded, tone: AppTone.success, size: 56),
                      ),
                      const SizedBox(height: Spacing.md),
                      Center(child: Text('Pago confirmado', style: theme.textTheme.titleLarge)),
                      Center(
                        child: Text(
                          format.currency(p.amount),
                          style: theme.textTheme.headlineLarge?.copyWith(fontSize: 36),
                        ),
                      ),
                      const SizedBox(height: Spacing.sm),
                      Center(
                        child: StatusPill(
                          label: 'Recibo ${p.receiptNumber}',
                          tone: AppTone.neutral,
                        ),
                      ),
                      const Divider(height: Spacing.xxl),
                      InfoRow(label: 'Estudiante', value: p.studentName),
                      InfoRow(
                        label: 'Fecha',
                        value: '${format.shortDate(p.paidAt)} · ${format.time(p.paidAt)}',
                      ),
                      InfoRow(label: 'Medio', value: p.method.label),
                      InfoRow(label: 'Referencia', value: p.reference),
                      InfoRow(label: 'Registró', value: p.registeredBy),
                      const Divider(height: Spacing.xl),
                      Text('Aplicado a', style: theme.textTheme.labelLarge),
                      const SizedBox(height: Spacing.xs),
                      for (final a in p.allocations)
                        InfoRow(label: a.label, value: format.currency(a.amount)),
                      if (p.creditBalance > 0)
                        InfoRow(label: 'Saldo a favor', value: format.currency(p.creditBalance)),
                    ],
                  ),
                ),
                if (statement != null) ...[
                  const SizedBox(height: Spacing.lg),
                  ClearanceBanner(clearance: statement.clearance),
                ],
                const SizedBox(height: Spacing.md),
                Text(
                  'Comprobante interno de demostración. No constituye factura fiscal.',
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
              ],
            );
          },
        ),
      ],
    );
  }
}
