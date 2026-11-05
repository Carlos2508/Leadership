// Copia la configuración institucional pública hacia los assets de la app.
//
// Fuente única: `config/institution.json` (más `config/institution.local.json`
// si existe). Los logos se toman de `src/SchoolPlatform.Host/wwwroot`.
// Los campos de `Documents` son exclusivos del servidor y no se copian.
//
// Uso (desde `mobile/`): dart run tool/sync_institution.dart
import 'dart:convert';
import 'dart:io';

const _publicSections = <String>{
  'SchoolId',
  'DisplayName',
  'ShortName',
  'CampusLabel',
  'LegalName',
  'Tagline',
  'WelcomeMessage',
  'Contact',
  'Assets',
  'Theme',
  'Locale',
  'Terms',
};

void main(List<String> args) {
  final mobileDir = Directory.current;
  final repoRoot = mobileDir.parent;
  final configFile = File('${repoRoot.path}/config/institution.json');
  final localFile = File('${repoRoot.path}/config/institution.local.json');
  final webRoot = '${repoRoot.path}/src/SchoolPlatform.Host/wwwroot';

  if (!configFile.existsSync()) {
    stderr.writeln('No existe ${configFile.path}. Ejecuta el script desde mobile/.');
    exitCode = 1;
    return;
  }

  var merged = _readInstitution(configFile);
  if (localFile.existsSync()) {
    merged = _deepMerge(merged, _readInstitution(localFile));
    stdout.writeln('Aplicada sobrescritura local: ${localFile.path}');
  }

  final public = <String, Object?>{
    for (final entry in merged.entries)
      if (_publicSections.contains(entry.key)) entry.key: entry.value,
  };

  final outDir = Directory('${mobileDir.path}/assets/institution');
  final brandingDir = Directory('${outDir.path}/branding');
  brandingDir.createSync(recursive: true);

  final assets = public['Assets'];
  if (assets is! Map<String, Object?>) {
    stderr.writeln('La configuración no contiene la sección Assets.');
    exitCode = 1;
    return;
  }

  final mobileAssets = <String, Object?>{};
  for (final key in const ['MainLogo', 'LoginLogo']) {
    final webPath = assets[key];
    if (webPath is! String || webPath.isEmpty) {
      stderr.writeln('Falta Assets.$key en la configuración.');
      exitCode = 1;
      return;
    }
    final source = File('$webRoot$webPath');
    if (!source.existsSync()) {
      stderr.writeln('No existe el recurso ${source.path} para Assets.$key.');
      exitCode = 1;
      return;
    }
    final fileName = webPath.split('/').last;
    source.copySync('${brandingDir.path}/$fileName');
    mobileAssets[key] = 'assets/institution/branding/$fileName';
  }
  public['Assets'] = mobileAssets;

  const encoder = JsonEncoder.withIndent('  ');
  File('${outDir.path}/institution.json')
      .writeAsStringSync('${encoder.convert({'Institution': public})}\n');
  stdout.writeln('Configuración institucional sincronizada en ${outDir.path}.');
}

Map<String, Object?> _readInstitution(File file) {
  final decoded = jsonDecode(file.readAsStringSync());
  if (decoded is! Map<String, Object?> || decoded['Institution'] is! Map<String, Object?>) {
    throw FormatException('${file.path} no contiene un objeto Institution.');
  }
  return decoded['Institution']! as Map<String, Object?>;
}

Map<String, Object?> _deepMerge(Map<String, Object?> base, Map<String, Object?> override) {
  final result = Map<String, Object?>.of(base);
  override.forEach((key, value) {
    final current = result[key];
    if (current is Map<String, Object?> && value is Map<String, Object?>) {
      result[key] = _deepMerge(current, value);
    } else {
      result[key] = value;
    }
  });
  return result;
}
