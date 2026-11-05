/// Plural sencillo en español para términos configurables
/// ("Colegiatura" → "Colegiaturas", "Mensualidad" → "Mensualidades").
String pluralizeEs(String word) {
  final trimmed = word.trim();
  if (trimmed.isEmpty) return trimmed;
  final last = trimmed[trimmed.length - 1].toLowerCase();
  if ('aeiouáéó'.contains(last)) return '${trimmed}s';
  if (last == 'z') return '${trimmed.substring(0, trimmed.length - 1)}ces';
  if (last == 's') return trimmed;
  return '${trimmed}es';
}
