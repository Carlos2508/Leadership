import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:school_platform_app/core/providers.dart';
import 'package:school_platform_app/core/theme/app_theme.dart';
import 'package:school_platform_app/features/connected/connected_session.dart';
import 'package:school_platform_app/features/connected/timetable_page.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../helpers/test_app.dart';
import '../unit/connected_api_test.dart' show FakeSchool;

void main() {
  setUpAll(initTestEnvironment);
  late ProviderContainer container;
  bool fail = false;
  final paths = <String>[];
  Future<void> prepare(WidgetTester tester, {double scale = 1, bool dark = false}) async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    final fake = FakeSchool();
    fail = false;
    paths.clear();
    http.Response json(Object data, {int status = 200}) =>
        http.Response(jsonEncode(data), status, headers: {'content-type': 'application/json'});
    final client = MockClient((request) async {
      if (request.url.path == '/api/v1/me/timetables/years') {
        return json([
          {
            'id': 'y1',
            'label': '2027',
            'startsOn': '2027-01-10',
            'endsOn': '2027-11-30',
            'isDefault': true,
          },
        ]);
      }
      if (request.url.path == '/api/v1/me/timetables/students/s1') {
        paths.add(request.url.toString());
        if (fail) return json({'title': 'Horario no disponible para prueba'}, status: 503);
        return json({
          'title': 'Horario semanal',
          'year': '2027',
          'on': request.url.queryParameters['on'],
          'slots': [
            {
              'day': 1,
              'start': 480,
              'end': 525,
              'kind': 'Lesson',
              'subject': 'Matemática',
              'teacher': 'Docente sintética',
              'grade': 'Primero',
              'section': 'A',
              'room': 'Aula 1',
              'courseId': 'c1',
            },
            {
              'day': 1,
              'start': 525,
              'end': 540,
              'kind': 'Break',
              'subject': 'Recreo',
              'teacher': '',
              'grade': 'Primero',
              'section': 'A',
              'room': '',
              'courseId': null,
            },
            {
              'day': 2,
              'start': 480,
              'end': 525,
              'kind': 'Lesson',
              'subject': 'Ciencias',
              'teacher': 'Docente sintética',
              'grade': 'Primero',
              'section': 'A',
              'room': 'Laboratorio',
              'courseId': 'c2',
            },
          ],
        });
      }
      final forwarded = http.Request(request.method, request.url)
        ..headers.addAll(request.headers)
        ..body = request.body;
      return http.Response.fromStream(await fake.client.send(forwarded));
    });
    container = ProviderContainer(
      overrides: [
        sharedPreferencesProvider.overrideWithValue(prefs),
        connectedHttpClientProvider.overrideWithValue(client),
      ],
    );
    addTearDown(container.dispose);
    await container
        .read(connectedControllerProvider.notifier)
        .signIn(server: 'https://colegio.ejemplo', username: 'fam', password: 'Correct-1');
    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: MaterialApp(
          theme: dark
              ? AppTheme.dark(testInstitution().palette)
              : AppTheme.light(testInstitution().palette),
          builder: (context, child) => MediaQuery(
            data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(scale)),
            child: child!,
          ),
          home: const TimetablePage(studentId: 's1'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      find.text('Matemática'),
      200,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.pumpAndSettle();
  }

  testWidgets(
    'family agenda reads the authorized student and changes days without mixing lessons',
    (tester) async {
      await prepare(tester);
      expect(find.text('Matemática'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('Recreo'),
        150,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('Recreo'), findsOneWidget);
      expect(paths.single, contains('yearId=y1'));
      await tester.drag(find.byType(CustomScrollView).first, const Offset(0, 800));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Martes'));
      await tester.tap(find.text('Martes'));
      await tester.pumpAndSettle();
      expect(find.text('Ciencias'), findsOneWidget);
      expect(find.text('Matemática'), findsNothing);
      expect(find.text('Bruno Ficticio'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets('a failed refresh removes previous lessons and retry restores them', (tester) async {
    await prepare(tester);
    fail = true;
    final refresh = tester.widget<RefreshIndicator>(find.byType(RefreshIndicator));
    await refresh.onRefresh();
    await tester.pumpAndSettle();
    expect(find.text('Horario no disponible para prueba'), findsOneWidget);
    expect(find.text('Matemática'), findsNothing);
    fail = false;
    await tester.scrollUntilVisible(
      find.text('Reintentar'),
      150,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Reintentar'));
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(
      find.text('Matemática'),
      200,
      scrollable: find.byType(Scrollable).first,
    );
    expect(find.text('Matemática'), findsOneWidget);
  });
  testWidgets('agenda remains readable on a narrow phone with large text and dark theme', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await prepare(tester, scale: 1.5, dark: true);
    expect(find.text('Matemática'), findsOneWidget);
    await tester.drag(find.byType(CustomScrollView).first, const Offset(0, 800));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Martes'));
    await tester.tap(find.text('Martes'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });
}
