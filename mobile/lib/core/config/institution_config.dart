import 'package:flutter/material.dart';

/// Error de validación de la configuración institucional.
class InstitutionConfigException implements Exception {
  const InstitutionConfigException(this.problems);

  final List<String> problems;

  @override
  String toString() => 'Configuración institucional inválida: ${problems.join('; ')}';
}

/// Proyección pública de `config/institution.json` usada por la app.
///
/// Ningún nombre, color o texto institucional vive en el código: todo
/// proviene de esta configuración, igual que en la web.
@immutable
class InstitutionConfig {
  const InstitutionConfig({
    required this.schoolId,
    required this.displayName,
    required this.shortName,
    required this.campusLabel,
    required this.tagline,
    required this.welcomeMessage,
    required this.contact,
    required this.mainLogo,
    required this.loginLogo,
    required this.palette,
    required this.locale,
    required this.terms,
  });

  factory InstitutionConfig.fromJson(Map<String, Object?> json) {
    final root = json['Institution'];
    if (root is! Map<String, Object?>) {
      throw const InstitutionConfigException(['Falta el objeto Institution.']);
    }
    final reader = _Reader(root);
    final contact = reader.section('Contact');
    final assets = reader.section('Assets');
    final theme = reader.section('Theme');
    final locale = reader.section('Locale');
    final terms = reader.section('Terms');

    final config = InstitutionConfig(
      schoolId: reader.requiredString('SchoolId'),
      displayName: reader.requiredString('DisplayName'),
      shortName: reader.requiredString('ShortName'),
      campusLabel: reader.optionalString('CampusLabel'),
      tagline: reader.optionalString('Tagline'),
      welcomeMessage: reader.optionalString('WelcomeMessage'),
      contact: InstitutionContact(
        phone: contact.optionalString('Phone'),
        email: contact.optionalString('Email'),
        address: contact.optionalString('Address'),
        website: contact.optionalString('Website'),
      ),
      mainLogo: assets.requiredString('MainLogo'),
      loginLogo: assets.requiredString('LoginLogo'),
      palette: BrandPalette(
        deep: theme.requiredColor('Deep'),
        primary: theme.requiredColor('Primary'),
        accent: theme.requiredColor('Accent'),
        surface: theme.requiredColor('Surface'),
      ),
      locale: InstitutionLocale(
        culture: locale.requiredString('Culture'),
        timeZone: locale.requiredString('TimeZone'),
        currency: locale.requiredString('Currency'),
      ),
      terms: InstitutionTerms(
        studentPlural: terms.requiredString('StudentPlural'),
        guardianPlural: terms.requiredString('GuardianPlural'),
        tuition: terms.requiredString('Tuition'),
        period: terms.requiredString('Period'),
      ),
    );

    final problems = [
      ...reader.problems,
      ...contact.problems,
      ...assets.problems,
      ...theme.problems,
      ...locale.problems,
      ...terms.problems,
    ];
    if (problems.isNotEmpty) {
      throw InstitutionConfigException(problems);
    }
    return config;
  }

  final String schoolId;
  final String displayName;
  final String shortName;
  final String campusLabel;
  final String tagline;
  final String welcomeMessage;
  final InstitutionContact contact;

  /// Rutas de assets locales (ya resueltas por la herramienta de sincronización).
  final String mainLogo;
  final String loginLogo;
  final BrandPalette palette;
  final InstitutionLocale locale;
  final InstitutionTerms terms;
}

@immutable
class InstitutionContact {
  const InstitutionContact({
    required this.phone,
    required this.email,
    required this.address,
    required this.website,
  });

  final String phone;
  final String email;
  final String address;
  final String website;

  bool get isEmpty => phone.isEmpty && email.isEmpty && address.isEmpty && website.isEmpty;
}

@immutable
class BrandPalette {
  const BrandPalette({
    required this.deep,
    required this.primary,
    required this.accent,
    required this.surface,
  });

  final Color deep;
  final Color primary;
  final Color accent;
  final Color surface;
}

@immutable
class InstitutionLocale {
  const InstitutionLocale({required this.culture, required this.timeZone, required this.currency});

  final String culture;
  final String timeZone;
  final String currency;
}

@immutable
class InstitutionTerms {
  const InstitutionTerms({
    required this.studentPlural,
    required this.guardianPlural,
    required this.tuition,
    required this.period,
  });

  final String studentPlural;
  final String guardianPlural;
  final String tuition;
  final String period;
}

class _Reader {
  _Reader(this._json, [this._path = '']);

  final Map<String, Object?> _json;
  final String _path;
  final List<String> problems = [];

  _Reader section(String key) {
    final value = _json[key];
    if (value is Map<String, Object?>) {
      return _Reader(value, '$_path$key.');
    }
    problems.add('Falta la sección $_path$key.');
    return _Reader(const {}, '$_path$key.');
  }

  String requiredString(String key) {
    final value = _json[key];
    if (value is String && value.trim().isNotEmpty) {
      return value.trim();
    }
    problems.add('$_path$key es obligatorio.');
    return '';
  }

  String optionalString(String key) {
    final value = _json[key];
    return value is String ? value.trim() : '';
  }

  Color requiredColor(String key) {
    final raw = _json[key];
    final match = raw is String ? RegExp(r'^#([0-9a-fA-F]{6})$').firstMatch(raw.trim()) : null;
    if (match == null) {
      problems.add('$_path$key debe tener formato #RRGGBB.');
      return const Color(0xFF000000);
    }
    return Color(0xFF000000 | int.parse(match.group(1)!, radix: 16));
  }
}
