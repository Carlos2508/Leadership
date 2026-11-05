import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/utils/text_search.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/states.dart';
import '../../../domain/models/administration.dart';
import '../../../domain/models/people.dart';
import '../../../shared/page_layout.dart';
import '../../../shared/visuals.dart';
import '../../auth/application/auth_controller.dart';
import '../application/administration_providers.dart';

class AccountsPage extends ConsumerStatefulWidget {
  const AccountsPage({super.key});

  @override
  ConsumerState<AccountsPage> createState() => _AccountsPageState();
}

class _AccountsPageState extends ConsumerState<AccountsPage> {
  String _query = '';
  AccountState? _state;

  @override
  Widget build(BuildContext context) {
    final accounts = ref.watch(accountsProvider);
    return TabPage(
      subtitle: 'Acceso y seguridad',
      onRefresh: () => safeRefresh([ref.refresh(accountsProvider.future)]),
      children: [
        const PageIntro(
          eyebrow: 'Cuentas',
          title: 'Accesos de la comunidad',
          subtitle: 'Activa, desactiva y entrega códigos de un solo uso por un canal verificado.',
        ),
        TextField(
          onChanged: (v) => setState(() => _query = v),
          decoration: const InputDecoration(
            hintText: 'Buscar por nombre o usuario…',
            prefixIcon: Icon(Icons.search_rounded),
          ),
        ),
        const SizedBox(height: Spacing.md),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              for (final s in [null, ...AccountState.values])
                Padding(
                  padding: const EdgeInsets.only(right: Spacing.sm),
                  child: ChoiceChip(
                    label: Text(s?.label ?? 'Todas'),
                    selected: _state == s,
                    onSelected: (_) => setState(() => _state = s),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: Spacing.lg),
        AsyncValueView(
          value: accounts,
          onRetry: () => ref.invalidate(accountsProvider),
          data: (list) {
            final filtered = list
                .where(
                  (a) =>
                      (_state == null || a.state == _state) &&
                      matchesSearch('${a.displayName} ${a.username}', _query),
                )
                .toList();
            if (filtered.isEmpty) {
              return const EmptyState(
                icon: Icons.manage_search_rounded,
                title: 'Sin cuentas que coincidan',
              );
            }
            return AppCard(
              padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
              child: Column(children: [for (final a in filtered) _AccountTile(account: a)]),
            );
          },
        ),
      ],
    );
  }
}

class _AccountTile extends ConsumerWidget {
  const _AccountTile({required this.account});

  final AccountSummary account;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final format = ref.watch(formattersProvider);
    final now = ref.watch(clockProvider)();
    return ListTile(
      leading: InitialsAvatar(
        initials: initialsOf(account.displayName),
        size: 40,
        tone: AppTone.neutral,
      ),
      title: Text(account.displayName, maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text(
        '${account.profileLabel} · ${account.username}'
        '${account.lastSignIn == null ? '' : ' · ${format.relativeDay(account.lastSignIn!, now)}'}',
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      trailing: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 112),
        child: StatusPill(label: account.state.label, tone: account.state.tone),
      ),
      onTap: () => showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        builder: (_) => _AccountSheet(account: account),
      ),
    );
  }
}

class _AccountSheet extends ConsumerStatefulWidget {
  const _AccountSheet({required this.account});

  final AccountSummary account;

  @override
  ConsumerState<_AccountSheet> createState() => _AccountSheetState();
}

class _AccountSheetState extends ConsumerState<_AccountSheet> {
  bool _busy = false;
  ActivationCode? _code;
  late AccountSummary _account = widget.account;

  Future<void> _issueCode() async {
    setState(() => _busy = true);
    try {
      final code = await ref.read(administrationActionsProvider).issueCode(_account.id);
      if (mounted) setState(() => _code = code);
    } on Object catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(friendlyError(error))));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _toggle() async {
    final activate = _account.state != AccountState.active;
    final isSelf = ref.read(requireSessionProvider).accountId == _account.id;
    if (!activate && isSelf) {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('No puedes desactivar tu propia cuenta.')));
      return;
    }
    setState(() => _busy = true);
    try {
      final updated = await ref
          .read(administrationActionsProvider)
          .setActive(_account.id, active: activate);
      if (mounted) setState(() => _account = updated);
    } on Object catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(friendlyError(error))));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final format = ref.watch(formattersProvider);
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(Spacing.xl, 0, Spacing.xl, Spacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(_account.displayName, style: theme.textTheme.headlineSmall),
            const SizedBox(height: Spacing.xs),
            Row(
              children: [
                Text(
                  '${_account.profileLabel} · ${_account.username}',
                  style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
                ),
                const Spacer(),
                StatusPill(label: _account.state.label, tone: _account.state.tone),
              ],
            ),
            const SizedBox(height: Spacing.xl),
            if (_code != null) ...[
              AppCard(
                tone: AppTone.accent,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Código de un solo uso', style: theme.textTheme.labelLarge),
                    const SizedBox(height: Spacing.sm),
                    Row(
                      children: [
                        Expanded(
                          child: SelectableText(
                            _code!.code,
                            style: theme.textTheme.headlineSmall?.copyWith(
                              letterSpacing: 3,
                              fontFeatures: const [],
                            ),
                          ),
                        ),
                        IconButton(
                          tooltip: 'Copiar código',
                          onPressed: () => Clipboard.setData(ClipboardData(text: _code!.code)),
                          icon: const Icon(Icons.copy_rounded),
                        ),
                      ],
                    ),
                    Text(
                      'Vence ${format.shortDate(_code!.expiresAt)} a las ${format.time(_code!.expiresAt)}. '
                      'Entrégalo en persona o por un canal verificado. No se mostrará de nuevo.',
                      style: theme.textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: Spacing.lg),
            ],
            FilledButton.icon(
              onPressed: _busy || _account.state == AccountState.inactive ? null : _issueCode,
              icon: const Icon(Icons.key_rounded),
              label: Text(
                _account.state == AccountState.invited
                    ? 'Generar código de activación'
                    : 'Generar código de recuperación',
              ),
            ),
            const SizedBox(height: Spacing.sm),
            OutlinedButton.icon(
              onPressed: _busy ? null : _toggle,
              icon: Icon(
                _account.state == AccountState.active
                    ? Icons.block_rounded
                    : Icons.check_circle_outline_rounded,
              ),
              label: Text(
                _account.state == AccountState.active ? 'Desactivar cuenta' : 'Activar cuenta',
              ),
            ),
            const SizedBox(height: Spacing.md),
            Text(
              'Cada cambio queda registrado en la auditoría del servidor.',
              textAlign: TextAlign.center,
              style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
            ),
          ],
        ),
      ),
    );
  }
}
