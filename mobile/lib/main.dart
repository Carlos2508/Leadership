import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app/app.dart';
import 'core/config/config_error_app.dart';
import 'core/config/institution_loader.dart';
import 'core/providers.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  try {
    // Carga los símbolos locales de fecha incluidos en intl (sin red).
    await initializeDateFormatting();
    final institution = await loadInstitutionConfig(rootBundle);
    final preferences = await SharedPreferences.getInstance();

    runApp(
      ProviderScope(
        // Los reintentos los decide la persona con el botón "Reintentar".
        retry: (_, _) => null,
        overrides: [
          institutionProvider.overrideWithValue(institution),
          sharedPreferencesProvider.overrideWithValue(preferences),
        ],
        child: const SchoolApp(),
      ),
    );
  } on Object catch (error) {
    runApp(ConfigErrorApp(error: error));
  }
}
