import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repository_providers.dart';
import '../../../domain/models/people.dart';

/// Sesión actual. En el prototipo vive en memoria; la app real guardará los
/// tokens en almacenamiento seguro (docs/reference §8.4).
class AuthController extends Notifier<UserSession?> {
  @override
  UserSession? build() => null;

  Future<void> signIn({required String username, required String password}) async {
    final session = await ref
        .read(authRepositoryProvider)
        .signIn(username: username, password: password);
    state = session;
  }

  Future<void> signOut() async {
    try {
      await ref.read(authRepositoryProvider).signOut();
    } on Object {
      // Cerrar sesión localmente aunque el servidor no responda.
    }
    state = null;
  }
}

final authControllerProvider = NotifierProvider<AuthController, UserSession?>(AuthController.new);

/// Sesión obligatoria dentro de las rutas protegidas.
final requireSessionProvider = Provider<UserSession>((ref) {
  final session = ref.watch(authControllerProvider);
  if (session == null) {
    throw StateError('No hay una sesión activa.');
  }
  return session;
});
