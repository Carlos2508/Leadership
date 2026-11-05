import 'package:flutter_test/flutter_test.dart';
import 'package:school_platform_app/core/format/formatters.dart';
import 'package:school_platform_app/core/utils/terms.dart';
import 'package:school_platform_app/core/utils/text_search.dart';

import '../helpers/test_app.dart';

void main() {
  setUpAll(initTestEnvironment);

  test('formatea moneda y fechas con la región configurada', () {
    final format = Formatters(testInstitution().locale);
    expect(format.currency(1250.5), 'Q1,250.50');
    expect(format.localeName, 'es_419');
    expect(format.currencySymbol, 'Q');
    expect(format.shortDate(DateTime(2026, 3, 9)), '09/03/2026');
    expect(format.weekdayDayMonth(DateTime(2026, 11, 4)), 'miércoles, 4 de noviembre');
  });

  test('fechas relativas', () {
    final format = Formatters(testInstitution().locale);
    final today = DateTime(2026, 11, 4, 10);
    expect(format.relativeDay(DateTime(2026, 11, 4, 23), today), 'Hoy');
    expect(format.relativeDay(DateTime(2026, 11, 5), today), 'Mañana');
    expect(format.relativeDay(DateTime(2026, 11, 3), today), 'Ayer');
  });

  test('plural de términos configurables', () {
    expect(pluralizeEs('Colegiatura'), 'Colegiaturas');
    expect(pluralizeEs('Mensualidad'), 'Mensualidades');
    expect(pluralizeEs('Cruz'), 'Cruces');
  });

  test('búsqueda sin tildes ni mayúsculas y por varios términos', () {
    expect(matchesSearch('Sofía Ramírez EST-001', 'sofia ramirez'), isTrue);
    expect(matchesSearch('Sofía Ramírez', 'ramírez sof'), isTrue);
    expect(matchesSearch('Sofía Ramírez', 'diego'), isFalse);
    expect(matchesSearch('cualquier cosa', '   '), isTrue);
  });
}
