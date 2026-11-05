import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'states.dart';

/// Renderiza un [AsyncValue] con estados de carga, error y datos coherentes.
///
/// Mientras se recarga conserva los datos previos para evitar parpadeos.
class AsyncValueView<T> extends StatelessWidget {
  const AsyncValueView({
    required this.value,
    required this.data,
    this.onRetry,
    this.loading,
    super.key,
  });

  final AsyncValue<T> value;
  final Widget Function(T data) data;
  final VoidCallback? onRetry;
  final Widget? loading;

  @override
  Widget build(BuildContext context) {
    if (value.hasValue) {
      return data(value.requireValue);
    }
    if (value.hasError) {
      return ErrorState(message: friendlyError(value.error!), onRetry: onRetry);
    }
    return loading ?? const LoadingState();
  }
}

/// Mensaje legible para errores esperados del prototipo.
String friendlyError(Object error) {
  final text = switch (error) {
    ArgumentError(:final message) => '$message',
    StateError(:final message) => message,
    _ => error.toString(),
  };
  return text.isEmpty ? 'Ocurrió un error inesperado.' : text;
}
