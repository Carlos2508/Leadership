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
import '../../../shared/back_navigation.dart';
import '../../../shared/page_layout.dart';
import '../../academics/application/academic_providers.dart';
import '../application/finance_providers.dart';

/// Envío de comprobante por la familia. Queda "En revisión" y no habilita
/// notas hasta que caja lo valide.
class EvidenceFormPage extends ConsumerStatefulWidget {
  const EvidenceFormPage({required this.studentId, super.key});

  final String studentId;

  @override
  ConsumerState<EvidenceFormPage> createState() => _EvidenceFormPageState();
}

class _EvidenceFormPageState extends ConsumerState<EvidenceFormPage> {
  static const _banks = ['Banco Ejemplo', 'Banco Demostración', 'Cooperativa de prueba'];

  final _formKey = GlobalKey<FormState>();
  final _amount = TextEditingController();
  final _reference = TextEditingController();
  String _bank = _banks.first;
  late DateTime _paidOn = dateOnly(ref.read(clockProvider)());
  String? _fileName;
  bool _submitting = false;
  String? _error;
  bool _prefilled = false;

  @override
  void dispose() {
    _amount.dispose();
    _reference.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final today = dateOnly(ref.read(clockProvider)());
    final picked = await showDatePicker(
      context: context,
      initialDate: _paidOn,
      firstDate: today.subtract(const Duration(days: 60)),
      lastDate: today,
      helpText: 'Fecha del pago',
    );
    if (picked != null) setState(() => _paidOn = picked);
  }

  Future<void> _submit() async {
    final valid = _formKey.currentState?.validate() ?? false;
    if (_fileName == null) {
      setState(() => _error = 'Adjunta la foto del comprobante.');
      return;
    }
    if (!valid || _submitting) return;
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await ref
          .read(financeActionsProvider)
          .submitEvidence(
            widget.studentId,
            EvidenceDraft(
              amount: double.parse(_amount.text.replaceAll(',', '')),
              reference: _reference.text,
              bank: _bank,
              paidOn: _paidOn,
              fileName: _fileName!,
            ),
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Comprobante enviado. Caja lo revisará pronto.')),
      );
      context.pop();
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _submitting = false;
          _error = friendlyError(error);
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final format = ref.watch(formattersProvider);
    final student = ref.watch(studentProvider(widget.studentId)).value;
    final statement = ref.watch(statementProvider(widget.studentId)).value;
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    if (!_prefilled && statement != null) {
      _prefilled = true;
      final overdue = statement.openCharges
          .where((c) => c.dueDate.isBefore(dateOnly(ref.read(clockProvider)())))
          .fold<double>(0, (sum, c) => sum + c.balance);
      if (overdue > 0) _amount.text = overdue.toStringAsFixed(2);
    }

    return UnsavedChangesGuard(
      hasChanges: !_submitting && (_reference.text.trim().isNotEmpty || _fileName != null),
      child: DetailPage(
        title: 'Registrar comprobante',
        bottomBar: FilledButton(
          onPressed: _submitting ? null : _submit,
          child: _submitting
              ? const SizedBox.square(
                  dimension: 22,
                  child: CircularProgressIndicator(strokeWidth: 2.5),
                )
              : const Text('Enviar comprobante'),
        ),
        children: [
          if (student != null)
            Padding(
              padding: const EdgeInsets.only(bottom: Spacing.lg),
              child: Row(
                children: [
                  InitialsAvatar(initials: student.initials),
                  const SizedBox(width: Spacing.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Para ${student.fullName}', style: theme.textTheme.titleMedium),
                        Text(
                          student.placement,
                          style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          Form(
            key: _formKey,
            onChanged: () => setState(() {}),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextFormField(
                  controller: _amount,
                  enabled: !_submitting,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
                  decoration: InputDecoration(
                    labelText: 'Monto pagado',
                    prefixText: '${format.currencySymbol} ',
                  ),
                  validator: (value) {
                    final amount = double.tryParse((value ?? '').replaceAll(',', ''));
                    if (amount == null || amount <= 0) return 'Escribe un monto válido.';
                    if (amount > 50000) return 'Verifica el monto.';
                    return null;
                  },
                ),
                const SizedBox(height: Spacing.lg),
                TextFormField(
                  controller: _reference,
                  enabled: !_submitting,
                  textCapitalization: TextCapitalization.characters,
                  decoration: const InputDecoration(
                    labelText: 'Número de referencia o boleta',
                    prefixIcon: Icon(Icons.tag_rounded),
                  ),
                  validator: (value) =>
                      (value ?? '').trim().length < 4 ? 'Escribe la referencia del pago.' : null,
                ),
                const SizedBox(height: Spacing.lg),
                DropdownButtonFormField<String>(
                  isExpanded: true,
                  initialValue: _bank,
                  decoration: const InputDecoration(
                    labelText: 'Banco o medio',
                    prefixIcon: Icon(Icons.account_balance_rounded),
                  ),
                  items: [for (final b in _banks) DropdownMenuItem(value: b, child: Text(b))],
                  onChanged: _submitting ? null : (value) => setState(() => _bank = value ?? _bank),
                ),
                const SizedBox(height: Spacing.lg),
                AppCard(
                  padding: EdgeInsets.zero,
                  child: ListTile(
                    leading: const Icon(Icons.event_rounded),
                    title: const Text('Fecha del pago'),
                    subtitle: Text(format.shortDate(_paidOn)),
                    trailing: const Icon(Icons.edit_calendar_rounded),
                    onTap: _submitting ? null : _pickDate,
                  ),
                ),
                const SizedBox(height: Spacing.lg),
                OutlinedButton.icon(
                  onPressed: _submitting
                      ? null
                      : () => setState(() {
                          _fileName =
                              'comprobante_${format.shortDate(_paidOn).replaceAll('/', '')}.jpg';
                          _error = null;
                        }),
                  icon: Icon(
                    _fileName == null ? Icons.add_a_photo_outlined : Icons.check_circle_rounded,
                  ),
                  label: Text(_fileName ?? 'Adjuntar foto del comprobante'),
                ),
                if (_error != null) ...[
                  const SizedBox(height: Spacing.md),
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
                const SizedBox(height: Spacing.lg),
                AppCard(
                  tone: AppTone.accent,
                  child: Text(
                    'Un comprobante no confirma el pago por sí solo: caja lo valida y lo aplica a las cuotas. '
                    'Las notas se habilitan cuando las cuotas requeridas quedan cubiertas.',
                    style: theme.textTheme.bodySmall?.copyWith(height: 1.45),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
