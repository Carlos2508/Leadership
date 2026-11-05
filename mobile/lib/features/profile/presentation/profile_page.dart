import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/institution_logo.dart';
import '../../../data/repository_providers.dart';
import '../../../shared/page_layout.dart';
import '../../auth/application/auth_controller.dart';

class ProfilePage extends ConsumerWidget {
  const ProfilePage({super.key});

  Future<void> _signOut(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('¿Cerrar sesión?'),
        content: const Text('Tendrás que ingresar de nuevo para ver tu información.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancelar')),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Cerrar sesión'),
          ),
        ],
      ),
    );
    if (confirmed == true) {
      ref.read(simulateFailuresProvider.notifier).toggle(false);
      await ref.read(authControllerProvider.notifier).signOut();
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(authControllerProvider);
    final institution = ref.watch(institutionProvider);
    final themeMode = ref.watch(themeModeProvider);
    final simulateFailures = ref.watch(simulateFailuresProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    if (session == null) return const Scaffold(body: SizedBox.shrink());

    return DetailPage(
      title: 'Mi perfil',
      children: [
        Center(child: InitialsAvatar(initials: session.initials, size: 84)),
        const SizedBox(height: Spacing.md),
        Text(
          session.displayName,
          textAlign: TextAlign.center,
          style: theme.textTheme.headlineSmall,
        ),
        Text(
          session.roleTitle,
          textAlign: TextAlign.center,
          style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
        ),
        const SizedBox(height: Spacing.sm),
        Center(
          child: StatusPill(label: 'Perfil: ${session.role.label}', tone: AppTone.brand),
        ),
        const SectionHeader(title: 'Apariencia'),
        SegmentedButton<ThemeMode>(
          showSelectedIcon: false,
          segments: const [
            ButtonSegment(
              value: ThemeMode.system,
              label: Text('Sistema'),
              icon: Icon(Icons.brightness_auto_rounded),
            ),
            ButtonSegment(
              value: ThemeMode.light,
              label: Text('Claro'),
              icon: Icon(Icons.light_mode_rounded),
            ),
            ButtonSegment(
              value: ThemeMode.dark,
              label: Text('Oscuro'),
              icon: Icon(Icons.dark_mode_rounded),
            ),
          ],
          selected: {themeMode},
          onSelectionChanged: (value) => ref.read(themeModeProvider.notifier).setMode(value.first),
        ),
        const SectionHeader(title: 'Mi colegio'),
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  InstitutionLogo(path: institution.mainLogo, size: 48),
                  const SizedBox(width: Spacing.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(institution.displayName, style: theme.textTheme.titleSmall),
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
              if (!institution.contact.isEmpty) ...[
                const SizedBox(height: Spacing.md),
                if (institution.contact.phone.isNotEmpty)
                  InfoRow(
                    icon: Icons.phone_rounded,
                    label: 'Teléfono',
                    value: institution.contact.phone,
                  ),
                if (institution.contact.email.isNotEmpty)
                  InfoRow(
                    icon: Icons.mail_rounded,
                    label: 'Correo',
                    value: institution.contact.email,
                  ),
                if (institution.contact.address.isNotEmpty)
                  InfoRow(
                    icon: Icons.place_rounded,
                    label: 'Dirección',
                    value: institution.contact.address,
                  ),
                if (institution.contact.website.isNotEmpty)
                  InfoRow(
                    icon: Icons.language_rounded,
                    label: 'Sitio web',
                    value: institution.contact.website,
                  ),
              ],
            ],
          ),
        ),
        const SectionHeader(title: 'Opciones del prototipo'),
        AppCard(
          padding: const EdgeInsets.symmetric(vertical: Spacing.xs),
          child: Column(
            children: [
              SwitchListTile(
                value: simulateFailures,
                onChanged: (value) => ref.read(simulateFailuresProvider.notifier).toggle(value),
                title: const Text('Simular errores de conexión'),
                subtitle: const Text(
                  'Muestra cómo responde la app cuando el servidor no está disponible.',
                ),
                secondary: const Icon(Icons.wifi_off_rounded),
              ),
              const ListTile(
                leading: Icon(Icons.science_outlined),
                title: Text('Datos ficticios'),
                subtitle: Text(
                  'Nombres, notas y montos son sintéticos. Los cambios se pierden al cerrar la app.',
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: Spacing.xl),
        OutlinedButton.icon(
          onPressed: () => _signOut(context, ref),
          icon: const Icon(Icons.logout_rounded),
          label: const Text('Cerrar sesión'),
        ),
        const SizedBox(height: Spacing.lg),
        Text(
          'Prototipo móvil · versión 0.1.0',
          textAlign: TextAlign.center,
          style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
        ),
      ],
    );
  }
}
