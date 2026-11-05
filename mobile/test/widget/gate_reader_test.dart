import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:school_platform_app/app/router/app_router.dart';
import 'package:school_platform_app/features/gate/gate_client.dart';
import 'package:school_platform_app/features/gate/gate_reader_page.dart';

import '../helpers/test_app.dart';

void main() {
  setUpAll(initTestEnvironment);
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));
  testWidgets('reader opens without a demo account and returns to login', (tester) async {
    final container = await pumpApp(tester, size: const Size(320, 640));
    unawaited(container.read(routerProvider).push<void>('/gate-reader'));
    await tester.pumpAndSettle();
    expect(find.text('Emparejar teléfono'), findsOneWidget);
    expect(find.text('Escanear QR de emparejamiento'), findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(container.read(routerProvider).state.matchedLocation, '/login');
  });

  testWidgets('pairing hides the credential, marks a card and clears revoked access', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    var revoked = false;
    final requests = <http.Request>[];
    final client = GateClient(
      httpClient: MockClient((request) async {
        requests.add(request);
        if (revoked) return http.Response('', 401);
        if (request.method == 'GET') {
          return http.Response(
            jsonEncode({'school': 'Colegio sintético', 'device': 'Lector'}),
            200,
          );
        }
        return http.Response(
          jsonEncode({
            'outcome': 'Recorded',
            'message': 'Entrada registrada',
            'student': {'name': 'Sara Sintética', 'code': 'G-1', 'placement': 'Cuarto'},
          }),
          200,
        );
      }),
    );
    await tester.pumpWidget(MaterialApp(home: GateReaderPage(client: client)));
    await tester.pumpAndSettle();
    final token = 'gd_${List.filled(43, 'a').join()}';
    final payload = jsonEncode({'v': 1, 'server': 'https://school.example', 'token': token});
    await tester.enterText(find.byType(TextField), payload);
    await tester.tap(find.text('Leer emparejamiento'));
    // Pairing waits for approval; the reader's busy indicator keeps animating behind the dialog.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 350));
    expect(requests, isEmpty);
    await tester.tap(find.text('Conectar'));
    await tester.pumpAndSettle();
    expect(find.text('Colegio sintético · Lector'), findsOneWidget);
    expect(tester.widget<TextField>(find.byType(TextField)).controller!.text, isEmpty);
    expect(find.textContaining(token), findsNothing);
    expect(await const FlutterSecureStorage().read(key: 'gate.device.v1'), payload);
    await tester.enterText(find.byType(TextField), '0123456789AB');
    await tester.tap(find.text('Registrar lectura'));
    await tester.pumpAndSettle();
    expect(find.text('Entrada registrada'), findsOneWidget);
    expect(find.text('Sara Sintética'), findsOneWidget);
    revoked = true;
    await tester.enterText(find.byType(TextField), '0123456789AB');
    await tester.tap(find.text('Registrar lectura'));
    await tester.pumpAndSettle();
    expect(find.text('Emparejar teléfono'), findsOneWidget);
    expect(find.textContaining('revocado'), findsOneWidget);
    expect(await const FlutterSecureStorage().read(key: 'gate.device.v1'), isNull);
    expect(tester.takeException(), isNull);
  });
}
