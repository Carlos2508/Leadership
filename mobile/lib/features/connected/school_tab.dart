import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/cards.dart';
import '../../core/widgets/states.dart';
import '../../shared/page_layout.dart';
import 'academic_page.dart';
import 'connected_session.dart';
import 'connected_shell.dart';
import 'inventory_page.dart';
import 'school_api.dart';
import 'students_tab.dart';
import 'timetable_page.dart';

/// Staff tools available from the phone: student directory (people.read) and cashier (finance.read / collect).
class SchoolTab extends ConsumerStatefulWidget {
  const SchoolTab({super.key});

  @override
  ConsumerState<SchoolTab> createState() => _SchoolTabState();
}

class _SchoolTabState extends ConsumerState<SchoolTab> {
  final _search = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(connectedControllerProvider)!;
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final canPeople = session.can('people.read');
    final path = '/api/v1/people?page=1&search=${Uri.encodeQueryComponent(_query)}';
    return ConnectedTabPage(
      onRefresh: () async => ref.invalidate(apiGetProvider(path)),
      children: [
        if (session.can('academic.read')) ...[
          AppCard(
            onTap: () =>
                Navigator.of(context)
                    .push(MaterialPageRoute<void>(builder: (_) => const TimetablePage())),
            child: const ListTile(
              contentPadding: EdgeInsets.zero,
              leading: Icon(Icons.calendar_month_outlined),
              title: Text('Horarios'),
              subtitle: Text('Grupos, jornada semanal e impresión'),
              trailing: Icon(Icons.chevron_right),
            ),
          ),
          const SizedBox(height: Spacing.lg),
        ],
        PageIntro(
          eyebrow: 'Colegio',
          title: session.can('finance.read') ? 'Estudiantes y caja' : 'Colegio',
          subtitle: canPeople ? 'Busca por nombre o código.' : null,
        ),
        if (session.can('academic.read')) ...[
          AppCard(
            onTap: () =>
                Navigator.of(context)
                    .push(MaterialPageRoute<void>(builder: (_) => const AcademicYearPage())),
            child: Row(
              children: [
                const ToneIcon(icon: Icons.event_note_outlined, tone: AppTone.accent),
                const SizedBox(width: Spacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Ciclo escolar', style: theme.textTheme.titleSmall),
                      Text(
                        session.can('academic.manage')
                            ? 'Prepara bimestres, secciones, cursos y el inicio de clases'
                            : 'Bimestres, cursos y estado del ciclo',
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                    ],
                  ),
                ),
                Icon(Icons.chevron_right, color: colors.muted),
              ],
            ),
          ),
          const SizedBox(height: Spacing.lg),
        ],
        if (session.can('inventory.read')) ...[
          AppCard(
            onTap: () =>
                Navigator.of(context)
                    .push(MaterialPageRoute<void>(builder: (_) => const InventoryPage())),
            child: Row(
              children: [
                const ToneIcon(icon: Icons.inventory_2_outlined, tone: AppTone.accent),
                const SizedBox(width: Spacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Inventario y bodega', style: theme.textTheme.titleSmall),
                      Text(
                        'Artículos, existencias y movimientos',
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                    ],
                  ),
                ),
                Icon(Icons.chevron_right, color: colors.muted),
              ],
            ),
          ),
          const SizedBox(height: Spacing.lg),
        ],
        if (canPeople) ...[
          TextField(
            controller: _search,
            textInputAction: TextInputAction.search,
            onSubmitted: (v) => setState(() => _query = v.trim()),
            decoration: InputDecoration(
              labelText: 'Buscar estudiante',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: IconButton(
                tooltip: 'Buscar',
                icon: const Icon(Icons.arrow_forward),
                onPressed: () => setState(() => _query = _search.text.trim()),
              ),
            ),
          ),
          const SizedBox(height: Spacing.lg),
          if (_query.length < 2)
            Text(
              'Escribe al menos dos letras y pulsa buscar.',
              style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
            )
          else
            ApiBuilder(
              path: path,
              builder: (context, data) {
                final items = ((data as Map<String, dynamic>)['items'] as List<dynamic>)
                    .cast<Map<String, dynamic>>();
                if (items.isEmpty) {
                  return const EmptyState(
                    icon: Icons.search_off,
                    title: 'No encontramos coincidencias',
                  );
                }
                return Column(
                  children: [
                    for (final p in items)
                      Padding(
                        padding: const EdgeInsets.only(bottom: Spacing.sm),
                        child: AppCard(
                          onTap: p['studentCode'] != null && session.can('finance.read')
                              ? () => Navigator.of(context).push(
                                  MaterialPageRoute<void>(
                                    builder: (_) => CashierStudentPage(person: p),
                                  ),
                                )
                              : null,
                          child: Row(
                            children: [
                              InitialsAvatar(
                                initials:
                                    '${(p['givenNames'] as String? ?? '?')[0]}${(p['familyNames'] as String? ?? ' ')[0]}'
                                        .trim()
                                        .toUpperCase(),
                              ),
                              const SizedBox(width: Spacing.md),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      p['displayName'] as String,
                                      style: theme.textTheme.titleSmall,
                                    ),
                                    Text(
                                      [
                                        if (p['studentCode'] != null)
                                          'Estudiante ${p['studentCode']}',
                                        if (p['isGuardian'] == true) 'Padre o encargado',
                                        if (p['isStaff'] == true) 'Personal',
                                        if (p['account'] != null) 'Cuenta ${p['account']}',
                                      ].join(' · '),
                                      style: theme.textTheme.bodySmall?.copyWith(
                                        color: colors.muted,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              StatusPill(
                                label: p['status'] == 'Active' ? 'Activo' : '${p['status']}',
                                tone: p['status'] == 'Active' ? AppTone.success : AppTone.neutral,
                              ),
                            ],
                          ),
                        ),
                      ),
                  ],
                );
              },
            ),
        ],
      ],
    );
  }
}

