import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:school_platform_app/core/providers.dart';
import 'package:school_platform_app/core/theme/app_theme.dart';
import 'package:school_platform_app/features/connected/connected_session.dart';
import 'package:school_platform_app/features/connected/inventory_page.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../helpers/test_app.dart';
import '../unit/connected_api_test.dart' show FakeSchool;

void main() {
  setUpAll(initTestEnvironment);
  late ProviderContainer container;
  late InventoryMap product;
  late List<InventoryMap> history;
  late List<InventoryMap> attempts;
  bool lostResponse = false;
  bool failList = false;
  bool readOnly = false;

  Future<void> prepare(
    WidgetTester tester,
    Widget child, {
    bool dark = false,
    double scale = 1,
  }) async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    final school = FakeSchool();
    attempts = [];
    history = [];
    lostResponse = false;
    failList = false;
    product = {
      'id': 'p1',
      'code': 'LIB-01',
      'name': 'Cuaderno escolar',
      'category': 'Cuadernos',
      'location': 'Estante A',
      'minimum': 5,
      'onHand': 10,
      'isActive': true,
      'lowStock': false,
      'version': 'v1',
    };
    http.Response json(Object body, {int status = 200}) =>
        http.Response(jsonEncode(body), status, headers: {'content-type': 'application/json'});
    final client = MockClient((request) async {
      final path = request.url.path;
      if (path == '/api/v1/session') {
        return json({
          'username': 'fam',
          'displayName': 'Persona sintética',
          'studentId': null,
          'schoolProfile': 'Personal',
          'permissions': [
            'inventory.read',
            if (!readOnly) ...['inventory.manage', 'inventory.move'],
          ],
          'mfaPending': <String>[],
        });
      }
      if (path == '/api/v1/inventory/summary') {
        return json({
          'activeProducts': 1,
          'units': product['onHand'],
          'lowStockProducts': product['lowStock'] == true ? 1 : 0,
        });
      }
      if (path == '/api/v1/inventory/products') {
        if (failList) return json({'title': 'No se pudo consultar inventario'}, status: 503);
        if (request.method == 'POST') {
          final body = jsonDecode(request.body) as InventoryMap;
          attempts.add(body);
          return json(product);
        }
        return json({
          'items': [product],
          'total': 1,
          'page': 1,
          'pageSize': 40,
        });
      }
      if (path == '/api/v1/inventory/products/p1') return json(product);
      if (path.endsWith('/counts') || (path.endsWith('/movements') && request.method == 'POST')) {
        final body = jsonDecode(request.body) as InventoryMap;
        attempts.add(body);
        if (history.every((m) => m['id'] != body['requestId'])) {
          final before = product['onHand'] as int;
          final quantity = body['quantity'] as int;
          final after = body['kind'] == 'Count'
              ? quantity
              : body['kind'] == 'In'
              ? before + quantity
              : before - quantity;
          if (after < 0) {
            return json({'title': 'No hay suficientes unidades disponibles'}, status: 400);
          }
          history.insert(0, {
            'id': body['requestId'],
            'sequence': history.length + 1,
            'kind': body['kind'],
            'delta': after - before,
            'before': before,
            'after': after,
            'reason': body['reason'],
            'reference': body['reference'],
            'occurredAt': '2027-02-01T12:00:00Z',
            'actor': 'Operador de prueba',
          });
          product = {
            ...product,
            'onHand': after,
            'version': 'v${history.length + 1}',
            'lowStock': after <= 5,
          };
        }
        if (lostResponse) {
          lostResponse = false;
          return json({'title': 'Respuesta perdida'}, status: 503);
        }
        return json(product);
      }
      if (path.endsWith('/movements')) {
        return json({'items': history, 'total': history.length, 'page': 1, 'pageSize': 40});
      }
      final forwarded = http.Request(request.method, request.url)
        ..headers.addAll(request.headers)
        ..body = request.body;
      return http.Response.fromStream(await school.client.send(forwarded));
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
          home: child,
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  setUp(() => readOnly = false);
  testWidgets(
    'native inventory retries a committed exit with the same attempt and keeps one movement',
    (tester) async {
      await prepare(tester, const InventoryPage());
      await tester.tap(find.text('Cuaderno escolar'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Salida'));
      await tester.pumpAndSettle();
      await tester.enterText(find.widgetWithText(TextFormField, 'Cantidad de unidades'), '3');
      lostResponse = true;
      await tester.ensureVisible(find.text('Registrar salida'));
      await tester.tap(find.text('Registrar salida'));
      await tester.pumpAndSettle();
      expect(find.textContaining('No se pudo confirmar el registro'), findsOneWidget);
      expect(product['onHand'], 7);
      await tester.ensureVisible(find.text('Reintentar registro'));
      await tester.tap(find.text('Reintentar registro'));
      await tester.pumpAndSettle();
      expect(attempts.length, 2);
      expect(attempts[0]['requestId'], attempts[1]['requestId']);
      expect(history.length, 1);
      expect(find.text('7 unidades'), findsOneWidget);
      expect(find.text('10 → 7 unidades'), findsOneWidget);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );
  testWidgets('physical count accepts zero and preserves the adjustment', (tester) async {
    await prepare(tester, const InventoryPage());
    await tester.tap(find.text('Cuaderno escolar'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Conteo físico'));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Unidades contadas físicamente'),
      '0',
    );
    await tester.ensureVisible(find.text('Registrar conteo físico'));
    await tester.tap(find.text('Registrar conteo físico'));
    await tester.pumpAndSettle();
    expect(find.text('0 unidades'), findsOneWidget);
    expect(history.single['delta'], -10);
    await tester.pumpWidget(const SizedBox.shrink());
  });
  testWidgets('new article submits its opening quantity with one stable creation attempt', (
    tester,
  ) async {
    await prepare(tester, const InventoryPage());
    await tester.tap(find.text('Nuevo artículo'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Código del artículo'), 'NUEVO-01');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Nombre del artículo'),
      'Marcador de prueba',
    );
    await tester.ensureVisible(find.widgetWithText(TextFormField, 'Unidades iniciales'));
    await tester.enterText(find.widgetWithText(TextFormField, 'Unidades iniciales'), '15');
    await tester.ensureVisible(find.text('Guardar artículo'));
    await tester.tap(find.text('Guardar artículo'));
    await tester.pumpAndSettle();
    expect(attempts.single['code'], 'NUEVO-01');
    expect(attempts.single['initialQuantity'], 15);
    expect(attempts.single['requestId'], matches(RegExp(r'^[a-f0-9-]{36}$')));
    expect(find.text('Artículos y existencias'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });
  testWidgets('read-only inventory exposes the ledger without stock or product actions', (
    tester,
  ) async {
    readOnly = true;
    await prepare(tester, const InventoryPage());
    expect(find.text('Nuevo artículo'), findsNothing);
    await tester.tap(find.text('Cuaderno escolar'));
    await tester.pumpAndSettle();
    expect(find.text('10 unidades'), findsOneWidget);
    for (final action in ['Entrada', 'Salida', 'Conteo físico', 'Editar artículo']) {
      expect(find.text(action), findsNothing);
    }
    await tester.pumpWidget(const SizedBox.shrink());
  });
  testWidgets('a failed refresh clears old inventory instead of showing previous units', (
    tester,
  ) async {
    await prepare(tester, const InventoryPage());
    failList = true;
    await tester.tap(find.byTooltip('Buscar'));
    await tester.pumpAndSettle();
    expect(find.text('Cuaderno escolar'), findsNothing);
    expect(find.text('No se pudo consultar inventario'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });
  for (final variant in [(320.0, 1.0, false), (390.0, 1.5, true)]) {
    testWidgets('inventory fits ${variant.$1} px at scale ${variant.$2} dark=${variant.$3}', (
      tester,
    ) async {
      tester.view.devicePixelRatio = 1;
      tester.view.physicalSize = Size(variant.$1, 750);
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await prepare(tester, const InventoryPage(), dark: variant.$3, scale: variant.$2);
      expect(tester.takeException(), isNull);
      await tester.scrollUntilVisible(
        find.text('Cuaderno escolar'),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('Cuaderno escolar'));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(find.text('Conteo físico'));
      await tester.tap(find.text('Conteo físico'));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
    });
  }
}
