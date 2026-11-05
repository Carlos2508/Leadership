import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/institution_logo.dart';
import '../../../data/repository_providers.dart';
import '../../../domain/models/people.dart';
import '../../../domain/repositories/auth_repository.dart';
import '../application/auth_controller.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  static const _demoPassword = 'demo1234';

  final _formKey = GlobalKey<FormState>();
  final _username = TextEditingController();
  final _password = TextEditingController();
  final _passwordFocus = FocusNode();
  bool _obscure = true;
  bool _submitting = false;
  String? _error;
  UserRole? _selectedRole;

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    _passwordFocus.dispose();
    super.dispose();
  }

  void _selectDemo(DemoAccount account) {
    setState(() {
      _selectedRole = account.role;
      _username.text = account.username;
      _password.text = _demoPassword;
      _error = null;
    });
  }

  Future<void> _submit() async {
    if (_submitting) return;
    FocusScope.of(context).unfocus();
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await ref
          .read(authControllerProvider.notifier)
          .signIn(username: _username.text, password: _password.text);
      TextInput.finishAutofillContext();
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
    final institution = ref.watch(institutionProvider);
    final width = MediaQuery.sizeOf(context).width;
    final form = _buildForm(context);

    if (width < 900) {
      return Scaffold(
        body: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(Spacing.page, Spacing.lg, Spacing.page, Spacing.xxl),
            child: Center(
              child: ConstrainedBox(constraints: const BoxConstraints(maxWidth: 460), child: form),
            ),
          ),
        ),
      );
    }

    final colors = AppColors.of(context);
    final theme = Theme.of(context);
    return Scaffold(
      body: Row(
        children: [
          Expanded(
            child: DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: [colors.heroStart, colors.heroEnd],
                ),
              ),
              child: Padding(
                padding: const EdgeInsets.all(56),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        InstitutionLogo(path: institution.mainLogo, size: 56),
                        const SizedBox(width: Spacing.md),
                        Expanded(
                          child: Text(
                            institution.displayName,
                            style: theme.textTheme.titleMedium?.copyWith(color: Colors.white),
                          ),
                        ),
                      ],
                    ),
                    const Spacer(),
                    Text(
                      institution.tagline.isEmpty ? institution.displayName : institution.tagline,
                      style: theme.textTheme.headlineLarge?.copyWith(
                        color: Colors.white,
                        fontSize: 44,
                      ),
                    ),
                    if (institution.welcomeMessage.isNotEmpty) ...[
                      const SizedBox(height: Spacing.lg),
                      Text(
                        institution.welcomeMessage,
                        style: theme.textTheme.titleMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.85),
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                    const Spacer(),
                  ],
                ),
              ),
            ),
          ),
          Expanded(
            child: SafeArea(
              child: Center(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(Spacing.xxl),
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 440),
                    child: form,
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildForm(BuildContext context) {
    final institution = ref.watch(institutionProvider);
    final accounts = ref.watch(authRepositoryProvider).demoAccounts;
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return AutofillGroup(
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                InstitutionLogo(path: institution.loginLogo, size: 48),
                const SizedBox(width: Spacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(institution.shortName, style: theme.textTheme.titleMedium),
                      if (institution.campusLabel.isNotEmpty)
                        Text(
                          institution.campusLabel,
                          style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                        ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: Spacing.xxl),
            Text(
              'TU COLEGIO, CONTIGO',
              style: theme.textTheme.labelSmall?.copyWith(color: colors.muted, letterSpacing: 1.1),
            ),
            const SizedBox(height: 6),
            Semantics(
              header: true,
              child: Text('¡Qué gusto verte!', style: theme.textTheme.headlineMedium),
            ),
            const SizedBox(height: 6),
            Text(
              'Ingresa a tu espacio escolar.',
              style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
            ),
            const SizedBox(height: Spacing.xl),
            _DemoNotice(colors: colors),
            const SizedBox(height: Spacing.lg),
            Text('Perfil de demostración', style: theme.textTheme.labelLarge),
            const SizedBox(height: Spacing.sm),
            Wrap(
              spacing: Spacing.sm,
              runSpacing: Spacing.sm,
              children: [
                for (final account in accounts)
                  ChoiceChip(
                    label: Text(account.role.label),
                    avatar: Icon(_roleIcon(account.role), size: 18),
                    selected: _selectedRole == account.role,
                    tooltip: account.description,
                    onSelected: _submitting ? null : (_) => _selectDemo(account),
                  ),
              ],
            ),
            const SizedBox(height: Spacing.xl),
            TextFormField(
              controller: _username,
              enabled: !_submitting,
              autofillHints: const [AutofillHints.username],
              textInputAction: TextInputAction.next,
              keyboardType: TextInputType.emailAddress,
              autocorrect: false,
              decoration: const InputDecoration(
                labelText: 'Usuario o correo electrónico',
                prefixIcon: Icon(Icons.person_outline_rounded),
              ),
              onFieldSubmitted: (_) => _passwordFocus.requestFocus(),
              validator: (value) =>
                  (value == null || value.trim().isEmpty) ? 'Escribe tu usuario.' : null,
            ),
            const SizedBox(height: Spacing.lg),
            TextFormField(
              controller: _password,
              focusNode: _passwordFocus,
              enabled: !_submitting,
              obscureText: _obscure,
              autofillHints: const [AutofillHints.password],
              textInputAction: TextInputAction.done,
              decoration: InputDecoration(
                labelText: 'Contraseña',
                prefixIcon: const Icon(Icons.lock_outline_rounded),
                suffixIcon: IconButton(
                  tooltip: _obscure ? 'Mostrar contraseña' : 'Ocultar contraseña',
                  onPressed: () => setState(() => _obscure = !_obscure),
                  icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                ),
              ),
              onFieldSubmitted: (_) => _submit(),
              validator: (value) =>
                  (value == null || value.isEmpty) ? 'Escribe tu contraseña.' : null,
            ),
            AnimatedSize(
              duration: const Duration(milliseconds: 200),
              child: _error == null
                  ? const SizedBox(height: Spacing.xl)
                  : Padding(
                      padding: const EdgeInsets.symmetric(vertical: Spacing.lg),
                      child: Semantics(
                        liveRegion: true,
                        child: Container(
                          padding: const EdgeInsets.all(Spacing.md),
                          decoration: BoxDecoration(
                            color: colors.tone(AppTone.danger).background,
                            borderRadius: BorderRadius.circular(Radii.sm),
                          ),
                          child: Row(
                            children: [
                              Icon(
                                Icons.error_outline_rounded,
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
            ),
            FilledButton(
              onPressed: _submitting ? null : _submit,
              child: _submitting
                  ? const SizedBox.square(
                      dimension: 22,
                      child: CircularProgressIndicator(strokeWidth: 2.5),
                    )
                  : const Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Text('Iniciar sesión'),
                        SizedBox(width: Spacing.sm),
                        Icon(Icons.arrow_forward_rounded, size: 20),
                      ],
                    ),
            ),
            const SizedBox(height: Spacing.md),
            TextButton(
              onPressed: _submitting ? null : () => context.push('/recover'),
              child: const Text('¿Olvidaste tu contraseña o tienes un código?'),
            ),
            const SizedBox(height: Spacing.md),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(Icons.lock_rounded, size: 14, color: colors.muted),
                const SizedBox(width: 6),
                Flexible(
                  child: Text(
                    'Acceso privado y seguro',
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  static IconData _roleIcon(UserRole role) => switch (role) {
    UserRole.student => Icons.backpack_rounded,
    UserRole.guardian => Icons.family_restroom_rounded,
    UserRole.teacher => Icons.co_present_rounded,
    UserRole.admin => Icons.admin_panel_settings_rounded,
    UserRole.finance => Icons.point_of_sale_rounded,
  };
}

class _DemoNotice extends StatelessWidget {
  const _DemoNotice({required this.colors});

  final AppColors colors;

  @override
  Widget build(BuildContext context) {
    final pair = colors.tone(AppTone.accent);
    return Container(
      padding: const EdgeInsets.all(Spacing.md),
      decoration: BoxDecoration(
        color: pair.background,
        borderRadius: BorderRadius.circular(Radii.md),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.science_outlined, color: pair.foreground, size: 20),
          const SizedBox(width: Spacing.sm),
          Expanded(
            child: Text(
              'Prototipo con datos ficticios. Elige un perfil para completar el acceso '
              'o usa cualquier contraseña de 6 caracteres o más.',
              style: Theme.of(context).textTheme.bodySmall
                  ?.copyWith(color: pair.foreground, fontWeight: FontWeight.w600, height: 1.4),
            ),
          ),
        ],
      ),
    );
  }
}
