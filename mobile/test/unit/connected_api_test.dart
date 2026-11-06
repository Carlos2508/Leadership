import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:school_platform_app/core/providers.dart';
import 'package:school_platform_app/features/connected/connected_session.dart';
import 'package:school_platform_app/features/connected/school_api.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Minimal fake of the school server: cookie session + antiforgery, like ASP.NET Core Identity (ADR-014).
class FakeSchool {
  final requests = <http.Request>[];
  bool requireSecondFactor = false;
  bool expireNextToken = false;
  int tokens = 0;
  bool revoked = false;
  bool offline = false;

  http.Response json(Object body, {int status = 200, Map<String, String> headers = const {}}) =>
      http.Response(
        jsonEncode(body),
        status,
        headers: {'content-type': 'application/json', ...headers},
      );

  bool signedIn(http.Request r) =>
      (r.headers['Cookie'] ?? '').contains('SchoolPlatform.Session=abc');

  late final client = MockClient((request) async {
    requests.add(request);
    if (offline) throw http.ClientException('synthetic offline');
    final path = request.url.path;
    if (path == '/api/v1/auth/antiforgery' || path == '/api/v1/antiforgery') {
      tokens++;
      return json(
        {'token': 't$tokens'},
        headers: {'set-cookie': '.AspNetCore.Antiforgery.x=af$tokens; path=/; httponly'},
      );
    }
    if (request.method == 'POST' && request.headers['X-XSRF-TOKEN'] == null) {
      return json({'title': 'csrf', 'code': 'invalid-csrf'}, status: 400);
    }
    switch (path) {
      case '/api/v1/auth/login':
        final body = jsonDecode(request.body) as Map<String, dynamic>;
        if (body['password'] != 'Correct-1') {
          return json({
            'title': 'No pudimos iniciar sesión.',
            'code': 'sign-in-failed',
          }, status: 401);
        }
        if (requireSecondFactor) return json({'status': 'second-factor'});
        return json(
          {'status': 'signed-in'},
          headers: {'set-cookie': 'SchoolPlatform.Session=abc; path=/; httponly'},
        );
      case '/api/v1/auth/login/second-factor':
        return json(
          {'status': 'signed-in'},
          headers: {'set-cookie': 'SchoolPlatform.Session=abc; path=/; httponly'},
        );
      case '/api/v1/auth/logout':
        return http.Response(
          '',
          204,
          headers: {
            'set-cookie': 'SchoolPlatform.Session=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/',
          },
        );
      case '/api/v1/auth/remember-device':
      case '/api/v1/auth/forget-device':
        return signedIn(request) ? http.Response('', 204) : http.Response('', 401);
      case '/api/v1/me/announcements/n1/read':
        if (expireNextToken) {
          expireNextToken = false;
          return json({'title': 'csrf', 'code': 'invalid-csrf'}, status: 400);
        }
        return http.Response('', 204);
    }
    if (revoked || !signedIn(request)) return http.Response('', 401);
    return switch (path) {
      '/api/v1/session' => json({
        'username': 'fam',
        'displayName': 'Marta Ficticia',
        'studentId': null,
        'schoolProfile': 'Familia',
        'permissions': <String>[],
        'mfaPending': <String>[],
      }),
      '/api/v1/me' => json({
        'person': {'id': 'p1', 'displayName': 'Marta Ficticia', 'studentCode': null},
        'placement': null,
        'students': [
          {
            'id': 's1',
            'displayName': 'Ana Ficticia',
            'studentCode': 'A-1',
            'faculties': ['AcademicRecords', 'Finance'],
            'placement': {'grade': 'Primero', 'section': 'A'},
          },
          {
            'id': 's2',
            'displayName': 'Bruno Ficticio',
            'studentCode': 'B-2',
            'faculties': ['Finance'],
            'placement': null,
          },
        ],
      }),
      '/api/v1/me/courses' => json(<Object>[]),
      _ => http.Response('', 404),
    };
  });
}