/// Statement of a student for staff with finance.read; payment registration with finance.collect.
class CashierStudentPage extends ConsumerWidget {
  const CashierStudentPage({required this.person, super.key});

  final Map<String, dynamic> person;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(connectedControllerProvider);
    final path = '/api/v1/finance/students/${person['id']}/statement';
    return DetailPage(
      title: 'Estado de cuenta',
      onRefresh: () async => ref.invalidate(apiGetProvider(path)),
      bottomBar: session?.can('finance.collect') == true
          ? FilledButton.icon(
              onPressed: () async {
                final statement =
                    await ref.read(apiGetProvider(path).future) as Map<String, dynamic>;
                if (!context.mounted) return;
                final done = await showModalBottomSheet<bool>(
                  context: context,
                  isScrollControlled: true,
                  builder: (_) => _PaymentSheet(statement: statement),
                );
                if (done == true) ref.invalidate(apiGetProvider(path));
              },
              icon: const Icon(Icons.point_of_sale),
              label: const Text('Registrar pago'),
            )
          : null,
      children: [
        PageIntro(
          eyebrow: 'Estudiante ${person['studentCode']}',
          title: person['displayName'] as String,
        ),
        StatementView(path: path),
      ],
    );
  }
}

class _PaymentSheet extends ConsumerStatefulWidget {
  const _PaymentSheet({required this.statement});

  final Map<String, dynamic> statement;

  @override
  ConsumerState<_PaymentSheet> createState() => _PaymentSheetState();
}

class _PaymentSheetState extends ConsumerState<_PaymentSheet> {
  // One key per sheet: retrying after a network error never creates a second payment.
  final _idempotencyKey = List.generate(
    16,
    (_) => Random.secure().nextInt(256).toRadixString(16).padLeft(2, '0'),
  ).join();
  final _payer = TextEditingController();
  final _reference = TextEditingController();
  late final TextEditingController _amount;
  String _method = 'Cash';
  bool _busy = false;
  String? _error;
  late final List<Map<String, dynamic>> _pending;

  @override
  void initState() {
    super.initState();
    _pending =
        (widget.statement['charges'] as List<dynamic>)
            .cast<Map<String, dynamic>>()
            .where((c) => (c['balance'] as num) > 0)
            .toList()
          ..sort((a, b) => (a['dueOn'] as String).compareTo(b['dueOn'] as String));
    final oldest = _pending.isEmpty ? 0 : _pending.first['balance'] as num;
    _amount = TextEditingController(text: oldest == 0 ? '' : oldest.toStringAsFixed(2));
  }

  @override
  void dispose() {
    _payer.dispose();
    _reference.dispose();
    _amount.dispose();
    super.dispose();
  }

  /// Applies the amount to the oldest pending charges first, as the web proposes; the cashier sees the split.
  List<Map<String, dynamic>> _allocations(num amount) {
    var left = amount;
    final result = <Map<String, dynamic>>[];
    for (final c in _pending) {
      if (left <= 0) break;
      final balance = c['balance'] as num;
      final applied = left < balance ? left : balance;
      result.add({
        'chargeId': c['id'],
        'amount': applied,
        'label': c['description'] ?? c['concept'],
      });
      left -= applied;
    }
    return result;
  }

