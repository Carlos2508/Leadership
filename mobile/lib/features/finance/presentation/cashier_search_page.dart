import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/states.dart';
import '../../../shared/page_layout.dart';
import '../application/finance_providers.dart';

class CashierSearchPage extends ConsumerStatefulWidget {
  const CashierSearchPage({super.key});

  @override
  ConsumerState<CashierSearchPage> createState() => _CashierSearchPageState();
}

class _CashierSearchPageState extends ConsumerState<CashierSearchPage> {
  final _controller = TextEditingController();
  Timer? _debounce;
  String _query = '';

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), () {
      if (mounted) setState(() => _query = value.trim());
    });
  }

  @override
  Widget build(BuildContext context) {
    final results = ref.watch(studentSearchProvider(_query));
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return TabPage(
      subtitle: 'Cobrar',
      children: [
        const PageIntro(
          eyebrow: 'Caja',
          title: 'Registrar un pago',
          subtitle: 'Busca al estudiante para ver sus cargos y aplicar el pago.',
        ),
        TextField(
          controller: _controller,
          onChanged: _onChanged,
          textInputAction: TextInputAction.search,
          decoration: InputDecoration(
            hintText: 'Nombre, código o grado…',
            prefixIcon: const Icon(Icons.search_rounded),
            suffixIcon: _controller.text.isEmpty
                ? null
                : IconButton(
                    tooltip: 'Limpiar búsqueda',
                    onPressed: () {
                      _controller.clear();
                      _onChanged('');
                      setState(() {});
                    },
                    icon: const Icon(Icons.close_rounded),
                  ),
          ),
        ),
        const SizedBox(height: Spacing.lg),
        AsyncValueView(
          value: results,
          onRetry: () => ref.invalidate(studentSearchProvider(_query)),
          data: (list) {
            if (list.isEmpty) {
              return const EmptyState(
                icon: Icons.person_search_rounded,
                title: 'Sin coincidencias',
                message: 'Prueba con otro nombre o con el código del estudiante.',
              );
            }
            return AppCard(
              padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
              child: Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(
                      Spacing.lg,
                      Spacing.sm,
                      Spacing.lg,
                      Spacing.sm,
                    ),
                    child: Align(
                      alignment: Alignment.centerLeft,
                      child: Text(
                        '${list.length} ${list.length == 1 ? 'estudiante' : 'estudiantes'}',
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                    ),
                  ),
                  for (final s in list.take(60))
                    ListTile(
                      leading: InitialsAvatar(
                        initials: s.initials,
                        size: 40,
                        tone: AppTone.neutral,
                      ),
                      title: Text(s.fullName),
                      subtitle: Text('${s.code} · ${s.placement}'),
                      trailing: const Icon(Icons.chevron_right_rounded),
                      onTap: () => context.push('/accounts/${s.id}'),
                    ),
                ],
              ),
            );
          },
        ),
      ],
    );
  }
}
