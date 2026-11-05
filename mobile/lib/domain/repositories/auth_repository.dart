import '../models/people.dart';

class AuthException implements Exception {
  const AuthException(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Cuenta de demostración que el prototipo ofrece para ingresar rápido.
class DemoAccount {
  const DemoAccount({
    required this.role,
    required this.username,
    required this.displayName,
    required this.description,
  });

  final UserRole role;
  final String username;
  final String displayName;
  final String description;
}

abstract interface class AuthRepository {
  /// Valida credenciales y devuelve la sesión con su perfil de experiencia.
  Future<UserSession> signIn({required String username, required String password});

  Future<void> signOut();

  /// Canje de un código de activación/recuperación de un solo uso.
  Future<void> redeemCode({required String code, required String newPassword});

  List<DemoAccount> get demoAccounts;
}
