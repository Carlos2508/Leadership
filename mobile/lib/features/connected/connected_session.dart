import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:http/http.dart' as http;

import '../../core/providers.dart';
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

/// Last server address used (not secret). Credentials and cookies are never stored.
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
