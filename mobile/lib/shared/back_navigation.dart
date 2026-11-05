import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Intercepta el "atrás" del sistema en pantallas raíz para que la app no se
/// cierre por accidente: primero ejecuta [onBack] (p. ej. volver a la primera
/// pestaña) y, si ya no hay a dónde volver, pide una segunda pulsación.
class ExitOnDoubleBack extends StatefulWidget {
  const ExitOnDoubleBack({required this.child, this.onBack, super.key});

  final Widget child;

  /// Devuelve `true` si consumió el gesto de regreso.
  final bool Function()? onBack;

  static const window = Duration(seconds: 2);
  static const message = 'Presiona atrás otra vez para salir';

  @override
  State<ExitOnDoubleBack> createState() => _ExitOnDoubleBackState();
}

class _ExitOnDoubleBackState extends State<ExitOnDoubleBack> {
  DateTime? _lastPress;

  void _handleBack() {
    if (widget.onBack?.call() ?? false) {
      _lastPress = null;
      return;
    }
    final messenger = ScaffoldMessenger.maybeOf(context);
    final now = DateTime.now();
    if (_lastPress != null && now.difference(_lastPress!) < ExitOnDoubleBack.window) {
      messenger?.hideCurrentSnackBar();
      // Android cierra la actividad; iOS y web ignoran esta llamada.
      unawaited(SystemNavigator.pop());
      return;
    }
    _lastPress = now;
    messenger
      ?..hideCurrentSnackBar()
      ..showSnackBar(
        const SnackBar(content: Text(ExitOnDoubleBack.message), duration: ExitOnDoubleBack.window),
      );
  }

  @override
  Widget build(BuildContext context) {
    return PopScope<Object?>(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _handleBack();
      },
      child: widget.child,
    );
  }
}

/// Pregunta antes de salir de una pantalla con cambios sin guardar
/// (botón atrás del sistema, gesto de iOS o flecha de la barra superior).
class UnsavedChangesGuard extends StatelessWidget {
  const UnsavedChangesGuard({
    required this.hasChanges,
    required this.child,
    this.message = 'Los datos que escribiste no se han guardado.',
    super.key,
  });

  final bool hasChanges;
  final Widget child;
  final String message;

  @override
  Widget build(BuildContext context) {
    return PopScope<Object?>(
      canPop: !hasChanges,
      onPopInvokedWithResult: (didPop, result) async {
        if (didPop) return;
        final navigator = Navigator.of(context);
        if (await confirmDiscardChanges(context, message: message) && navigator.mounted) {
          navigator.pop(result);
        }
      },
      child: child,
    );
  }
}

Future<bool> confirmDiscardChanges(
  BuildContext context, {
  String message = 'Los datos que escribiste no se han guardado.',
}) async {
  final discard = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: const Text('¿Descartar cambios?'),
      content: Text(message),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: const Text('Seguir editando'),
        ),
        FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Descartar')),
      ],
    ),
  );
  return discard ?? false;
}

/// Se incrementa al volver a tocar la pestaña activa; la página visible
/// responde desplazándose al inicio.
class TabReselectController extends Notifier<int> {
  @override
  int build() => 0;

  void reselect() => state++;
}

final tabReselectProvider = NotifierProvider<TabReselectController, int>(TabReselectController.new);
