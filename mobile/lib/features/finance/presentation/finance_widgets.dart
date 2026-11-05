import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/finance.dart';
import '../../../shared/visuals.dart';

class ClearanceBanner extends StatelessWidget {
  const ClearanceBanner({required this.clearance, this.action, super.key});

  final Clearance clearance;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final (tone, icon, title) = switch (clearance.state) {
      ClearanceState.cleared => (
        AppTone.success,
        Icons.verified_rounded,
        'Al día para la publicación vigente',
      ),
      ClearanceState.notCleared => (
        AppTone.warning,
        Icons.error_outline_rounded,
        'Pagos pendientes para ver notas',
      ),
      ClearanceState.pendingSetup => (
        AppTone.blue,
        Icons.pending_actions_rounded,
        'Plan financiero en configuración',
      ),
    };
    final pair = AppColors.of(context).tone(tone);
    return AppCard(
      tone: tone,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, color: pair.foreground),
              const SizedBox(width: Spacing.sm),
              Expanded(
                child: Text(
                  title,
                  style: theme.textTheme.titleSmall?.copyWith(color: pair.foreground),
                ),
              ),
            ],
          ),
          const SizedBox(height: Spacing.sm),
          Text(clearance.requirementLabel, style: theme.textTheme.bodySmall),
          if (clearance.pendingPeriods.isNotEmpty) ...[
            const SizedBox(height: Spacing.sm),
            Wrap(
              spacing: Spacing.sm,
              runSpacing: 4,
              children: [
                for (final p in clearance.pendingPeriods)
                  StatusPill(label: p, tone: AppTone.danger),
              ],
            ),
          ],
          if (action != null) ...[const SizedBox(height: Spacing.md), action!],
        ],
      ),
    );
  }
}

class ChargeTile extends ConsumerWidget {
  const ChargeTile({required this.charge, this.trailing, super.key});

  final Charge charge;
  final Widget? trailing;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: Spacing.lg, vertical: Spacing.md),
      child: Row(
        children: [
          ?trailing,
          if (trailing != null) const SizedBox(width: Spacing.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(charge.label, style: theme.textTheme.titleSmall),
                Text(
                  'Vence ${format.shortDate(charge.dueDate)}',
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
              ],
            ),
          ),
          const SizedBox(width: Spacing.sm),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                format.currency(charge.status.isOpen ? charge.balance : charge.amount),
                style: theme.textTheme.titleSmall,
              ),
              const SizedBox(height: 4),
              StatusPill(label: charge.status.label, tone: charge.status.tone),
            ],
          ),
        ],
      ),
    );
  }
}

class PaymentTile extends ConsumerWidget {
  const PaymentTile({required this.payment, this.onTap, this.showStudent = false, super.key});

  final Payment payment;
  final VoidCallback? onTap;
  final bool showStudent;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return ListTile(
      onTap: onTap,
      leading: const ToneIcon(icon: Icons.receipt_rounded, tone: AppTone.success, size: 40),
      title: Text(
        showStudent ? payment.studentName : payment.receiptNumber,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      subtitle: Text(
        '${showStudent ? '${payment.receiptNumber} · ' : ''}${payment.method.label} · ${format.shortDate(payment.paidAt)}',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: Text(
        format.currency(payment.amount),
        style: theme.textTheme.titleSmall?.copyWith(color: colors.tone(AppTone.success).foreground),
      ),
    );
  }
}

class EvidenceStatusTile extends ConsumerWidget {
  const EvidenceStatusTile({required this.evidence, super.key});

  final PaymentEvidence evidence;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final format = ref.watch(formattersProvider);
    return ListTile(
      leading: ToneIcon(icon: Icons.upload_file_rounded, tone: evidence.status.tone, size: 40),
      title: Text('${format.currency(evidence.amount)} · ${evidence.reference}'),
      subtitle: Text(
        evidence.status == EvidenceStatus.rejected && evidence.rejectionReason != null
            ? 'Rechazado: ${evidence.rejectionReason}'
            : 'Enviado ${format.shortDate(evidence.submittedAt)} · ${evidence.bank}',
      ),
      trailing: StatusPill(label: evidence.status.label, tone: evidence.status.tone),
    );
  }
}
