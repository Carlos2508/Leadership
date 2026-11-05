import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/format/formatters.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/finance.dart';
import '../../../shared/page_layout.dart';
import '../application/finance_providers.dart';

/// Flujo de caja: cargos → monto y medio → revisión de la distribución →
/// confirmación idempotente → recibo.
class RegisterPaymentPage extends ConsumerStatefulWidget {
  const RegisterPaymentPage({required this.studentId, super.key});

  final String studentId;

  @override
  ConsumerState<RegisterPaymentPage> createState() => _RegisterPaymentPageState();
}

class _RegisterPaymentPageState extends ConsumerState<RegisterPaymentPage> {
  final _formKey = GlobalKey<FormState>();
  final _amount = TextEditingController();
  final _reference = TextEditingController();

  /// Clave única por intento: un doble toque o reintento no duplica el pago.
  final String _idempotencyKey =
      '${DateTime.now().microsecondsSinceEpoch}-${Random.secure().nextInt(1 << 32)}';

  final Set<String> _selected = {};
  PaymentMethod _method = PaymentMethod.cash;
  bool _initialized = false;
  bool _amountEdited = false;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _amount.dispose();
    _reference.dispose();
    super.dispose();
  }

  double _selectedTotal(List<Charge> open) =>
      open.where((c) => _selected.contains(c.id)).fold(0, (sum, c) => sum + c.balance);

  void _syncAmount(List<Charge> open) {
    if (!_amountEdited) _amount.text = _selectedTotal(open).toStringAsFixed(2);
  }

  PaymentRequest _request(List<Charge> open) => PaymentRequest(
    studentId: widget.studentId,
    chargeIds: [
      for (final c in open)
        if (_selected.contains(c.id)) c.id,
    ],
    amount: double.parse(_amount.text.replaceAll(',', '')),
    method: _method,
    reference: _reference.text.trim().isEmpty
        ? _method.label.toUpperCase()
        : _reference.text.trim(),
    idempotencyKey: _idempotencyKey,
  );

  Future<void> _review(List<Charge> open, Formatters format) async {
    setState(() => _error = null);
    if (_selected.isEmpty) {
      setState(() => _error = 'Selecciona al menos un cargo.');
      return;
    }
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final request = _request(open);
    setState(() => _busy = true);
    try {
      final allocations = await ref.read(financeActionsProvider).preview(request);
      if (!mounted) return;
      setState(() => _busy = false);
      final applied = allocations.fold<double>(0, (s, a) => s + a.amount);
      final confirmed = await showModalBottomSheet<bool>(
        context: context,
        isScrollControlled: true,
        builder: (context) => _ReviewSheet(
          request: request,
          allocations: allocations,
          credit: max(0, request.amount - applied),
          format: format,
        ),
      );
      if (confirmed == true) await _confirm(request);
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = friendlyError(error);
        });
      }
    }
  }

  Future<void> _confirm(PaymentRequest request) async {
    setState(() => _busy = true);
    try {
      final payment = await ref.read(financeActionsProvider).registerPayment(request);
      if (!mounted) return;
      context.pushReplacement('/receipts/${payment.id}');
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = '${friendlyError(error)} Puedes reintentar: el pago no se duplicará.';
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final statement = ref.watch(statementProvider(widget.studentId));
    final format = ref.watch(formattersProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final open = statement.value?.openCharges ?? const <Charge>[];

    if (!_initialized && statement.hasValue && open.isNotEmpty) {
      _initialized = true;
      _selected.add(open.first.id); // la cuota más antigua por defecto
      _syncAmount(open);
    }

    return DetailPage(
      title: 'Registrar pago',
      bottomBar: FilledButton(
        onPressed: _busy || open.isEmpty ? null : () => _review(open, format),
        child: _busy
            ? const SizedBox.square(
                dimension: 22,
                child: CircularProgressIndicator(strokeWidth: 2.5),
              )
            : const Text('Revisar pago'),
      ),
      children: [
        AsyncValueView(
          value: statement,
          onRetry: () => ref.invalidate(statementProvider(widget.studentId)),
          data: (data) => Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(data.student.fullName, style: theme.textTheme.titleLarge),
                Text(
                  '${data.student.code} · ${data.student.placement}',
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
                const SectionHeader(title: '1. Cargos a cubrir'),
                AppCard(
                  padding: const EdgeInsets.symmetric(vertical: Spacing.xs),
                  child: Column(
                    children: [
                      for (final charge in open)
                        CheckboxListTile(
                          value: _selected.contains(charge.id),
                          onChanged: _busy
                              ? null
                              : (value) => setState(() {
                                  value == true
                                      ? _selected.add(charge.id)
                                      : _selected.remove(charge.id);
                                  _syncAmount(open);
                                }),
                          title: Text(charge.label),
                          subtitle: Text(
                            '${charge.status.label} · vence ${format.shortDate(charge.dueDate)}',
                          ),
                          secondary: Text(
                            format.currency(charge.balance),
                            style: theme.textTheme.titleSmall,
                          ),
                          controlAffinity: ListTileControlAffinity.leading,
                        ),
                    ],
                  ),
                ),
                const SizedBox(height: Spacing.sm),
                Text(
                  'Se aplica en el orden mostrado, de la cuota más antigua a la más reciente.',
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
                const SectionHeader(title: '2. Monto y medio'),
                TextFormField(
                  controller: _amount,
                  enabled: !_busy,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
                  onChanged: (_) => _amountEdited = true,
                  decoration: InputDecoration(
                    labelText: 'Monto recibido',
                    prefixText: '${format.currencySymbol} ',
                    helperText:
                        'Seleccionado: ${format.currency(_selectedTotal(open))}. El excedente queda como saldo a favor.',
                    helperMaxLines: 2,
                  ),
                  validator: (value) {
                    final amount = double.tryParse((value ?? '').replaceAll(',', ''));
                    if (amount == null || amount <= 0) return 'Escribe un monto válido.';
                    return null;
                  },
                ),
                const SizedBox(height: Spacing.lg),
                Wrap(
                  spacing: Spacing.sm,
                  runSpacing: Spacing.sm,
                  children: [
                    for (final method in PaymentMethod.values)
                      ChoiceChip(
                        label: Text(method.label),
                        selected: _method == method,
                        onSelected: _busy ? null : (_) => setState(() => _method = method),
                      ),
                  ],
                ),
                const SizedBox(height: Spacing.lg),
                TextFormField(
                  controller: _reference,
                  enabled: !_busy,
                  textCapitalization: TextCapitalization.characters,
                  decoration: InputDecoration(
                    labelText: _method == PaymentMethod.cash
                        ? 'Referencia (opcional)'
                        : 'Referencia bancaria',
                    prefixIcon: const Icon(Icons.tag_rounded),
                  ),
                  validator: (value) =>
                      _method != PaymentMethod.cash && (value ?? '').trim().length < 4
                      ? 'La referencia es obligatoria para este medio.'
                      : null,
                ),
                if (_error != null) ...[
                  const SizedBox(height: Spacing.lg),
                  Semantics(
                    liveRegion: true,
                    child: Text(
                      _error!,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: colors.tone(AppTone.danger).foreground,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _ReviewSheet extends StatelessWidget {
  const _ReviewSheet({
    required this.request,
    required this.allocations,
    required this.credit,
    required this.format,
  });

  final PaymentRequest request;
  final List<PaymentAllocation> allocations;
  final double credit;
  final Formatters format;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(Spacing.xl, 0, Spacing.xl, Spacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('3. Revisa la distribución', style: theme.textTheme.headlineSmall),
            const SizedBox(height: Spacing.lg),
            for (final a in allocations) InfoRow(label: a.label, value: format.currency(a.amount)),
            if (credit > 0) InfoRow(label: 'Saldo a favor', value: format.currency(credit)),
            const Divider(height: Spacing.xl),
            InfoRow(label: 'Total recibido', value: format.currency(request.amount)),
            InfoRow(label: 'Medio', value: request.method.label),
            InfoRow(label: 'Referencia', value: request.reference),
            const SizedBox(height: Spacing.xl),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('Confirmar y emitir recibo'),
            ),
            const SizedBox(height: Spacing.sm),
            OutlinedButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Corregir'),
            ),
          ],
        ),
      ),
    );
  }
}