void main() {
  test('parseServer only accepts HTTPS or local development hosts', () {
    expect(parseServer('colegio.ejemplo').toString(), 'https://colegio.ejemplo');
    expect(parseServer('https://colegio.ejemplo/').host, 'colegio.ejemplo');
    expect(parseServer('http://localhost:5080').port, 5080);
    expect(() => parseServer('http://colegio.ejemplo'), throwsA(isA<ApiFailure>()));
    expect(() => parseServer('https://user:pw@colegio.ejemplo'), throwsA(isA<ApiFailure>()));
    expect(() => parseServer('https://colegio.ejemplo/otra/ruta'), throwsA(isA<ApiFailure>()));
  });

  test(
    'login keeps the session cookie, sends it back and refreshes the token after signing in',
    () async {
      final school = FakeSchool();
      final api = SchoolApi(Uri.parse('https://colegio.ejemplo'), client: school.client);

      await expectLater(
        api.login('fam', 'wrong'),
        throwsA(isA<ApiFailure>().having((e) => e.status, 'status', 401)),
      );
      expect(api.hasSession, isFalse);

      expect(await api.login('fam', 'Correct-1'), LoginStep.signedIn);
      expect(api.hasSession, isTrue);
      final login = school.requests.lastWhere((r) => r.url.path == '/api/v1/auth/login');
      expect(login.headers['X-XSRF-TOKEN'], isNotNull);
      expect(login.headers['Cookie'], contains('.AspNetCore.Antiforgery.x='));

      final session = await api.get('/api/v1/session') as Map<String, dynamic>;
      expect(session['username'], 'fam');

      // The first write after signing in asks for a token bound to the new identity.
      final before = school.tokens;
      await api.post('/api/v1/me/announcements/n1/read');
      expect(school.tokens, before + 1);
      expect(school.requests.last.url.path, '/api/v1/me/announcements/n1/read');

      // A rejected token is refreshed once and the write retried.
      school.expireNextToken = true;
      await api.post('/api/v1/me/announcements/n1/read');
      expect(school.tokens, before + 2);

      await api.logout();
      expect(api.hasSession, isFalse);
      await expectLater(
        api.get('/api/v1/session'),
        throwsA(isA<ApiFailure>().having((e) => e.unauthorized, 'unauthorized', isTrue)),
      );
    },
  );

  test('second factor completes the sign-in only after the code', () async {
    final school = FakeSchool()..requireSecondFactor = true;
    final api = SchoolApi(Uri.parse('https://colegio.ejemplo'), client: school.client);
    expect(await api.login('fam', 'Correct-1'), LoginStep.secondFactor);
    expect(api.hasSession, isFalse);
    expect(await api.secondFactor('123 456'), LoginStep.signedIn);
    expect(api.hasSession, isTrue);
  });

  test('controller maps the session: linked students, faculties and saved server', () async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    final school = FakeSchool();
    final container = ProviderContainer(
      overrides: [
        sharedPreferencesProvider.overrideWithValue(prefs),
        connectedHttpClientProvider.overrideWithValue(school.client),
      ],
    );
    addTearDown(container.dispose);

    final controller = container.read(connectedControllerProvider.notifier);
    expect(
      await controller.signIn(server: 'colegio.ejemplo', username: 'fam', password: 'Correct-1'),
      LoginStep.signedIn,
    );
    final session = container.read(connectedControllerProvider)!;
    expect(session.profile, 'Familia');
    expect(session.isStudent, isFalse);
    expect(session.students.map((s) => s.name), ['Ana Ficticia', 'Bruno Ficticio']);
    expect(session.students.first.canAcademic, isTrue);
    expect(session.students.first.placement, 'Primero · Sección A');
    expect(session.students.last.canAcademic, isFalse);
    expect(session.students.last.canFinance, isTrue);
    expect(session.hasSchoolModules, isFalse);
    expect(prefs.getString('connected.server'), 'https://colegio.ejemplo');

    await controller.signOut();
    expect(container.read(connectedControllerProvider), isNull);
  });
}
