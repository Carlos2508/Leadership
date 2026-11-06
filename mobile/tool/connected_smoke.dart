// Smoke test of the connected app client against a running development host (ADR-014).
// Usage: dart run tool/connected_smoke.dart http://127.0.0.1:5091
// Passwords come only from QA_PASS_<KEY> environment variables (fictional qa-* accounts); nothing is printed or stored.
import 'dart:io';

import 'package:school_platform_app/features/connected/school_api.dart';

Future<void> main(List<String> args) async {
  final server = parseServer(args.isEmpty ? 'http://127.0.0.1:5091' : args.first);
  final accounts = {
    'qa-estudiante': 'ESTUDIANTE',
    'qa-familia': 'FAMILIA',
    'qa-docente': 'DOCENTE',
    'qa-director': 'DIRECTOR',
    'qa-cajero': 'CAJERO',
  };
  var failures = 0;
  Future<void> check(String label, Future<Object?> Function() action, {int? expect}) async {
    try {
      await action();
      final ok = expect == null;
      if (!ok) failures++;
      stdout.writeln('${ok ? 'ok  ' : 'FAIL'} $label${ok ? '' : ' (se esperaba $expect)'}');
    } on ApiFailure catch (e) {
      final ok = expect != null && e.status == expect;
      if (!ok) failures++;
      stdout.writeln(
        '${ok ? 'ok  ' : 'FAIL'} $label -> ${e.status} ${ok ? '(rechazado, correcto)' : e.message}',
      );
    }
  }

  for (final entry in accounts.entries) {
    final password = Platform.environment['QA_PASS_${entry.value}'];
    if (password == null) {
      stdout.writeln('skip ${entry.key}: falta la contraseña en el ambiente');
      continue;
    }
    final api = SchoolApi(server);
    stdout.writeln('--- ${entry.key}');
    await check('ingreso', () => api.login(entry.key, password));
    final session = await api.get('/api/v1/session') as Map<String, dynamic>;
    final me = await api.get('/api/v1/me') as Map<String, dynamic>;
    final courses = await api.get('/api/v1/me/courses') as List<dynamic>;
    stdout.writeln(
      '     perfil ${session['schoolProfile']} · permisos ${(session['permissions'] as List).length}'
      ' · estudiantes ${(me['students'] as List).length + (session['studentId'] == null ? 0 : 1)} · cursos ${courses.length}',
    );
    await check('panel', () => api.get('/api/v1/dashboard'));
    final feed = await api.get('/api/v1/me/announcements') as List<dynamic>;
    stdout.writeln('     avisos ${feed.length}');
    final unread = feed.cast<Map<String, dynamic>>().where((a) => a['read'] != true).toList();
    if (unread.isNotEmpty) {
      await check(
        'marcar aviso leído (escritura con token)',
        () => api.post('/api/v1/me/announcements/${unread.first['id']}/read'),
      );
    }
    final ids = [
      if (session['studentId'] != null) session['studentId'] as String,
      for (final s in (me['students'] as List).cast<Map<String, dynamic>>()) s['id'] as String,
    ];
    for (final id in ids) {
      await check(
        'notas $id',
        () => api.get('/api/v1/me/students/$id/grades'),
        expect: entry.key == 'qa-familia' && id.startsWith('948') ? 404 : null,
      );
      await check(
        'tareas $id',
        () => api.get('/api/v1/me/students/$id/assignments'),
        expect: entry.key == 'qa-familia' && id.startsWith('948') ? 404 : null,
      );
      await check(
        'estado de cuenta $id',
        () => api.get('/api/v1/me/students/$id/statement'),
        expect: entry.key == 'qa-estudiante' ? 404 : null,
      );
    }
    for (final c in courses.cast<Map<String, dynamic>>()) {
      await check('libreta ${c['subject']}', () => api.get('/api/v1/courses/${c['id']}/grades'));
      await check(
        'asistencia ${c['subject']}',
        () => api.get('/api/v1/courses/${c['id']}/attendance'),
      );
    }
    final staff = (session['permissions'] as List).contains('people.read');
    await check(
      'directorio',
      () => api.get('/api/v1/people?page=1&search=Ficticio'),
      expect: staff ? null : 403,
    );
    final finance = (session['permissions'] as List).contains('finance.read');
    await check(
      'caja: estado de cuenta de Bruno',
      () => api.get('/api/v1/finance/students/948DB589-6A27-4896-BE81-AE5DEAB5E96D/statement'),
      expect: finance ? null : 403,
    );
    await check('cerrar sesión', api.logout);
    await check('sesión cerrada', () => api.get('/api/v1/session'), expect: 401);
  }
  stdout.writeln(failures == 0 ? '\nTodo correcto.' : '\n$failures verificaciones fallaron.');
  exitCode = failures == 0 ? 0 : 1;
}
