// Referencia de tokens Flutter; no constituye una app Flutter completa.
import 'package:flutter/material.dart';

class LeadershipTheme {
  static const forest = Color(0xFF073B25);
  static const primary = Color(0xFF0B5835);
  static const gold = Color(0xFFECA42E);
  static const mint = Color(0xFFE6F5EC);
  static const background = Color(0xFFF7F9F5);
  static ThemeData light() => ThemeData(
    useMaterial3: true,
    scaffoldBackgroundColor: background,
    colorScheme: ColorScheme.fromSeed(seedColor: primary, primary: primary, secondary: gold),
    appBarTheme: const AppBarTheme(backgroundColor: background, foregroundColor: forest),
    cardTheme: const CardThemeData(color: Colors.white, elevation: 0),
    inputDecorationTheme: InputDecorationTheme(
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(14)),
    ),
  );
}
