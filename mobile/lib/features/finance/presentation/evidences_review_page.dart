import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/states.dart';
import '../../../domain/models/finance.dart';
import '../../../shared/page_layout.dart';
import '../../../shared/visuals.dart';
import '../application/finance_providers.dart';

class EvidencesReviewPage extends ConsumerStatefulWidget {
  const EvidencesReviewPage({super.key});

  @override
  ConsumerState<EvidencesReviewPage> createState() => _EvidencesReviewPageState();
}

class _EvidencesReviewPageState extends ConsumerState<EvidencesReviewPage> {
  bool _onlyPending = true;

  @override
  Widget build(BuildContext context) {
    final evidences = ref.watch(evidencesProvider);
    return TabPage(
      subtitle: 'Comprobantes',
      onRefresh: () => safeRefresh([ref.refresh(evidencesProvider.future)]),
      children: [
        const PageIntro(
          eyebrow: 'Validación',
          title: 'Comprobantes de familias',
          subtitle: 'Acepta para aplicar el pago a las cuotas más antiguas o rechaza indicando el motivo.',
        ),
        Wrap(
          spacing: Spacing.sm,
          children: [
            ChoiceChip(
              label: const Text('En revisión'),
              selected: _onlyPending,
              onSelected: (_) => setState(() => _onlyPending = true),
            ),
            ChoiceChip(
              label: const Text('Todos'),
              selected: !_onlyPending,
              onSelected: (_) => setState(() => _onlyPending = false),
            ),
          ],
        ),
        const SizedBox(height: Spacing.lg),
        AsyncValueView(
          value: evidences,
          onRetry: () => ref.invalidate(evidencesProvider),
          data: (list) {
            final filtered = _onlyPending
                ? list.where((e) => e.status == EvidenceStatus.inReview).toList()
                : list;
            if (filtered.isEmpty) {
              return const EmptyState(
                icon: Icons.inbox_rounded,
                title: 'Nada por revisar',
                message: 'Los comprobantes nuevos aparecerán aquí.',
              );
            }
            return Column(
              children: [
                for (final e in filtered) ...[
                  _EvidenceCard(evidence: e),
                  const SizedBox(height: Spacing.md),
                ],
              ],
            );
          },
        ),
      ],
    );
  }
}

class _EvidenceCard extends ConsumerStatefulWidget {
  const _EvidenceCard({required this.evidence});

  final PaymentEvidence evidence;

  @override
  ConsumerState<_EvidenceCard> createState() => _EvidenceCardState();
}

class _EvidenceCardState extends ConsumerState<_EvidenceCard> {
  bool _busy = false;

  Future<void> _accept() async {
    final format = ref.read(formattersProvider);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('¿Aceptar comprobante?'),
        content: Text(
          'Se registrará un pago de ${format.currency(widget.evidence.amount)} para '
          '${widget.evidence.studentName} y se emitirá su recibo.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Aceptar')),
        ],
      ),
    );
    if (confirmed == true) await _review(accept: true);
  }

  Future<void> _reject() async {
    final reason = await showDialog<String>(
      context: context,
      builder: (_) => const _RejectDialog(),
    );
    if (reason != null) await _review(accept: false, reason: reason);
  }

  Future<void> _review({required bool accept, String? reason}) async {
    setState(() => _busy = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref
          .read(financeActionsProvider)
          .reviewEvidence(widget.evidence, accept: accept, reason: reason);
      messenger.showSnackBar(
        SnackBar(
          content: Text(accept ? 'Pago aplicado y recibo emitido.' : 'Comprobante rechazado.'),
        ),
      );
    } on Object catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(friendlyError(error))));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final e = widget.evidence;
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              ToneIcon(icon: Icons.image_outlined, tone: e.status.tone),
              const SizedBox(width: Spacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(e.studentName, style: theme.textTheme.titleSmall),
                    Text(
                      'Enviado por ${e.submittedBy}',
                      style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    ),
                  ],
                ),
              ),
              StatusPill(label: e.status.label, tone: e.status.tone),
            ],
          ),
          const SizedBox(height: Spacing.md),
          InfoRow(label: 'Monto', value: format.currency(e.amount)),
          InfoRow(label: 'Referencia', value: e.reference),
          InfoRow(label: 'Banco', value: e.bank),
          InfoRow(label: 'Fecha de pago', value: format.shortDate(e.paidOn)),
          InfoRow(label: 'Archivo', value: e.fileName),
          if (e.rejectionReason != null) InfoRow(label: 'Motivo', value: e.rejectionReason!),
          if (e.status == EvidenceStatus.inReview) ...[
            const SizedBox(height: Spacing.md),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: _busy ? null : _reject,
                    child: const Text('Rechazar'),
                  ),
                ),
                const SizedBox(width: Spacing.md),
                Expanded(
                  child: FilledButton(
                    onPressed: _busy ? null : _accept,
                    child: _busy
                        ? const SizedBox.square(
                            dimension: 20,
                            child: CircularProgressIndicator(strokeWidth: 2.5),
                          )
                        : const Text('Aceptar'),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

class _RejectDialog extends StatefulWidget {
  const _RejectDialog();

  @override
  State<_RejectDialog> createState() => _RejectDialogState();
}

class _RejectDialogState extends State<_RejectDialog> {
  final _controller = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Motivo del rechazo'),
      content: TextField(
        controller: _controller,
        autofocus: true,
        maxLength: 200,
        decoration: InputDecoration(hintText: 'Ej.: la imagen no es legible', errorText: _error),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
        FilledButton(
          onPressed: () {
            final text = _controller.text.trim();
            if (text.length < 5) {
              setState(() => _error = 'Explica el motivo (mínimo 5 caracteres).');
              return;
            }
            Navigator.pop(context, text);
          },
          child: const Text('Rechazar'),
        ),
      ],
    );
  }
}
