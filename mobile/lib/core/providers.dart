import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'config/institution_config.dart';
import 'format/formatters.dart';

/// Configuración institucional validada; se inyecta en `main` mediante override.
final institutionProvider = Provider<InstitutionConfig>(
  (ref) => throw UnimplementedError('institutionProvider debe sobrescribirse'),
);

/// Preferencias locales no sensibles (tema, bienvenida vista).
final sharedPreferencesProvider = Provider<SharedPreferences>(
  (ref) => throw UnimplementedError('sharedPreferencesProvider debe sobrescribirse'),
);

/// Reloj inyectable para que las pruebas sean deterministas.
final clockProvider = Provider<DateTime Function()>((ref) => DateTime.now);

final formattersProvider = Provider<Formatters>(
  (ref) => Formatters(ref.watch(institutionProvider).locale),
);

const _themeModeKey = 'preferences.themeMode';
const _welcomeSeenKey = 'preferences.welcomeSeen';

class ThemeModeController extends Notifier<ThemeMode> {
  @override
  ThemeMode build() {
    final stored = ref.watch(sharedPreferencesProvider).getString(_themeModeKey);
    return ThemeMode.values.firstWhere(
      (mode) => mode.name == stored,
      orElse: () => ThemeMode.system,
    );
  }

  Future<void> setMode(ThemeMode mode) async {
    state = mode;
    await ref.read(sharedPreferencesProvider).setString(_themeModeKey, mode.name);
  }
}

final themeModeProvider = NotifierProvider<ThemeModeController, ThemeMode>(ThemeModeController.new);

class WelcomeSeenController extends Notifier<bool> {
  @override
  bool build() => ref.watch(sharedPreferencesProvider).getBool(_welcomeSeenKey) ?? false;

  Future<void> markSeen() async {
    state = true;
    await ref.read(sharedPreferencesProvider).setBool(_welcomeSeenKey, true);
  }
}

final welcomeSeenProvider = NotifierProvider<WelcomeSeenController, bool>(
  WelcomeSeenController.new,
);