  Future<void> _save() async {
    final amount = num.tryParse(_amount.text.trim().replaceAll(',', '.'));
    final total = _pending.fold<num>(0, (sum, c) => sum + (c['balance'] as num));
    if (_payer.text.trim().length < 3) {
      setState(() => _error = 'Escribe el nombre de quien paga.');
      return;
    }
    if (amount == null || amount <= 0 || amount > total || (amount * 100).round() != amount * 100) {
      setState(
        () => _error = 'El monto debe ser mayor que cero y no superar el saldo (${money(total)}).',
      );
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final result = await ref.read(connectedControllerProvider)!.api.post(
        '/api/v1/finance/payments',
        {
          'idempotencyKey': _idempotencyKey,
          'payerName': _payer.text.trim(),
          'method': _method,
          'reference': _reference.text.trim().isEmpty ? null : _reference.text.trim(),
          'receivedOn': DateFormat('yyyy-MM-dd').format(DateTime.now()),
          'note': null,
          'amount': amount,
          'allocations': [
            for (final a in _allocations(amount))
              {'chargeId': a['chargeId'], 'amount': a['amount']},
          ],
        },
      );
      final number =
          ((result as Map<String, dynamic>)['receipt'] as Map<String, dynamic>?)?['number'];
      if (!mounted) return;
      Navigator.of(context).pop(true);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Pago registrado${number == null ? '' : ' · Recibo No. $number'}')),
      );
    } on ApiFailure catch (e) {
      setState(() {
        _error = e.message;
        _busy = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final amount = num.tryParse(_amount.text.trim().replaceAll(',', '.')) ?? 0;
    if (_pending.isEmpty) {
      return const Padding(
        padding: EdgeInsets.fromLTRB(Spacing.page, 0, Spacing.page, Spacing.xl),
        child: EmptyState(icon: Icons.check_circle_outline, title: 'Sin saldo pendiente'),
      );
    }
    return Padding(
      padding: EdgeInsets.fromLTRB(
        Spacing.page,
        0,
        Spacing.page,
        Spacing.lg + MediaQuery.viewInsetsOf(context).bottom,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Registrar pago', style: theme.textTheme.titleLarge),
            const SizedBox(height: Spacing.md),
            TextField(
              controller: _amount,
              enabled: !_busy,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
              onChanged: (_) => setState(() {}),
              decoration: const InputDecoration(labelText: 'Monto recibido', prefixText: 'Q '),
            ),
            const SizedBox(height: Spacing.sm),
            for (final a in _allocations(amount))
              Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: Text(
                  '${a['label']}: ${money(a['amount'] as num)}',
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
              ),
            const SizedBox(height: Spacing.md),
            TextField(
              controller: _payer,
              enabled: !_busy,
              textCapitalization: TextCapitalization.words,
              decoration: const InputDecoration(labelText: 'Nombre de quien paga'),
            ),
            const SizedBox(height: Spacing.md),
            DropdownButtonFormField<String>(
              initialValue: _method,
              decoration: const InputDecoration(labelText: 'Medio de pago'),
              items: const [
                DropdownMenuItem(value: 'Cash', child: Text('Efectivo')),
                DropdownMenuItem(value: 'Transfer', child: Text('Transferencia')),
                DropdownMenuItem(value: 'Deposit', child: Text('Depósito')),
                DropdownMenuItem(value: 'Card', child: Text('Tarjeta')),
                DropdownMenuItem(value: 'Check', child: Text('Cheque')),
                DropdownMenuItem(value: 'Other', child: Text('Otro')),
              ],
              onChanged: _busy ? null : (v) => setState(() => _method = v ?? 'Cash'),
            ),
            const SizedBox(height: Spacing.md),
            TextField(
              controller: _reference,
              enabled: !_busy,
              decoration: const InputDecoration(labelText: 'Referencia (opcional)'),
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(top: Spacing.sm),
                child: Text(
                  _error!,
                  style: TextStyle(color: theme.colorScheme.error, fontWeight: FontWeight.w600),
                ),
              ),
            const SizedBox(height: Spacing.lg),
            FilledButton(
              onPressed: _busy ? null : _save,
              child: Text(_busy ? 'Registrando…' : 'Confirmar pago'),
            ),
          ],
        ),
      ),
    );
  }
}
