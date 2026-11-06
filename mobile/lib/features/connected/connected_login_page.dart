import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/institution_logo.dart';
import 'biometric_vault.dart';
import 'connected_session.dart';
import 'school_api.dart';

/// Sign-in with a real school account (ADR-014). Same accounts, permissions and two-step verification as the web.
class ConnectedLoginPage extends ConsumerStatefulWidget {
  const ConnectedLoginPage({super.key});

  @override
  ConsumerState<ConnectedLoginPage> createState() => _ConnectedLoginPageState();
}

class _ConnectedLoginPageState extends ConsumerState<ConnectedLoginPage> {
  final _form = GlobalKey<FormState>();
  late final _server = TextEditingController(text: ref.read(savedServerProvider));
  final _user = TextEditingController();
  final _password = TextEditingController();
  final _code = TextEditingController();
  bool _busy = false;
  bool _secondFactor = false;
  bool _recovery = false;
  bool _obscure = true;
  String? _error;

  Future<void> _unlock() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(connectedControllerProvider.notifier).unlockBiometrics();
      if (mounted) context.go('/c');
    } on Object {
      if (mounted) {
        setState(() => _error = 'No se desbloqueó. Intenta de nuevo o ingresa con tu contraseña.');
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  void dispose() {
    _server.dispose();
    _user.dispose();
    _password.dispose();
    _code.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_form.currentState?.validate() ?? false)) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    final controller = ref.read(connectedControllerProvider.notifier);
    try {
      if (_secondFactor) {
        await controller.completeSecondFactor(_code.text, recovery: _recovery);
      } else {
        final step = await controller.signIn(
          server: _server.text,
          username: _user.text,
          password: _password.text,
        );
        if (step == LoginStep.secondFactor) {
          setState(() {
            _secondFactor = true;
            _busy = false;
          });
          return;
        }
      }
      _password.clear();
      _code.clear();
      if (mounted) context.go('/c');
    } on ApiFailure catch (e) {
      if (e.code == 'second-factor-expired') _secondFactor = false;
      setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final institution = ref.watch(institutionProvider);
    return Scaffold(
      appBar: AppBar(
        leading: BackButton(
          onPressed: () {
            if (_secondFactor) {
              ref.read(connectedControllerProvider.notifier).cancelSecondFactor();
              setState(() => _secondFactor = false);
            } else {
              context.go('/login');
            }
          },
        ),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(Spacing.page, 0, Spacing.page, Spacing.xl),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 460),
              child: Form(
                key: _form,
                child: AutofillGroup(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          InstitutionLogo(path: institution.mainLogo),
                          const SizedBox(width: Spacing.md),
                          Expanded(
                            child: Text(institution.shortName, style: theme.textTheme.titleMedium),
                          ),
                        ],
                      ),
                      const SizedBox(height: Spacing.xl),
                      Text(
                        _secondFactor ? 'VERIFICACIÓN EN DOS PASOS' : 'CUENTA DEL COLEGIO',
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: colors.muted,
                          letterSpacing: 1.1,
                        ),
                      ),
                      const SizedBox(height: 6),
                      Semantics(
                        header: true,
                        child: Text(
                          _secondFactor ? 'Confirma que eres tú' : 'Ingresa a tu espacio',
                          style: theme.textTheme.headlineMedium?.copyWith(fontSize: 28),
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        _secondFactor
                            ? (_recovery
                                  ? 'Escribe uno de tus códigos de recuperación.'
                                  : 'Escribe el código de 6 dígitos de tu app autenticadora.')
                            : 'Usa el mismo usuario y contraseña de la plataforma web.',
                        style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
                      ),
                      const SizedBox(height: Spacing.xl),
                      if (!_secondFactor) ...[
                        if (ref.watch(biometricEnabledProvider)) ...[
                          FilledButton.icon(
                            onPressed: _busy ? null : _unlock,
                            icon: const Icon(Icons.fingerprint),
                            label: const Text('Desbloquear con huella'),
                          ),
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 14),
                            child: Text('O ingresa con tu contraseña', textAlign: TextAlign.center),
                          ),
                        ],
                        TextFormField(
                          controller: _server,
                          enabled: !_busy,
                          keyboardType: TextInputType.url,
                          autocorrect: false,
                          decoration: const InputDecoration(
                            labelText: 'Dirección del colegio',
                            hintText: 'https://colegio.ejemplo',
                            prefixIcon: Icon(Icons.dns_outlined),
                          ),
                          validator: (v) =>
                              (v ?? '').trim().isEmpty ? 'Escribe la dirección del colegio.' : null,
                        ),
                        const SizedBox(height: Spacing.md),
                        TextFormField(
                          controller: _user,
                          enabled: !_busy,
                          autocorrect: false,
                          autofillHints: const [AutofillHints.username],
                          textInputAction: TextInputAction.next,
                          decoration: const InputDecoration(
                            labelText: 'Usuario',
                            prefixIcon: Icon(Icons.person_outline),
                          ),
                          validator: (v) => (v ?? '').trim().isEmpty ? 'Ingresa tu usuario.' : null,
                        ),
                        const SizedBox(height: Spacing.md),
                        TextFormField(
                          controller: _password,
                          enabled: !_busy,
                          obscureText: _obscure,
                          autofillHints: const [AutofillHints.password],
                          textInputAction: TextInputAction.done,
                          onFieldSubmitted: (_) => _submit(),
                          decoration: InputDecoration(
                            labelText: 'Contraseña',
                            prefixIcon: const Icon(Icons.lock_outline),
                            suffixIcon: IconButton(
                              tooltip: _obscure ? 'Mostrar contraseña' : 'Ocultar contraseña',
                              icon: Icon(
                                _obscure
                                    ? Icons.visibility_outlined
                                    : Icons.visibility_off_outlined,
                              ),
                              onPressed: () => setState(() => _obscure = !_obscure),
                            ),
                          ),
                          validator: (v) => (v ?? '').isEmpty ? 'Ingresa tu contraseña.' : null,
                        ),
                      ] else ...[
                        TextFormField(
                          controller: _code,
                          enabled: !_busy,
                          autofocus: true,
                          autocorrect: false,
                          keyboardType: _recovery ? TextInputType.text : TextInputType.number,
                          autofillHints: const [AutofillHints.oneTimeCode],
                          onFieldSubmitted: (_) => _submit(),
                          decoration: InputDecoration(
                            labelText: _recovery ? 'Código de recuperación' : 'Código',
                            prefixIcon: const Icon(Icons.verified_user_outlined),
                          ),
                          validator: (v) => (v ?? '').trim().isEmpty ? 'Escribe el código.' : null,
                        ),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: TextButton(
                            onPressed: _busy ? null : () => setState(() => _recovery = !_recovery),
                            child: Text(
                              _recovery
                                  ? 'Usar la app autenticadora'
                                  : 'Usar un código de recuperación',
                            ),
                          ),
                        ),
                      ],
                      if (_error != null) ...[
                        const SizedBox(height: Spacing.md),
                        Semantics(
                          liveRegion: true,
                          child: DecoratedBox(
                            decoration: BoxDecoration(
                              color: colors.tone(AppTone.danger).background,
                              borderRadius: BorderRadius.circular(14),
                            ),
                            child: Padding(
                              padding: const EdgeInsets.all(Spacing.md),
                              child: Row(
                                children: [
                                  Icon(
                                    Icons.error_outline,
                                    color: colors.tone(AppTone.danger).foreground,
                                  ),
                                  const SizedBox(width: Spacing.sm),
                                  Expanded(
                                    child: Text(
                                      _error!,
                                      style: theme.textTheme.bodyMedium?.copyWith(
                                        color: colors.tone(AppTone.danger).foreground,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ],
                      const SizedBox(height: Spacing.lg),
                      FilledButton.icon(
                        onPressed: _busy ? null : _submit,
                        iconAlignment: IconAlignment.end,
                        icon: _busy
                            ? const SizedBox.square(
                                dimension: 18,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.arrow_forward),
                        label: Text(_secondFactor ? 'Verificar' : 'Ingresar'),
                      ),
                      const SizedBox(height: Spacing.lg),
                      Text(
                        'Tu contraseña no se guarda. Puedes activar el desbloqueo con huella desde tu perfil en el app del teléfono.',
                        textAlign: TextAlign.center,
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
