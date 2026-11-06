import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/providers.dart';
import '../core/theme/app_theme.dart';
import '../features/connected/biometric_lock.dart';
import 'router/app_router.dart';

class SchoolApp extends ConsumerWidget {
  const SchoolApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final institution = ref.watch(institutionProvider);
    final router = ref.watch(routerProvider);
    final themeMode = ref.watch(themeModeProvider);
    return MaterialApp.router(
      onGenerateTitle: (_) => institution.shortName,
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(institution.palette),
      darkTheme: AppTheme.dark(institution.palette),
      themeMode: themeMode,
      routerConfig: router,
      builder: (context, child) => BiometricLock(
        onPassword: () => router.go('/connect'),
        child: child ?? const SizedBox.shrink(),
      ),
      locale: const Locale('es'),
      supportedLocales: const [Locale('es')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
    );
  }
}
