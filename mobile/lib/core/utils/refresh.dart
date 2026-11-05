/// Espera varias recargas para el gesto "deslizar para actualizar".
///
/// Los errores no se propagan: cada sección ya muestra su estado de error con
/// opción de reintento.
Future<void> safeRefresh(Iterable<Future<Object?>> futures) async {
  try {
    await Future.wait(futures);
  } on Object {
    // Manejado por la interfaz de cada sección.
  }
}
