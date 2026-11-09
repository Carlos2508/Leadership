import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:school_platform_app/core/providers.dart';
import 'package:school_platform_app/core/theme/app_theme.dart';
import 'package:school_platform_app/features/connected/biometric_lock.dart';
import 'package:school_platform_app/features/connected/biometric_vault.dart';
import 'package:school_platform_app/features/connected/chat_page.dart';
import 'package:school_platform_app/features/connected/chat_status.dart';
import 'package:school_platform_app/features/connected/connected_session.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../helpers/test_app.dart';
import '../unit/biometric_session_test.dart' show FakeVault;
import '../unit/connected_api_test.dart' show FakeSchool;

void main() {
  setUpAll(initTestEnvironment);
  late ProviderContainer container;
  late FakeSchool school;
  late FakeVault vault;
  late List<ChatMap> messages;
  late List<String> attempts;
  late List<ChatMap> activity;
  late ChatMap thread;
  bool failSend = false;
  Completer<http.Response>? delayedSend;
  int peerRead = 0;

  Future<void> prepare(
    WidgetTester tester,
    Widget child, {
    bool dark = false,
    double textScale = 1,
  }) async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    school = FakeSchool();
    vault = FakeVault();
    attempts = [];
    activity = [];
    failSend = false;
    delayedSend = null;
    peerRead = 0;
    thread = {
      'id': 'c1',
      'academicYearId': 'y1',
      'year': '2027',
      'name': 'Elena Docente',
      'role': 'Docente',
      'student': 'Ana Ejemplo',
      'preview': 'Hola',
      'updatedAt': '2027-02-01T10:00:00Z',
      'unread': 1,
      'lastSequence': 1,
      'peerReadSequence': 0,
      'canSend': true,
    };
    messages = [
      {
        'sequence': 1,
        'clientMessageId': 'old',
        'body': 'Hola, ¿cómo va tu tarea?',
        'sentAt': '2027-02-01T10:00:00Z',
        'mine': false,
      },
    ];
    http.Response json(Object data, {int status = 200}) =>
        http.Response(jsonEncode(data), status, headers: {'content-type': 'application/json'});
    final client = MockClient((request) async {
      final path = request.url.path;
      if (path == '/api/v1/chat/activity') {
        activity.add(jsonDecode(request.body) as ChatMap);
        return http.Response('', 204);
      }
      if (path == '/api/v1/chat/summary') {
        return json({
          'available': true,
          'unread': 1,
          'conversations': 1,
          'revision': 'r1',
          'pendingDeliveries': [
            {'academicYearId': 'y1', 'conversationId': 'c1', 'throughSequence': 1},
          ],
        });
      }
      if (path == '/api/v1/chat/years') {
        return json([
          {'id': 'y1', 'label': '2027', 'status': 'Active'},
        ]);
      }
      if (path.endsWith('/chat/conversations')) {
        return json({
          'items': [thread],
          'total': 1,
          'page': 1,
          'pageSize': 40,
        });
      }
      if (path.endsWith('/contacts')) {
        return json({
          'items': [
            {
              'studentId': 's1',
              'teacherId': 't1',
              'memberId': 's1',
              'name': 'Elena Docente',
              'role': 'Docente',
              'student': 'Ana Ejemplo',
            },
          ],
          'total': 1,
          'page': 1,
          'pageSize': 40,
        });
      }
      if (path.endsWith('/messages')) {
        if (request.method == 'POST') {
          final body = jsonDecode(request.body) as ChatMap;
          attempts.add(body['clientMessageId'] as String);
          if (failSend) return json({'title': 'Conexión interrumpida'}, status: 503);
          final message = {
            'sequence': messages.length + 1,
            'clientMessageId': body['clientMessageId'],
            'body': body['body'],
            'sentAt': '2027-02-01T10:01:00Z',
            'mine': true,
          };
          messages.add(message);
          if (delayedSend != null) return delayedSend!.future;
          return json(message);
        }
        final after = int.tryParse(request.url.queryParameters['after'] ?? '') ?? 0;
        return json({
          'conversation': {...thread, 'peerReadSequence': peerRead},
          'items': messages.where((m) => (m['sequence'] as int) > after).toList(),
          'hasMore': false,
          'receipts': messages
              .where((m) => m['mine'] == true)
              .map(
                (m) => {
                  'sequence': m['sequence'],
                  'deliveredAt': m['deliveredAt'],
                  'readAt': m['readAt'],
                },
              )
              .toList(),
        });
      }
      if (path.endsWith('/read')) return http.Response('', 204);
      final forwarded = http.Request(request.method, request.url)
        ..headers.addAll(request.headers)
        ..body = request.body;
      return http.Response.fromStream(await school.client.send(forwarded));
    });
    container = ProviderContainer(
      overrides: [
        sharedPreferencesProvider.overrideWithValue(prefs),
        connectedHttpClientProvider.overrideWithValue(client),
        biometricVaultProvider.overrideWithValue(vault),
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
            data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(textScale)),
            child: child!,
          ),
          home: child,
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets(
    'native chat keeps a failed message and retries the same id, then receives read status',
    (tester) async {
      await prepare(
        tester,
        const ChatThreadPage(
          year: 'y1',
          thread: {'id': 'c1', 'name': 'Elena Docente', 'student': 'Ana Ejemplo', 'canSend': true},
        ),
      );
      expect(find.text('Hola, ¿cómo va tu tarea?'), findsOneWidget);
      await tester.enterText(find.byType(TextField), 'Mensaje importante');
      await tester.pump();
      failSend = true;
      await tester.tap(find.byTooltip('Enviar mensaje'));
      await tester.pumpAndSettle();
      expect(find.text('Conexión interrumpida'), findsOneWidget);
      expect(
        tester.widget<TextField>(find.byType(TextField)).controller!.text,
        'Mensaje importante',
      );
      expect(tester.widget<TextField>(find.byType(TextField)).readOnly, isTrue);
      await tester.pump(const Duration(seconds: 3));
      await tester.pumpAndSettle();
      expect(find.text('Conexión interrumpida'), findsOneWidget);
      expect(find.text('Reintentar'), findsOneWidget);
      failSend = false;
      await tester.tap(find.text('Reintentar'));
      await tester.pumpAndSettle();
      expect(attempts.length, 2);
      expect(attempts.toSet().length, 1);
      expect(find.text('Mensaje importante'), findsOneWidget);
      expect(tester.widget<TextField>(find.byType(TextField)).controller!.text, isEmpty);
      thread['peerDeliveredSequence'] = 2;
      messages.last['deliveredAt'] = '2027-02-01T10:02:00Z';
      await tester.pump(const Duration(seconds: 3));
      await tester.pumpAndSettle();
      expect(find.textContaining('✓✓ Entregado'), findsOneWidget);
      peerRead = 2;
      messages.last['readAt'] = '2027-02-01T10:03:00Z';
      await tester.pump(const Duration(seconds: 3));
      await tester.pumpAndSettle();
      expect(find.textContaining('✓✓ Visto a las'), findsOneWidget);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  testWidgets('back keeps the draft when leaving is cancelled', (tester) async {
    await prepare(
      tester,
      const ChatThreadPage(
        year: 'y1',
        thread: {'id': 'c1', 'name': 'Elena Docente', 'student': 'Ana Ejemplo', 'canSend': true},
      ),
    );
    await tester.enterText(find.byType(TextField), 'Todavía no envié esto');
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();
    expect(find.text('Hay un mensaje sin enviar'), findsOneWidget);
    await tester.tap(find.text('Seguir escribiendo'));
    await tester.pumpAndSettle();
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'Todavía no envié esto',
    );
    expect(attempts, isEmpty);
    await tester.pumpWidget(const SizedBox.shrink());
  });
  testWidgets('presence expires on screen despite a skewed device clock and a failed network', (
    tester,
  ) async {
    var deviceNow = DateTime.utc(2035);
    final presence = {
      'serverNow': '2027-02-01T10:00:00Z',
      'peerActiveUntil': '2027-02-01T10:00:45Z',
      'peerLastActiveAt': '2027-02-01T10:00:00Z',
    };
    await prepare(tester, ChatPresenceLabel(thread: presence, now: () => deviceNow));
    expect(find.text('En línea'), findsOneWidget);
    deviceNow = deviceNow.add(const Duration(seconds: 46));
    await tester.pump(const Duration(seconds: 1));
    expect(find.text('En línea'), findsNothing);
    expect(find.textContaining('Últ. vez:'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  test('old read cursors show seen without inventing a historical time', () {
    final message = {'sequence': 1, 'sentAt': '2027-02-01T10:00:00Z'};
    expect(chatReceiptLabel(message, {}), 'Enviado');
    expect(chatReceiptLabel(message, {'peerDeliveredSequence': 1}), 'Entregado');
    expect(chatReceiptLabel(message, {'peerReadSequence': 1}), 'Visto');
    expect(chatReceiptDetails(message, {'peerReadSequence': 1}), contains('hora no registrada'));
  });

  testWidgets(
    'foreground acknowledges delivery and background or lock closes only its own presence',
    (tester) async {
      await prepare(
        tester,
        Consumer(
          builder: (context, ref, child) {
            ref.watch(chatInboxProvider);
            return const SizedBox.shrink();
          },
        ),
      );
      expect(activity.last['active'], isTrue);
      expect(activity.last['deliveries'], [
        {'academicYearId': 'y1', 'conversationId': 'c1', 'throughSequence': 1},
      ]);
      final clientId = activity.last['clientId'];
      final controller = container.read(chatInboxProvider.notifier);
      await controller.setForeground(false);
      expect(activity.last['active'], isFalse);
      final version = activity.last['version'] as int;
      await controller.setForeground(true);
      expect(activity.last['active'], isTrue);
      expect(activity.last['clientId'], clientId);
      expect(activity.last['version'], greaterThan(version));
      container.read(biometricLockedProvider.notifier).state = true;
      await controller.setForeground(true);
      expect(activity.last['active'], isFalse);
      await tester.pumpWidget(const SizedBox.shrink());
      container.invalidate(chatInboxProvider);
      await tester.pump();
    },
  );
  testWidgets(
    'a committed message confirmed by polling does not lock the composer when its response is lost',
    (tester) async {
      await prepare(
        tester,
        const ChatThreadPage(
          year: 'y1',
          thread: {'id': 'c1', 'name': 'Elena Docente', 'student': 'Ana Ejemplo', 'canSend': true},
        ),
      );
      delayedSend = Completer<http.Response>();
      await tester.enterText(find.byType(TextField), 'Se guardó aunque la respuesta se perdió');
      await tester.pump();
      await tester.tap(find.byTooltip('Enviar mensaje'));
      await tester.pump();
      await tester.pump(const Duration(seconds: 3));
      await tester.pump(const Duration(milliseconds: 100));
      expect(tester.widget<TextField>(find.byType(TextField)).controller!.text, isEmpty);
      expect(tester.widget<TextField>(find.byType(TextField)).readOnly, isFalse);
      await tester.enterText(find.byType(TextField), 'Borrador del siguiente mensaje');
      delayedSend!.complete(
        http.Response(
          '{"title":"Respuesta perdida"}',
          503,
          headers: {'content-type': 'application/json'},
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.widget<TextField>(find.byType(TextField)).readOnly, isFalse);
      expect(
        tester.widget<TextField>(find.byType(TextField)).controller!.text,
        'Borrador del siguiente mensaje',
      );
      expect(find.text('Respuesta perdida'), findsNothing);
      expect(messages.length, 2);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  testWidgets('inbox shows the selected cycle and authorized contacts', (tester) async {
    await prepare(tester, const ChatHomePage());
    expect(find.text('2027'), findsWidgets);
    expect(find.text('Elena Docente'), findsOneWidget);
    await tester.tap(find.byTooltip('Nueva conversación'));
    await tester.pumpAndSettle();
    expect(find.text('Contactos autorizados de los cursos del estudiante.'), findsOneWidget);
    expect(find.text('Docente · Sobre Ana Ejemplo'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('background lock hides private content until a protected restore succeeds', (
    tester,
  ) async {
    await prepare(tester, const BiometricLock(child: Center(child: Text('Contenido privado'))));
    await container.read(connectedControllerProvider.notifier).enableBiometrics();
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
    await tester.pump();
    expect(find.text('Contenido privado'), findsNothing);
    expect(find.text('Tu espacio está bloqueado'), findsOneWidget);
    tester.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
    await tester.pump();
    vault.cancelled = true;
    await tester.tap(find.text('Desbloquear'));
    await tester.pumpAndSettle();
    expect(find.text('Contenido privado'), findsNothing);
    vault.cancelled = false;
    await tester.tap(find.text('Desbloquear'));
    await tester.pumpAndSettle();
    expect(find.text('Contenido privado'), findsOneWidget);
    expect(vault.reads, 2);
    await tester.pumpWidget(const SizedBox.shrink());
  });
  for (final variant in [(320.0, 1.0, false), (390.0, 1.5, true)]) {
    testWidgets(
      'chat layout fits ${variant.$1} px with text scale ${variant.$2} and dark=${variant.$3}',
      (tester) async {
        tester.view.devicePixelRatio = 1;
        tester.view.physicalSize = Size(variant.$1, 700);
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        await prepare(
          tester,
          const ChatThreadPage(
            year: 'y1',
            thread: {
              'id': 'c1',
              'name': 'Elena Docente de ejemplo',
              'student': 'Ana Ejemplo',
              'canSend': true,
            },
          ),
          dark: variant.$3,
          textScale: variant.$2,
        );
        await tester.enterText(
          find.byType(TextField),
          'Mensaje con varias líneas para revisar que el teclado y los botones sigan dentro de la pantalla.\nOtra línea.',
        );
        await tester.pump();
        expect(tester.takeException(), isNull);
        expect(find.byTooltip('Enviar mensaje'), findsOneWidget);
        await tester.pumpWidget(const SizedBox.shrink());
      },
    );
  }
}
