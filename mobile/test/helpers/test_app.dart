import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:school_platform_app/app/app.dart';
import 'package:school_platform_app/core/config/institution_config.dart';
import 'package:school_platform_app/core/providers.dart';
import 'package:school_platform_app/data/repository_providers.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Fecha fija (miércoles) para pruebas deterministas.
final testNow = DateTime(2026, 11, 4, 10, 30);

/// Configuración neutra, equivalente a docs/examples/institution.example.json.
InstitutionConfig testInstitution() => InstitutionConfig.fromJson(const {
  'Institution': {
    'SchoolId': '8ec97557-d19c-4e03-acba-8af401c2394f',
    'DisplayName': 'Colegio de ejemplo',
    'ShortName': 'Mi colegio',
    'CampusLabel': 'Comunidad educativa',
    'Tagline': 'Aprendemos juntos',
    'WelcomeMessage': 'Una comunidad para aprender y crecer.',
    'Contact': {'Phone': '', 'Email': '', 'Address': '', 'Website': ''},
    'Assets': {'MainLogo': 'assets/test/missing.png', 'LoginLogo': 'assets/test/missing.png'},
    'Theme': {'Deep': '#18324A', 'Primary': '#234C6F', 'Accent': '#ECA42E', 'Surface': '#F5F8FA'},
    'Locale': {'Culture': 'es-GT', 'TimeZone': 'America/Guatemala', 'Currency': 'GTQ'},
    'Terms': {
      'StudentPlural': 'Alumnos',
      'GuardianPlural': 'Familias',
      'Tuition': 'Mensualidad',
      'Period': 'Bimestre',
    },
  },
});

Future<void> initTestEnvironment() async {
  await initializeDateFormatting();
}

/// Contenedor de Riverpod con dependencias de prueba.
Future<ProviderContainer> createTestContainer({
  Map<String, Object> prefs = const {},
}) async {
  SharedPreferences.setMockInitialValues(prefs);
  final preferences = await SharedPreferences.getInstance();
  return ProviderContainer(
    retry: (_, _) => null,
    overrides: [
      institutionProvider.overrideWithValue(testInstitution()),
      sharedPreferencesProvider.overrideWithValue(preferences),
      clockProvider.overrideWithValue(() => testNow),
      demoLatencyProvider.overrideWithValue(Duration.zero),
    ],
  );
}

/// Monta la app completa con un tamaño de pantalla dado.
Future<ProviderContainer> pumpApp(
  WidgetTester tester, {
  Size size = const Size(390, 844),
  Map<String, Object> prefs = const {'preferences.welcomeSeen': true},
  ThemeMode themeMode = ThemeMode.light,
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final container = await createTestContainer(
    prefs: {...prefs, 'preferences.themeMode': themeMode.name},
  );
  addTearDown(container.dispose);
  await tester.pumpWidget(
    UncontrolledProviderScope(container: container, child: const SchoolApp()),
  );
  await tester.pumpAndSettle();
  return container;
}
