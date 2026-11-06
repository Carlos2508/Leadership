import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'biometric_vault.dart';
import 'connected_session.dart';

/// Masks all connected routes (including pushed details) as soon as the app loses foreground focus.
class BiometricLock extends ConsumerStatefulWidget {
  const BiometricLock({required this.child, this.onPassword, super.key});
  final Widget child;
  final VoidCallback? onPassword;
  @override
  ConsumerState<BiometricLock> createState() => _BiometricLockState();
}

class _BiometricLockState extends ConsumerState<BiometricLock> with WidgetsBindingObserver {
  bool _busy = false;
  String? _error;
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state != AppLifecycleState.resumed &&
        ref.read(biometricEnabledProvider) &&
        ref.read(connectedControllerProvider) != null) {
      ref.read(biometricLockedProvider.notifier).setLocked(true);
    }
  }

  Future<void> _unlock() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(connectedControllerProvider.notifier).unlockBiometrics();
    } on Object {
      if (mounted) {
        setState(() => _error = 'No se desbloqueó. Intenta de nuevo o ingresa con tu contraseña.');
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final locked =
        ref.watch(biometricLockedProvider) && ref.watch(connectedControllerProvider) != null;
    return Stack(
      children: [
        Offstage(offstage: locked, child: widget.child),
        if (locked)
          Positioned.fill(
            child: Material(
              child: SafeArea(
                child: Center(
                  child: Padding(
                    padding: const EdgeInsets.all(28),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Icons.fingerprint, size: 64),
                        const SizedBox(height: 20),
                        Text(
                          'Tu espacio está bloqueado',
                          style: Theme.of(context).textTheme.headlineSmall,
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 12),
                        const Text(
                          'Confirma tu huella o biometría para continuar.',
                          textAlign: TextAlign.center,
                        ),
                        if (_error != null)
                          Padding(
                            padding: const EdgeInsets.all(12),
                            child: Text(_error!, textAlign: TextAlign.center),
                          ),
                        const SizedBox(height: 20),
                        FilledButton.icon(
                          onPressed: _busy ? null : _unlock,
                          icon: const Icon(Icons.fingerprint),
                          label: Text(_busy ? 'Desbloqueando…' : 'Desbloquear'),
                        ),
                        TextButton(
                          onPressed: _busy
                              ? null
                              : () async {
                                  final operation = ref
                                      .read(connectedControllerProvider.notifier)
                                      .signOut();
                                  widget.onPassword?.call();
                                  await operation;
                                },
                          child: const Text('Ingresar con contraseña'),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
      ],
    );
  }
}
