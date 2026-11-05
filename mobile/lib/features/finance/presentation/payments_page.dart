import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/utils/text_search.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/states.dart';
import '../../../shared/page_layout.dart';
import '../application/finance_providers.dart';
import 'finance_widgets.dart';

class PaymentsPage extends ConsumerStatefulWidget {
  const PaymentsPage({super.key});

  @override
  ConsumerState<PaymentsPage> createState() => _PaymentsPageState();
}

class _PaymentsPageState extends ConsumerState<PaymentsPage> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final payments = ref.watch(paymentsProvider);
    final filtered = payments.value
        ?.where((p) => matchesSearch('${p.studentName} ${p.receiptNumber} ${p.reference}', _query))
        .toList();

    return TabPage(
      subtitle: 'Movimientos',
      onRefresh: () => safeRefresh([ref.refresh(paymentsProvider.future)]),
      slivers: [
        if (filtered != null && filtered.isNotEmpty)
          SliverList.builder(
            itemCount: filtered.length,
            itemBuilder: (context, index) {
              final p = filtered[index];
              return PaymentTile(
                payment: p,
                showStudent: true,
                onTap: () => context.push('/receipts/${p.id}'),
              );
            },
          ),
      ],
      children: [
        const PageIntro(eyebrow: 'Historial', title: 'Pagos y recibos'),
        TextField(
          onChanged: (value) => setState(() => _query = value),
          decoration: const InputDecoration(
            hintText: 'Buscar por estudiante, recibo o referencia…',
            prefixIcon: Icon(Icons.search_rounded),
          ),
        ),
        const SizedBox(height: Spacing.md),
        if (!payments.hasValue)
          AsyncValueView(
            value: payments,
            onRetry: () => ref.invalidate(paymentsProvider),
            data: (_) => const SizedBox.shrink(),
          )
        else if (filtered!.isEmpty)
          const EmptyState(icon: Icons.search_off_rounded, title: 'Sin resultados'),
      ],
    );
  }
}
