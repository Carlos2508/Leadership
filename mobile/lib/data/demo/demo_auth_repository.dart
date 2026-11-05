import '../../domain/models/administration.dart';
import '../../domain/models/people.dart';
import '../../domain/repositories/auth_repository.dart';
import 'demo_backend.dart';
import 'demo_database.dart';

class DemoAuthRepository implements AuthRepository {
  DemoAuthRepository(this._db, this._backend);

  final DemoDatabase _db;
  final DemoBackend _backend;

  static const _minPasswordLength = 6;

  static final _sessions = <String, UserSession>{
    'estudiante.demo': const UserSession(
      accountId: 'acc-07',
      displayName: 'Sofía Ramírez',
      firstName: 'Sofía',
      role: UserRole.student,
      roleTitle: 'Estudiante · 1.º Básico A',
      studentId: DemoDatabase.studentId,
    ),
    'familia.demo': const UserSession(
      accountId: 'acc-06',
      displayName: DemoDatabase.guardianName,
      firstName: 'Marta',
      role: UserRole.guardian,
      roleTitle: 'Encargada',
      linkedStudentIds: [DemoDatabase.studentId, DemoDatabase.siblingId],
    ),
    'docente.demo': const UserSession(
      accountId: 'acc-03',
      displayName: 'Andrea Morales',
      firstName: 'Andrea',
      role: UserRole.teacher,
      roleTitle: 'Docente de Matemáticas',
      teacherId: DemoDatabase.teacherId,
    ),
    'direccion.demo': const UserSession(
      accountId: 'acc-01',
      displayName: 'Roberto Castillo',
      firstName: 'Roberto',
      role: UserRole.admin,
      roleTitle: 'Dirección',
    ),
    'caja.demo': const UserSession(
      accountId: 'acc-02',
      displayName: 'Ana Lucía Pérez',
      firstName: 'Ana Lucía',
      role: UserRole.finance,
      roleTitle: 'Caja',
    ),
  };

  @override
  List<DemoAccount> get demoAccounts => const [
    DemoAccount(
      role: UserRole.student,
      username: 'estudiante.demo',
      displayName: 'Sofía Ramírez',
      description: 'Tareas, cursos, agenda y notas',
    ),
    DemoAccount(
      role: UserRole.guardian,
      username: 'familia.demo',
      displayName: DemoDatabase.guardianName,
      description: 'Dos hijos, pagos y solvencia',
    ),
    DemoAccount(
      role: UserRole.teacher,
      username: 'docente.demo',
      displayName: 'Andrea Morales',
      description: 'Asistencia, libreta y revisiones',
    ),
    DemoAccount(
      role: UserRole.admin,
      username: 'direccion.demo',
      displayName: 'Roberto Castillo',
      description: 'Indicadores, matrícula y cuentas',
    ),
    DemoAccount(
      role: UserRole.finance,
      username: 'caja.demo',
      displayName: 'Ana Lucía Pérez',
      description: 'Cobros, recibos y comprobantes',
    ),
  ];

  @override
  Future<UserSession> signIn({required String username, required String password}) {
    return _backend.call(() {
      final session = _sessions[username.trim().toLowerCase()];
      // Mensaje genérico: no revela si el usuario existe.
      if (session == null || password.length < _minPasswordLength) {
        throw const AuthException('Usuario o contraseña incorrectos.');
      }
      final account = _db.accounts.where((a) => a.id == session.accountId).firstOrNull;
      if (account != null && account.state != AccountState.active) {
        throw const AuthException('Esta cuenta no está activa. Comunícate con secretaría.');
      }
      return session;
    });
  }

  @override
  Future<void> signOut() => _backend.call(() {});

  @override
  Future<void> redeemCode({required String code, required String newPassword}) {
    return _backend.call(() {
      final normalized = code.trim().toUpperCase();
      final expires = _db.activationCodes[normalized];
      if (expires == null || expires.isBefore(_db.now)) {
        throw const AuthException('El código no es válido o ya venció.');
      }
      if (newPassword.length < 8) {
        throw const AuthException('La contraseña debe tener al menos 8 caracteres.');
      }
      _db.activationCodes.remove(normalized); // un solo uso
    });
  }
}
