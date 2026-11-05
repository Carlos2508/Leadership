import 'dart:convert';

import 'package:flutter/services.dart';

import 'institution_config.dart';

/// Ruta del asset generado por `tool/sync_institution.dart`.
const institutionAssetPath = 'assets/institution/institution.json';

/// Carga y valida la configuración institucional empaquetada.
///
/// Nunca recurre a valores predeterminados: si falta o es inválida, la app
/// muestra una pantalla de configuración en lugar de inventar una marca.
Future<InstitutionConfig> loadInstitutionConfig(AssetBundle bundle) async {
  final String raw;
  try {
    raw = await bundle.loadString(institutionAssetPath, cache: false);
  } on Object {
    throw const InstitutionConfigException([
      'No se encontró la configuración institucional. '
          'Ejecuta "dart run tool/sync_institution.dart" desde mobile/.',
    ]);
  }
  final Object? decoded;
  try {
    decoded = jsonDecode(raw);
  } on FormatException catch (error) {
    throw InstitutionConfigException(['JSON inválido: ${error.message}']);
  }
  if (decoded is! Map<String, Object?>) {
    throw const InstitutionConfigException(['El JSON debe ser un objeto.']);
  }
  return InstitutionConfig.fromJson(decoded);
}
