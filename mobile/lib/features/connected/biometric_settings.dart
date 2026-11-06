import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'biometric_vault.dart';
import 'connected_session.dart';

class BiometricSettings extends ConsumerStatefulWidget {
  const BiometricSettings({super.key});
  @override
  ConsumerState<BiometricSettings> createState() => _BiometricSettingsState();
}

class _BiometricSettingsState extends ConsumerState<BiometricSettings> {
  bool _busy = false;
  String? _error;
  @override
  Widget build(BuildContext context) {
    final available = ref.watch(biometricAvailableProvider).value ?? false;
    final enabled = ref.watch(biometricEnabledProvider);
    if (!available && !enabled) return const SizedBox.shrink();
    return Column(
      children: [
        SwitchListTile.adaptive(
          contentPadding: EdgeInsets.zero,
          secondary: const Icon(Icons.fingerprint),
          title: const Text('Desbloquear con huella'),
          subtitle: const Text(
            'Usa la huella o Face ID del teléfono. Tu contraseña no se guarda. Al salir, el app se bloquea.',
          ),
          value: enabled,
          onChanged: _busy
              ? null
              : (value) async {
                  setState(() {
                    _busy = true;
                    _error = null;
                  });
                  try {
                    final controller = ref.read(connectedControllerProvider.notifier);
                    if (value) {
                      await controller.enableBiometrics();
                    } else {
                      await controller.disableBiometrics();
                    }
                  } on Object {
                    if (mounted) {
                      setState(
                        () => _error =
                            'No se pudo cambiar. Confirma tu biometría o inténtalo otra vez.',
                      );
                    }
                  } finally {
                    if (mounted) setState(() => _busy = false);
                  }
                },
        ),
        if (_busy) const LinearProgressIndicator(),
        if (_error != null)
          Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
      ],
    );
  }
}
