import 'package:flutter_test/flutter_test.dart';
import 'package:school_platform_app/features/connected/academic_page.dart';

void main() {
  test('splitCalendar matches the server: contiguous periods covering the whole cycle', () {
    final periods = splitCalendar(DateTime(2026, 1, 12), DateTime(2026, 10, 30), 4);
    expect(periods, hasLength(4));
    expect(periods.first.$1, DateTime.utc(2026, 1, 12));
    expect(periods.last.$2, DateTime.utc(2026, 10, 30));
    for (var i = 1; i < periods.length; i++) {
      expect(periods[i].$1, periods[i - 1].$2.add(const Duration(days: 1)));
    }
    final lengths = [for (final p in periods) p.$2.difference(p.$1).inDays];
    expect(
      lengths.reduce((a, b) => a > b ? a : b) - lengths.reduce((a, b) => a < b ? a : b),
      lessThanOrEqualTo(1),
    );
    expect(splitCalendar(DateTime(2026, 3, 2), DateTime(2026, 3, 3), 3), isEmpty);
  });

  test('nextCycleLabel moves the year forward and leaves other labels empty', () {
    expect(nextCycleLabel('2026'), '2027');
    expect(nextCycleLabel('Ciclo 2026 · Primaria'), 'Ciclo 2027 · Primaria');
    expect(nextCycleLabel('Ciclo A'), '');
  });
}
