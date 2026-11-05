import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_platform_app/app/router/app_router.dart';
import 'package:school_platform_app/domain/models/people.dart';
import 'package:school_platform_app/features/auth/application/auth_controller.dart';

import '../helpers/test_app.dart';

const _usernames = {
  UserRole.student: 'estudiante.demo',
  UserRole.guardian: 'familia.demo',
  UserRole.teacher: 'docente.demo',
  UserRole.admin: 'direccion.demo',
  UserRole.finance: 'caja.demo',
};

/// Rutas de detalle representativas por perfil.
const _detailRoutes = {
  UserRole.student: ['/tasks/asg-101', '/tasks/asg-106', '/courses/stu-001/mat', '/profile'],
  UserRole.guardian: ['/evidence/stu-002', '/courses/stu-002/cie', '/profile'],
  UserRole.teacher: ['/groups/grp-1a', '/reviews/rev-01', '/profile'],
  UserRole.admin: ['/students/stu-001', '/enrollments/enr-01', '/announcements/new', '/profile'],
  UserRole.finance: ['/accounts/stu-002', '/accounts/stu-002/pay', '/profile'],
};

void main() {
  setUpAll(initTestEnvironment);

  const sizes = {
    'teléfono': Size(390, 844),
    'teléfono pequeño': Size(320, 640),
    'tableta': Size(1024, 768),
  };

  for (final role in UserRole.values) {
    for (final entry in sizes.entries) {
      testWidgets('${role.label}: recorre pestañas y detalles en ${entry.key}', (tester) async {
        final container = await pumpApp(tester, size: entry.value);
        await container
            .read(authControllerProvider.notifier)
            .signIn(username: _usernames[role]!, password: 'demo1234');
        await tester.pumpAndSettle();

        final router = container.read(routerProvider);
        expect(router.state.matchedLocation, homePathFor(role));

        final destinations = entry.value.width < 720
            ? find.byType(NavigationDestination)
            : find.byType(NavigationRail);
        expect(destinations, findsWidgets);

        // Recorre cada pestaña y verifica que no haya errores de diseño.
        for (var i = 0; i < 5; i++) {
          if (entry.value.width < 720) {
            await tester.tap(find.byType(NavigationDestination).at(i));
          } else {
            final rail = tester.widget<NavigationRail>(find.byType(NavigationRail));
            rail.onDestinationSelected!(i);
          }
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
          await tester.drag(find.byType(Scrollable).first, const Offset(0, -2000));
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
        }

        for (final route in _detailRoutes[role]!) {
          unawaited(router.push(route));
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull, reason: route);
          expect(router.state.matchedLocation, isNot(homePathFor(role)), reason: route);
          router.pop();
          await tester.pumpAndSettle();
        }
      });
    }
  }

  testWidgets('modo oscuro: inicio de familia sin errores', (tester) async {
    final container = await pumpApp(tester, themeMode: ThemeMode.dark);
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'familia.demo', password: 'demo1234');
    await tester.pumpAndSettle();
    expect(find.text('Acompaña su aprendizaje'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('sin sesión redirige al ingreso y protege rutas', (tester) async {
    final container = await pumpApp(tester);
    final router = container.read(routerProvider);
    expect(router.state.matchedLocation, '/login');
    router.go('/admin/accounts');
    await tester.pumpAndSettle();
    expect(router.state.matchedLocation, '/login');
  });

  testWidgets('primer uso muestra la bienvenida', (tester) async {
    final container = await pumpApp(tester, prefs: const {});
    expect(container.read(routerProvider).state.matchedLocation, '/welcome');
    expect(find.text('Aprendemos juntos'), findsOneWidget);
    await tester.tap(find.text('Comenzar'));
    await tester.pumpAndSettle();
    expect(container.read(routerProvider).state.matchedLocation, '/login');
  });

  testWidgets('un perfil no puede abrir rutas de otro', (tester) async {
    final container = await pumpApp(tester);
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'estudiante.demo', password: 'demo1234');
    await tester.pumpAndSettle();
    final router = container.read(routerProvider);
    router.go('/finance/collect');
    await tester.pumpAndSettle();
    expect(router.state.matchedLocation, '/student');
    router.go('/students/stu-002');
    await tester.pumpAndSettle();
    expect(router.state.matchedLocation, '/student');
  });

  testWidgets('cerrar sesión vuelve al ingreso', (tester) async {
    final container = await pumpApp(tester);
    await container
        .read(authControllerProvider.notifier)
        .signIn(username: 'docente.demo', password: 'demo1234');
    await tester.pumpAndSettle();
    final router = container.read(routerProvider);
    unawaited(router.push('/profile'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Cerrar sesión'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cerrar sesión'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Cerrar sesión'));
    await tester.pumpAndSettle();
    expect(router.state.matchedLocation, '/login');
  });
}
