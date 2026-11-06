import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:http/http.dart' as http;

import '../../core/providers.dart';
import 'biometric_vault.dart';
import 'school_api.dart';

/// Linked student as returned by `/api/v1/me` (guardians) or the account's own student record.
class LinkedStudent {
  const LinkedStudent({
    required this.id,
    required this.name,
    this.code,
    this.placement,
    this.faculties = const [],
    this.isSelf = false,
  });

  final String id;
  final String name;
  final String? code;
  final String? placement;
  final List<String> faculties;
  final bool isSelf;

  /// Same rule as the server: grades/tasks need the academic faculty (or being the student), money the finance one.
  bool get canAcademic => isSelf || faculties.contains('AcademicRecords');
  bool get canFinance => !isSelf && faculties.contains('Finance');
}

class TeachingCourse {
  const TeachingCourse({
    required this.id,
    required this.subject,
    required this.grade,
    this.section,
    this.students = 0,
  });

  final String id;
  final String subject;
  final String grade;
  final String? section;
  final int students;

  String get groupLabel => section == null || section!.isEmpty ? grade : '$grade · $section';
}

/// Signed-in context from `/api/v1/session`, `/api/v1/me` and `/api/v1/me/courses`. Presentation only:
/// every request is still authorized by the server.
class ConnectedSession {
  const ConnectedSession({
    required this.api,
    required this.username,
    required this.profile,
    required this.permissions,
    required this.students,
    required this.courses,
    this.displayName,
    this.mfaPending = const [],
  });

  final SchoolApi api;
  final String username;
  final String? displayName;
  final String profile;
  final Set<String> permissions;
  final List<String> mfaPending;
  final List<LinkedStudent> students;
  final List<TeachingCourse> courses;

