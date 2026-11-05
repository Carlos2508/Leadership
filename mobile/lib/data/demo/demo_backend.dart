/// Error simulado de red para demostrar estados de error y reintento.
class DemoNetworkException implements Exception {
  const DemoNetworkException();

  @override
  String toString() =>
      'No pudimos conectar con el servidor. Revisa tu conexión e intenta de nuevo.';
}

/// Simula la latencia y fallos de un backend real sobre la base en memoria.
class DemoBackend {
  DemoBackend({required this.latency, required this._simulateFailures});

  final Duration latency;
  final bool Function() _simulateFailures;

  Future<T> call<T>(T Function() action) async {
    if (latency > Duration.zero) {
      await Future<void>.delayed(latency);
    }
    if (_simulateFailures()) {
      throw const DemoNetworkException();
    }
    return action();
  }
}
