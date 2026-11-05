import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../data/repository_providers.dart';
import '../../../domain/repositories/auth_repository.dart';
import '../../../shared/back_navigation.dart';
import '../../../shared/page_layout.dart';

/// Activación o recuperación asistida con un código de un solo uso entregado
/// por secretaría mediante un canal verificado (docs/IDENTITY_AND_ACCESS.md).
class RecoveryPage extends ConsumerStatefulWidget {
  const RecoveryPage({super.key});

  @override
  ConsumerState<RecoveryPage> createState() => _RecoveryPageState();
}

class _RecoveryPageState extends ConsumerState<RecoveryPage> {
  final _formKey = GlobalKey<FormState>();
  final _code = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _code.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_submitting || !(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await ref
          .read(authRepositoryProvider)
          .redeemCode(code: _code.text, newPassword: _password.text);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Contraseña actualizada. Ya puedes iniciar sesión.')),
      );
      context.go('/login');
    } on AuthException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } on Object catch (error) {
      if (mounted) setState(() => _error = friendlyError(error));
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return UnsavedChangesGuard(
      hasChanges: !_submitting && (_code.text.isNotEmpty || _password.text.isNotEmpty),
      child: DetailPage(
        title: 'Recuperar acceso',
        children: [
          const PageIntro(
            title: 'Activa o recupera tu cuenta',
            subtitle: 'Por seguridad, la recuperación es asistida: secretaría verifica tu identidad y te entrega un código de un solo uso.',
          ),
          const AppCard(
            tone: AppTone.brand,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _Step(number: 1, text: 'Acércate a secretaría con tu identificación.'),
                _Step(
                  number: 2,
                  text:
                      'Recibe un código válido por 48 horas, en persona o por un canal verificado.',
                ),
                _Step(number: 3, text: 'Escríbelo aquí y crea una contraseña nueva.'),
              ],
            ),
          ),
          const SizedBox(height: Spacing.xl),
          Form(
            key: _formKey,
            onChanged: () => setState(() {}),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextFormField(
                  controller: _code,
                  enabled: !_submitting,
                  textCapitalization: TextCapitalization.characters,
                  autocorrect: false,
                  inputFormatters: [
                    FilteringTextInputFormatter.allow(RegExp('[A-Za-z0-9-]')),
                    LengthLimitingTextInputFormatter(9),
                  ],
                  decoration: const InputDecoration(
                    labelText: 'Código de un solo uso',
                    hintText: 'XXXX-XXXX',
                    prefixIcon: Icon(Icons.key_rounded),
                  ),
                  validator: (value) =>
                      RegExp(r'^[A-Za-z0-9]{4}-[A-Za-z0-9]{4}$').hasMatch(value?.trim() ?? '')
                      ? null
                      : 'El código tiene el formato XXXX-XXXX.',
                ),
                const SizedBox(height: Spacing.lg),
                TextFormField(
                  controller: _password,
                  enabled: !_submitting,
                  obscureText: true,
                  autofillHints: const [AutofillHints.newPassword],
                  decoration: const InputDecoration(
                    labelText: 'Contraseña nueva',
                    helperText: 'Mínimo 8 caracteres.',
                    prefixIcon: Icon(Icons.lock_outline_rounded),
                  ),
                  validator: (value) =>
                      (value ?? '').length < 8 ? 'Usa al menos 8 caracteres.' : null,
                ),
                const SizedBox(height: Spacing.lg),
                TextFormField(
                  controller: _confirm,
                  enabled: !_submitting,
                  obscureText: true,
                  decoration: const InputDecoration(
                    labelText: 'Confirma la contraseña',
                    prefixIcon: Icon(Icons.lock_outline_rounded),
                  ),
                  onFieldSubmitted: (_) => _submit(),
                  validator: (value) =>
                      value != _password.text ? 'Las contraseñas no coinciden.' : null,
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
                const SizedBox(height: Spacing.xl),
                FilledButton(
                  onPressed: _submitting ? null : _submit,
                  child: _submitting
                      ? const SizedBox.square(
                          dimension: 22,
                          child: CircularProgressIndicator(strokeWidth: 2.5),
                        )
                      : const Text('Guardar contraseña'),
                ),
                const SizedBox(height: Spacing.md),
                Text(
                  'En el prototipo, los códigos se generan desde Administración → Cuentas.',
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Step extends StatelessWidget {
  const _Step({required this.number, required this.text});

  final int number;
  final String text;

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    final pair = colors.tone(AppTone.brand);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 26,
            height: 26,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: pair.foreground, shape: BoxShape.circle),
            child: Text(
              '$number',
              style: TextStyle(color: pair.background, fontWeight: FontWeight.w800, fontSize: 13),
            ),
          ),
          const SizedBox(width: Spacing.md),
          Expanded(
            child: Text(
              text,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: pair.foreground),
            ),
          ),
        ],
      ),
    );
  }
}
