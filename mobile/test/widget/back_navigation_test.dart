import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_platform_app/app/router/app_router.dart';
import 'package:school_platform_app/features/auth/application/auth_controller.dart';
import 'package:school_platform_app/shared/back_navigation.dart';

import '../helpers/test_app.dart';

/// Registra si la app pidió cerrarse (SystemNavigator.pop).
bool _watchExit(WidgetTester tester) {
  var exited = false;
  tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(SystemChannels.platform, (
    call,
  ) async {
    if (call.method == 'SystemNavigator.pop') exited = true;
    return null;
  });
  addTearDown(
    () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
      SystemChannels.platform,
      null,
    ),
  );
  return exited;
}

Future<void> _systemBack(WidgetTester tester) async {
  await tester.binding.handlePopRoute();
  await tester.pumpAndSettle();
}

Future<ProviderContainer> _signedIn(WidgetTester tester, String username) async {
  final container = await pumpApp(tester);
  await container
      .read(authControllerProvider.notifier)
      .signIn(username: username, password: 'demo1234');
  await tester.pumpAndSettle();
  return container;
}

String _location(ProviderContainer c) => c.read(routerProvider).state.matchedLocation;

/// Deja expirar el aviso de salida para no dejar temporizadores pendientes.
Future<void> _flushSnackBars(WidgetTester tester) async {
  await tester.pump(const Duration(seconds: 3));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(initTestEnvironment);

  testWidgets('atrás en una pestaña vuelve a Inicio y solo sale con doble pulsación', (
    tester,
  ) async {
    var exited = false;
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(SystemChannels.platform, (
      call,
    ) async {
      if (call.method == 'SystemNavigator.pop') exited = true;
      return null;
    });
    addTearDown(
      () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        SystemChannels.platform,
        null,
      ),
    );

    final container = await _signedIn(tester, 'estudiante.demo');
    await tester.tap(find.text('Notas'));
    await tester.pumpAndSettle();
    expect(_location(container), '/student/grades');

    await _systemBack(tester);
    expect(_location(container), '/student');
    expect(exited, isFalse);

    await _systemBack(tester);
    expect(find.text(ExitOnDoubleBack.message), findsOneWidget);
    expect(exited, isFalse);
    expect(_location(container), '/student');

    await _systemBack(tester);
    expect(exited, isTrue);
    await _flushSnackBars(tester);
  });

  testWidgets('atrás desde un detalle regresa a la pestaña de origen', (tester) async {
    _watchExit(tester);
    final container = await _signedIn(tester, 'estudiante.demo');
    await tester.tap(find.text('Agenda'));
    await tester.pumpAndSettle();
    unawaited(container.read(routerProvider).push('/tasks/asg-101'));
    await tester.pumpAndSettle();
    expect(_location(container), '/tasks/asg-101');

    await _systemBack(tester);
    expect(_location(container), '/student/agenda');
    expect(find.text('A tu ritmo, sin olvidar nada'), findsOneWidget);
  });

  testWidgets('la pantalla de ingreso no se cierra con un solo atrás', (tester) async {
    var exited = false;
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(SystemChannels.platform, (
      call,
    ) async {
      if (call.method == 'SystemNavigator.pop') exited = true;
      return null;
    });
    addTearDown(
      () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        SystemChannels.platform,
        null,
      ),
    );
    final container = await pumpApp(tester);
    await _systemBack(tester);
    expect(_location(container), '/login');
    expect(exited, isFalse);
    expect(find.text(ExitOnDoubleBack.message), findsOneWidget);

    // Recuperar acceso se abre encima y atrás vuelve al ingreso.
    await tester.ensureVisible(find.text('¿Olvidaste tu contraseña o tienes un código?'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('¿Olvidaste tu contraseña o tienes un código?'));
    await tester.pumpAndSettle();
    expect(_location(container), '/recover');
    await _systemBack(tester);
    expect(_location(container), '/login');
    expect(exited, isFalse);
    await _flushSnackBars(tester);
  });

  testWidgets('formulario con datos pide confirmar antes de salir', (tester) async {
    _watchExit(tester);
    final container = await _signedIn(tester, 'familia.demo');
    unawaited(container.read(routerProvider).push('/evidence/stu-002'));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextFormField).at(1), 'TRF-123');
    await tester.pumpAndSettle();
    await _systemBack(tester);
    expect(find.text('¿Descartar cambios?'), findsOneWidget);

    await tester.tap(find.text('Seguir editando'));
    await tester.pumpAndSettle();
    expect(_location(container), '/evidence/stu-002');

    await _systemBack(tester);
    await tester.tap(find.text('Descartar'));
    await tester.pumpAndSettle();
    expect(_location(container), '/family');
  });

  testWidgets('la hoja de entrega con archivos confirma antes de cerrarse', (tester) async {
    _watchExit(tester);
    final container = await _signedIn(tester, 'estudiante.demo');
    unawaited(container.read(routerProvider).push('/tasks/asg-103'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Entregar tarea'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Archivo'));
    await tester.pumpAndSettle();

    await _systemBack(tester);
    expect(find.text('¿Descartar cambios?'), findsOneWidget);
    await tester.tap(find.text('Descartar'));
    await tester.pumpAndSettle();
    expect(find.text('Confirmar entrega'), findsNothing);
    expect(_location(container), '/tasks/asg-103');
  });

  testWidgets('volver a tocar la pestaña activa sube al inicio', (tester) async {
    _watchExit(tester);
    await _signedIn(tester, 'direccion.demo');
    await tester.tap(find.text('Estudiantes'));
    await tester.pumpAndSettle();
    final scrollable = find.byType(Scrollable).first;
    await tester.drag(scrollable, const Offset(0, -1500));
    await tester.pumpAndSettle();
    final position = tester.state<ScrollableState>(scrollable).position;
    expect(position.pixels, greaterThan(0));

    await tester.tap(find.text('Estudiantes').last);
    await tester.pumpAndSettle();
    expect(position.pixels, 0);
  });

  testWidgets('el diálogo y la hoja se cierran con atrás sin salir de la pantalla', (tester) async {
    _watchExit(tester);
    final container = await _signedIn(tester, 'direccion.demo');
    await tester.tap(find.text('Cuentas'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Carmen Ortiz'));
    await tester.pumpAndSettle();
    expect(find.text('Generar código de activación'), findsOneWidget);

    await _systemBack(tester);
    expect(find.text('Generar código de activación'), findsNothing);
    expect(_location(container), '/admin/accounts');
  });
}
