import 'package:flutter/material.dart';

import 'institution_config.dart';

/// Pantalla neutra que se muestra cuando falta o es inválida la configuración.
/// No usa colores ni textos de ninguna institución.
class ConfigErrorApp extends StatelessWidget {
  const ConfigErrorApp({required this.error, super.key});

  final Object error;

  @override
  Widget build(BuildContext context) {
    final problems = error is InstitutionConfigException
        ? (error as InstitutionConfigException).problems
        : [error.toString()];
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      theme: ThemeData(useMaterial3: true, colorSchemeSeed: Colors.blueGrey),
      home: Scaffold(
        body: SafeArea(
          child: ListView(
            padding: const EdgeInsets.all(24),
            children: [
              const Icon(Icons.settings_suggest_rounded, size: 48),
              const SizedBox(height: 16),
              Text(
                'Falta configurar la institución',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 12),
              const Text(
                'La app toma nombre, logos y colores de config/institution.json. '
                'Desde la carpeta mobile/ ejecuta:',
              ),
              const SizedBox(height: 12),
              const SelectableText(
                'dart run tool/sync_institution.dart',
                style: TextStyle(fontFamily: 'monospace', fontWeight: FontWeight.w600),
              ),
              const SizedBox(height: 20),
              for (final problem in problems)
                Padding(padding: const EdgeInsets.only(bottom: 8), child: Text('• $problem')),
            ],
          ),
        ),
      ),
    );
  }
}
