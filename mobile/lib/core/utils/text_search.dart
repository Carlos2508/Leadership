/// Normaliza texto para búsquedas sin distinguir mayúsculas ni tildes.
String normalizeForSearch(String value) {
  const from = 'áéíóúüñàèìòùÁÉÍÓÚÜÑÀÈÌÒÙ';
  const to = 'aeiouunaeiouAEIOUUNAEIOU';
  final buffer = StringBuffer();
  for (final rune in value.trim().runes) {
    final char = String.fromCharCode(rune);
    final i = from.indexOf(char);
    buffer.write(i >= 0 ? to[i] : char);
  }
  return buffer.toString().toLowerCase();
}

/// `true` si todos los términos de [query] aparecen en [haystack].
bool matchesSearch(String haystack, String query) {
  final terms = normalizeForSearch(query).split(RegExp(r'\s+')).where((t) => t.isNotEmpty);
  if (terms.isEmpty) return true;
  final normalized = normalizeForSearch(haystack);
  return terms.every(normalized.contains);
}
