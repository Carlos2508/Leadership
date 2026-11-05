import 'package:intl/intl.dart';

import '../config/institution_config.dart';

/// Formatos de fecha, hora y moneda según la región configurada.
class Formatters {
  Formatters(InstitutionLocale locale)
    : localeName = _resolveLocale(locale.culture),
      currencyCode = locale.currency {
    _dayMonth = DateFormat("d 'de' MMMM", localeName);
    _weekdayDayMonth = DateFormat("EEEE, d 'de' MMMM", localeName);
    _shortDate = DateFormat('dd/MM/yyyy', localeName);
    _time = DateFormat('h:mm a', localeName);
    _weekdayShort = DateFormat('EEE', localeName);
    _monthYear = DateFormat('MMMM yyyy', localeName);
    _monthShort = DateFormat('MMM', localeName);
    _currency = NumberFormat.simpleCurrency(locale: localeName, name: currencyCode);
    _decimal = NumberFormat.decimalPatternDigits(locale: localeName, decimalDigits: 1);
  }

  final String localeName;
  final String currencyCode;

  late final DateFormat _dayMonth;
  late final DateFormat _weekdayDayMonth;
  late final DateFormat _shortDate;
  late final DateFormat _time;
  late final DateFormat _weekdayShort;
  late final DateFormat _monthYear;
  late final DateFormat _monthShort;
  late final NumberFormat _currency;
  late final NumberFormat _decimal;

  /// Resuelve la cultura configurada a un locale disponible en intl.
  /// Para español de Latinoamérica sin datos propios (p. ej. es-GT) usa es_419,
  /// que comparte separadores y formato de moneda (Q1,250.50).
  static String _resolveLocale(String culture) {
    bool exists(String l) => DateFormat.localeExists(l) && NumberFormat.localeExists(l);
    final normalized = culture.replaceAll('-', '_');
    final parts = normalized.split('_');
    final language = parts.first.toLowerCase();
    final candidates = [
      normalized,
      if (language == 'es' && parts.length > 1 && parts[1].toUpperCase() != 'ES') 'es_419',
      language,
      'es',
    ];
    return candidates.firstWhere(exists, orElse: () => 'es');
  }

  /// "4 de noviembre"
  String dayMonth(DateTime date) => _dayMonth.format(date);

  /// "miércoles, 4 de noviembre"
  String weekdayDayMonth(DateTime date) => _weekdayDayMonth.format(date);

  /// "04/11/2026"
  String shortDate(DateTime date) => _shortDate.format(date);

  /// "9:30 a. m."
  String time(DateTime date) => _time.format(date);

  /// "mié"
  String weekdayShort(DateTime date) => _cleanAbbrev(_weekdayShort.format(date));

  /// "Noviembre 2026"
  String monthYear(DateTime date) => _capitalize(_monthYear.format(date));

  /// "nov"
  String monthShort(DateTime date) => _cleanAbbrev(_monthShort.format(date));

  String currency(num amount) => _currency.format(amount);

  String get currencySymbol => _currency.currencySymbol;

  String decimal(num value) => _decimal.format(value);

  /// Etiqueta relativa: "Hoy", "Mañana", "Ayer" o "lun 10 nov".
  String relativeDay(DateTime date, DateTime today) {
    final diff = dateOnly(date).difference(dateOnly(today)).inDays;
    return switch (diff) {
      0 => 'Hoy',
      1 => 'Mañana',
      -1 => 'Ayer',
      _ => '${_capitalize(weekdayShort(date))} ${date.day} ${monthShort(date)}',
    };
  }

  /// "Hoy · 9:30 a. m."
  String relativeDateTime(DateTime date, DateTime now) =>
      '${relativeDay(date, now)} · ${time(date)}';

  static String _cleanAbbrev(String value) => value.replaceAll('.', '');

  static String _capitalize(String value) =>
      value.isEmpty ? value : value[0].toUpperCase() + value.substring(1);

  static String capitalize(String value) => _capitalize(value);
}

DateTime dateOnly(DateTime value) => DateTime(value.year, value.month, value.day);

bool isSameDay(DateTime a, DateTime b) => a.year == b.year && a.month == b.month && a.day == b.day;
