import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_platform_app/core/config/institution_config.dart';

import '../helpers/test_app.dart';

Map<String, Object?> _json({String? displayName = 'Colegio', String primary = '#234C6F'}) => {
  'Institution': {
    'SchoolId': 'x',
    'DisplayName': ?displayName,
    'ShortName': 'C',
    'Contact': <String, Object?>{},
    'Assets': {'MainLogo': 'a.png', 'LoginLogo': 'b.png'},
    'Theme': {'Deep': '#18324A', 'Primary': primary, 'Accent': '#ECA42E', 'Surface': '#F5F8FA'},
    'Locale': {'Culture': 'es-GT', 'TimeZone': 'America/Guatemala', 'Currency': 'GTQ'},
    'Terms': {
      'StudentPlural': 'Alumnos',
      'GuardianPlural': 'Familias',
      'Tuition': 'Mensualidad',
      'Period': 'Bimestre',
    },
  },
};

void main() {
  test('lee una configuración válida y sus colores', () {
    final config = testInstitution();
    expect(config.shortName, 'Mi colegio');
    expect(config.palette.primary, const Color(0xFF234C6F));
    expect(config.contact.isEmpty, isTrue);
    expect(config.terms.tuition, 'Mensualidad');
  });

  test('falla si falta un dato obligatorio en lugar de inventarlo', () {
    expect(
      () => InstitutionConfig.fromJson(_json(displayName: null)),
      throwsA(
        isA<InstitutionConfigException>().having(
          (e) => e.problems.join(),
          'problemas',
          contains('DisplayName'),
        ),
      ),
    );
  });

  test('rechaza colores con formato inválido', () {
    expect(
      () => InstitutionConfig.fromJson(_json(primary: 'verde')),
      throwsA(isA<InstitutionConfigException>()),
    );
  });
}
