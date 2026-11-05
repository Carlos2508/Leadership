import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_platform_app/app/router/app_router.dart';
import 'package:school_platform_app/data/demo/demo_database.dart';
import 'package:school_platform_app/data/repository_providers.dart';
import 'package:school_platform_app/domain/models/assignments.dart';
import 'package:school_platform_app/features/auth/application/auth_controller.dart';

import '../helpers/test_app.dart';

void main() {
  setUpAll(initTestEnvironment);

  testWidgets('ingreso con perfil demo desde la pantalla de acceso', (tester) async {
    await pumpApp(tester);
    expect(find.text('¡Qué gusto verte!'), findsOneWidget);

    // Sin datos muestra validaciones.
    await tester.ensureVisible(find.text('Iniciar sesión'));
    await tester.tap(find.text('Iniciar sesión'));
    await tester.pumpAndSettle();
    expect(find.text('Escribe tu usuario.'), findsOneWidget);

    await tester.tap(find.text('Estudiante'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Iniciar sesión'));
    await tester.tap(find.text('Iniciar sesión'));
    await tester.pumpAndSettle();
    expect(find.text('¡Hola, Sofía!'), findsOneWidget);
  });

  testWidgets('credenciales incorrectas muestran un error genérico', (tester) async {
    await pumpApp(tester);
    await tester.enterText(find.byType(TextFormField).at(0), 'docente.demo');
    await tester.enterText(find.byType(TextFormField).at(1), '123');
    await tester.ensureVisible(find.text('Iniciar sesión'));
    await tester.tap(find.text('Iniciar sesión'));
    await tester.pumpAndSettle();
    expect(find.text('Usuario o contraseña incorrectos.'), findsOneWidget);
  });

  testWidgets('la estudiante entrega una tarea', (tester) async {
    final container = await pumpApp(tester);
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'estudiante.demo', password: 'demo1234');
    await tester.pumpAndSettle();
    unawaited(container.read(routerProvider).push('/tasks/asg-102'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Entregar tarea'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirmar entrega'));
    await tester.pumpAndSettle();
    expect(find.text('Adjunta al menos un archivo.'), findsOneWidget);

    await tester.tap(find.text('Foto'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirmar entrega'));
    await tester.pumpAndSettle();

    expect(find.textContaining('¡Tarea entregada!'), findsOneWidget);
    expect(find.text('Entregar tarea'), findsNothing);
    final task = await container.read(academicRepositoryProvider).fetchAssignment('asg-102');
    expect(task.status, AssignmentStatus.submitted);
  });

  testWidgets('familia: notas bloqueadas hasta que caja valida el comprobante', (tester) async {
    final container = await pumpApp(tester);
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'familia.demo', password: 'demo1234');
    await tester.pumpAndSettle();

    await tester.tap(find.text('Notas'));
    await tester.pumpAndSettle();
    // Primer hijo (solvente) muestra notas.
    expect(find.text('Mis materias'), findsOneWidget);

    // Segundo hijo con cuota pendiente: no se muestran notas.
    await tester.tap(find.text('Diego'));
    await tester.pumpAndSettle();
    expect(find.textContaining('están publicadas'), findsOneWidget);
    expect(find.text('Mis materias'), findsNothing);
    expect(find.text('Cuota 05'), findsOneWidget);

    // Caja acepta el comprobante (otro perfil, mismo servidor simulado) y la
    // familia actualiza deslizando hacia abajo.
    await container
        .read(financeRepositoryProvider)
        .reviewEvidence('evd-001', accept: true, reviewer: 'Caja');
    await tester.fling(find.byType(Scrollable).first, const Offset(0, 400), 1000);
    await tester.pumpAndSettle();
    expect(find.text('Mis materias'), findsOneWidget);
    await tester.tap(find.text('Inicio'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Inicio'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Notas'));
    await tester.pumpAndSettle();
    expect(find.text('Mis materias'), findsOneWidget);
  });

  testWidgets('caja registra un pago y emite recibo', (tester) async {
    final container = await pumpApp(tester, size: const Size(390, 1200));
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'caja.demo', password: 'demo1234');
    await tester.pumpAndSettle();
    unawaited(container.read(routerProvider).push('/accounts/${DemoDatabase.siblingId}/pay'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Revisar pago'));
    await tester.pumpAndSettle();
    expect(find.text('3. Revisa la distribución'), findsOneWidget);
    await tester.tap(find.text('Confirmar y emitir recibo'));
    await tester.pumpAndSettle();

    expect(find.text('Pago confirmado'), findsOneWidget);
    expect(find.text('Al día para la publicación vigente'), findsOneWidget);
  });

  testWidgets('docente: la libreta valida el rango antes de guardar', (tester) async {
    final container = await pumpApp(tester);
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'docente.demo', password: 'demo1234');
    await tester.pumpAndSettle();
    unawaited(container.read(routerProvider).push('/groups/grp-1a'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Libreta'));
    await tester.pumpAndSettle();
    final field = find.byType(TextFormField).first;
    await tester.enterText(field, '150');
    await tester.pumpAndSettle();
    expect(find.text('0–100'), findsOneWidget);

    await tester.enterText(field, '88.5');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Guardar borrador'));
    await tester.pumpAndSettle();
    expect(find.text('Borrador guardado. No se publicó a familias.'), findsOneWidget);
  });

  testWidgets('docente: salir con cambios sin guardar pide confirmación', (tester) async {
    final container = await pumpApp(tester);
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'docente.demo', password: 'demo1234');
    await tester.pumpAndSettle();
    final router = container.read(routerProvider);
    unawaited(router.push('/groups/grp-1a'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Marcar todos presentes'));
    await tester.pumpAndSettle();
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(find.text('¿Descartar cambios?'), findsOneWidget);
    await tester.tap(find.text('Descartar'));
    await tester.pumpAndSettle();
    expect(router.state.matchedLocation, '/teacher');
  });

  testWidgets('modo de errores simulados muestra reintento', (tester) async {
    final container = await pumpApp(tester);
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'direccion.demo', password: 'demo1234');
    await tester.pumpAndSettle();
    container.read(simulateFailuresProvider.notifier).toggle(true);
    await tester.tap(find.text('Cuentas'));
    await tester.pumpAndSettle();
    expect(find.text('Reintentar'), findsOneWidget);

    container.read(simulateFailuresProvider.notifier).toggle(false);
    await tester.tap(find.text('Reintentar'));
    await tester.pumpAndSettle();
    expect(find.text('Reintentar'), findsNothing);
    expect(find.text('Accesos de la comunidad'), findsOneWidget);
  });

  testWidgets('administración publica un comunicado visible en avisos', (tester) async {
    final container = await pumpApp(tester);
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'direccion.demo', password: 'demo1234');
    await tester.pumpAndSettle();
    unawaited(container.read(routerProvider).push('/announcements/new'));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextFormField).at(0), 'Simulacro de evacuación');
    await tester.enterText(
      find.byType(TextFormField).at(1),
      'El viernes realizaremos un simulacro a las 10:00.',
    );
    await tester.tap(find.text('Publicar').last);
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Publicar').last);
    await tester.pumpAndSettle();
    expect(find.text('Comunicado publicado.'), findsOneWidget);

    final notices = await container
        .read(communicationRepositoryProvider)
        .fetchNotices(container.read(authControllerProvider)!.role);
    expect(
      notices.any((n) => n.title == 'Simulacro de evacuación'),
      isFalse,
      reason: 'audiencia: familias',
    );
  });
}