  String get name => displayName ?? username;
  String get firstName => name.split(' ').first;
  String get initials {
    final parts = name.split(RegExp(r'[\s.\-_]+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    return (parts.first[0] + (parts.length > 1 ? parts[1][0] : '')).toUpperCase();
  }

  bool can(String permission) => permissions.contains(permission);
  bool get isStudent => students.length == 1 && students.first.isSelf;
  bool get hasSchoolModules => can('people.read') || can('finance.read') || can('academic.read');
}

const _serverKey = 'connected.server';

/// Last server address used (not secret). The opt-in biometric ticket lives only in the protected vault.
final savedServerProvider = Provider<String>(
  (ref) => ref.watch(sharedPreferencesProvider).getString(_serverKey) ?? '',
);

/// Injectable HTTP client (tests replace it with a mock).
final connectedHttpClientProvider = Provider<http.Client?>((ref) => null);

class ConnectedController extends Notifier<ConnectedSession?> {
  SchoolApi? _pending;

  @override
  ConnectedSession? build() => null;

  /// First step. Returns [LoginStep.secondFactor] when the account has an authenticator.
  Future<LoginStep> signIn({
    required String server,
    required String username,
    required String password,
  }) async {
    // An explicit password sign-in must not leave another account's remembered ticket behind.
    await forgetBiometrics();
    final uri = parseServer(server);
    final api = SchoolApi(uri, client: ref.read(connectedHttpClientProvider));
    final step = await api.login(username.trim(), password);
    await ref.read(sharedPreferencesProvider).setString(_serverKey, uri.toString());
    if (step == LoginStep.secondFactor) {
      _pending = api;
      return step;
    }
    state = await _load(api);
    return step;
  }

  Future<void> completeSecondFactor(String code, {bool recovery = false}) async {
    final api = _pending;
    if (api == null) {
      throw const ApiFailure('El ingreso venció. Vuelve a escribir tu usuario y contraseña.');
    }
    await api.secondFactor(code, recovery: recovery);
    _pending = null;
    state = await _load(api);
  }

  void cancelSecondFactor() => _pending = null;

  Future<void> refresh() async {
    final current = state;
    if (current == null) return;
    state = await _load(current.api);
  }

  Future<void> signOut() async {
    final current = state;
    state = null;
    _pending = null;
    try {
      await forgetBiometrics();
    } on Object {
      /* Still close the server session if secure storage is unavailable. */
    }
    if (current == null) return;
    try {
      await current.api.logout();
    } on Object {
      // The local session is gone either way; the cookie also expires on the server.
    }
  }

  /// Session expired on the server (401): drop local state so the app asks to sign in again.
  void expire() {
    state = null;
    _pending = null;
    ref.read(biometricLockedProvider.notifier).setLocked(false);
    // Remove the usable credential immediately; preference cleanup may finish asynchronously.
    unawaited(forgetBiometrics().catchError((Object _) {}));
  }

  Future<void> enableBiometrics() async {
    final current = state;
    final vault = ref.read(biometricVaultProvider);
    if (current == null || !await vault.available()) {
      throw const ApiFailure('Registra una huella o biometría en los ajustes de tu teléfono.');
    }
    await current.api.post('/api/v1/auth/remember-device');
    final ticket = current.api.sessionTicket();
    if (ticket.isEmpty) {
      throw const ApiFailure('El desbloqueo está disponible en el app nativo del teléfono.');
    }
    try {
      await vault.seal(jsonEncode({'server': current.api.server.toString(), 'cookies': ticket}));
      await ref.read(biometricEnabledProvider.notifier).setEnabled(true);
    } on Object {
      try {
        await vault.clear();
      } on Object {
        /* The OS may have cancelled the operation. */
      }
      await forgetBiometrics();
      throw const ApiFailure(
        'No se activó el desbloqueo. Confirma tu biometría e inténtalo de nuevo.',
      );
    }
  }

  Future<void> unlockBiometrics() async {
    final raw = await ref.read(biometricVaultProvider).unlock();
    if (raw == null) {
      await forgetBiometrics();
      throw const ApiFailure('Vuelve a ingresar para preparar el desbloqueo.');
    }
    late SchoolApi api;
    try {
      final saved = jsonDecode(raw) as Map<String, dynamic>;
      api = SchoolApi(
        parseServer(saved['server'] as String),
        client: ref.read(connectedHttpClientProvider),
      );
      api.restoreTicket(Map<String, String>.from(saved['cookies'] as Map));
    } on Object {
      await forgetBiometrics();
      throw const ApiFailure('Vuelve a ingresar para preparar el desbloqueo.');
    }
    try {
      final restored = await _load(
        api,
      ); // Durable stamp, active account, permissions/faculties checked by the server.
      state = restored;
      ref.read(biometricLockedProvider.notifier).setLocked(false);
    } on ApiFailure catch (e) {
      if (e.unauthorized || e.status == 403) {
        await forgetBiometrics();
        state = null;
      }
      rethrow;
    }
  }

  Future<void> forgetBiometrics() async {
    // Web/demo clients never touch local secure storage.
    if (!ref.read(biometricEnabledProvider)) return;
    try {
      await ref.read(biometricVaultProvider).clear();
    } finally {
      await ref.read(biometricEnabledProvider.notifier).setEnabled(false);
      ref.read(biometricLockedProvider.notifier).setLocked(false);
    }
  }

  Future<void> disableBiometrics() async {
    final current = state;
    if (current != null) await current.api.post('/api/v1/auth/forget-device');
    await forgetBiometrics();
  }

  static Future<ConnectedSession> _load(SchoolApi api) async {
    final session = await api.get('/api/v1/session') as Map<String, dynamic>;
    final me = await api.get('/api/v1/me') as Map<String, dynamic>;
    final courses = await api.get('/api/v1/me/courses') as List<dynamic>;
    final students = <LinkedStudent>[];
    final person = me['person'] as Map<String, dynamic>?;
    final placement = me['placement'] as Map<String, dynamic>?;
    if (session['studentId'] != null && person != null) {
      students.add(
        LinkedStudent(
          id: session['studentId'] as String,
          name: person['displayName'] as String,
          code: person['studentCode'] as String?,
          placement: _placement(placement),
          isSelf: true,
        ),
      );
    }
    for (final raw in (me['students'] as List<dynamic>? ?? const [])) {
      final s = raw as Map<String, dynamic>;
      students.add(
        LinkedStudent(
          id: s['id'] as String,
          name: s['displayName'] as String,
          code: s['studentCode'] as String?,
          placement: _placement(s['placement'] as Map<String, dynamic>?),
          faculties: [for (final f in (s['faculties'] as List<dynamic>? ?? const [])) f as String],
        ),
      );
    }
    return ConnectedSession(
      api: api,
      username: session['username'] as String,
      displayName: session['displayName'] as String?,
      profile: (session['schoolProfile'] as String?) ?? 'Cuenta',
      permissions: {for (final p in session['permissions'] as List<dynamic>) p as String},
      mfaPending: [
        for (final p in (session['mfaPending'] as List<dynamic>? ?? const [])) p as String,
      ],
      students: students,
      courses: [
        for (final raw in courses)
          TeachingCourse(
            id: (raw as Map<String, dynamic>)['id'] as String,
            subject: raw['subject'] as String,
            grade: raw['grade'] as String,
            section: raw['section'] as String?,
            students: (raw['students'] as num?)?.toInt() ?? 0,
          ),
      ],
    );
  }

  static String? _placement(Map<String, dynamic>? p) {
    if (p == null) return null;
    final section = p['section'] as String?;
    return [p['grade'], if (section != null && section.isNotEmpty) 'Sección $section'].join(' · ');
  }
}

final connectedControllerProvider = NotifierProvider<ConnectedController, ConnectedSession?>(
  ConnectedController.new,
);
