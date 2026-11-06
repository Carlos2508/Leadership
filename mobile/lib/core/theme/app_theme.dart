import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../config/institution_config.dart';
import 'app_colors.dart';

/// Espaciado base de 4 px del sistema de diseño.
abstract final class Spacing {
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 24;
  static const double xxl = 32;
  static const double page = 20;
}

abstract final class Radii {
  static const double sm = 10;
  static const double md = 16;
  static const double lg = 22;
  static const double xl = 30;
}

/// Ancho máximo del contenido en pantallas amplias.
const double kMaxContentWidth = 860;

abstract final class AppTheme {
  static ThemeData light(BrandPalette palette) => _build(palette, Brightness.light);

  static ThemeData dark(BrandPalette palette) => _build(palette, Brightness.dark);

  static ThemeData _build(BrandPalette palette, Brightness brightness) {
    final isDark = brightness == Brightness.dark;
    final colors = AppColors.from(palette, brightness);

    // Same system as the web: ivory content, white cards, charcoal text and gold actions (light);
    // charcoal surfaces with the same gold in dark mode. Brand colors come from institution.json only.
    final seeded = ColorScheme.fromSeed(seedColor: palette.accent, brightness: brightness);
    final ink = isDark ? const Color(0xFFF1EFEB) : const Color(0xFF16191C);
    final scheme = seeded.copyWith(
      primary: isDark ? palette.accent : palette.deep,
      onPrimary: isDark ? palette.deep : Colors.white,
      secondary: palette.accent,
      onSecondary: colors.onAccent,
      tertiary: palette.accent,
      surface: isDark ? palette.deep : palette.surface,
      onSurface: ink,
      onSurfaceVariant: colors.muted,
      surfaceContainerLowest: isDark ? const Color(0xFF0A0D0F) : Colors.white,
      surfaceContainerLow: colors.cardBackground,
      surfaceContainer: colors.cardBackground,
      surfaceContainerHigh: isDark ? const Color(0xFF1C2226) : const Color(0xFFF3F0EA),
      outline: colors.border,
      outlineVariant: colors.border,
    );

    final base = ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      fontFamily: 'Inter',
      scaffoldBackgroundColor: scheme.surface,
      visualDensity: VisualDensity.standard,
      materialTapTargetSize: MaterialTapTargetSize.padded,
      extensions: [colors],
    );

    final text = base.textTheme.apply(
      bodyColor: scheme.onSurface,
      displayColor: isDark ? scheme.onSurface : palette.deep,
    );
    final textTheme = text.copyWith(
      headlineLarge: text.headlineLarge?.copyWith(
        fontWeight: FontWeight.w800,
        letterSpacing: -0.8,
        height: 1.12,
      ),
      headlineMedium: text.headlineMedium?.copyWith(
        fontWeight: FontWeight.w800,
        letterSpacing: -0.6,
        height: 1.15,
      ),
      headlineSmall: text.headlineSmall?.copyWith(fontWeight: FontWeight.w800, letterSpacing: -0.4),
      titleLarge: text.titleLarge?.copyWith(fontWeight: FontWeight.w700, letterSpacing: -0.3),
      titleMedium: text.titleMedium?.copyWith(fontWeight: FontWeight.w700),
      titleSmall: text.titleSmall?.copyWith(fontWeight: FontWeight.w700),
      labelLarge: text.labelLarge?.copyWith(fontWeight: FontWeight.w700),
      labelMedium: text.labelMedium?.copyWith(fontWeight: FontWeight.w600),
      labelSmall: text.labelSmall?.copyWith(fontWeight: FontWeight.w700, letterSpacing: 0.6),
    );

    final buttonShape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(14));
    const buttonPadding = EdgeInsets.symmetric(horizontal: 20, vertical: 14);

    return base.copyWith(
      textTheme: textTheme,
      appBarTheme: AppBarTheme(
        backgroundColor: scheme.surface,
        foregroundColor: isDark ? scheme.onSurface : palette.deep,
        surfaceTintColor: Colors.transparent,
        scrolledUnderElevation: 0.5,
        elevation: 0,
        centerTitle: false,
        titleTextStyle: textTheme.titleLarge,
        systemOverlayStyle: isDark ? SystemUiOverlayStyle.light : SystemUiOverlayStyle.dark,
      ),
      cardTheme: CardThemeData(
        color: colors.cardBackground,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(Radii.lg),
          side: BorderSide(color: colors.border),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: palette.accent,
          foregroundColor: colors.onAccent,
          disabledBackgroundColor: Color.lerp(palette.accent, scheme.surface, 0.55),
          disabledForegroundColor: colors.onAccent.withValues(alpha: 0.55),
          minimumSize: const Size(64, 50),
          padding: buttonPadding,
          shape: buttonShape,
          textStyle: textTheme.labelLarge?.copyWith(fontSize: 15),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(64, 50),
          padding: buttonPadding,
          shape: buttonShape,
          side: BorderSide(color: colors.border, width: 1.4),
          textStyle: textTheme.labelLarge?.copyWith(fontSize: 15),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(shape: buttonShape, textStyle: textTheme.labelLarge),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: isDark ? colors.cardBackground : Colors.white,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide(color: colors.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide(color: colors.border, width: 1.2),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide(color: palette.accent, width: 2),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide(color: scheme.error, width: 1.2),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: BorderSide(color: scheme.error, width: 2),
        ),
      ),
      chipTheme: base.chipTheme.copyWith(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
        side: BorderSide(color: colors.border),
        labelStyle: textTheme.labelMedium,
        showCheckmark: false,
        selectedColor: colors.tone(AppTone.brand).background,
        backgroundColor: colors.cardBackground,
      ),
      // Navigation mirrors the web sidebar: dark gradient surface, gold translucent pill on the active item.
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: palette.deep,
        surfaceTintColor: Colors.transparent,
        indicatorColor: palette.accent.withValues(alpha: 0.24),
        indicatorShape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: BorderSide(color: palette.accent.withValues(alpha: 0.42)),
        ),
        elevation: 0,
        height: 70,
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => textTheme.labelSmall?.copyWith(
            letterSpacing: 0,
            fontSize: 11.5,
            fontWeight: states.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w500,
            color: states.contains(WidgetState.selected) ? Colors.white : const Color(0xFFB9B7B2),
          ),
        ),
        iconTheme: WidgetStateProperty.resolveWith(
          (states) => IconThemeData(
            size: 23,
            color: states.contains(WidgetState.selected) ? palette.accent : const Color(0xFFB9B7B2),
          ),
        ),
      ),
      navigationRailTheme: NavigationRailThemeData(
        backgroundColor: palette.deep,
        indicatorColor: palette.accent.withValues(alpha: 0.24),
        indicatorShape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: BorderSide(color: palette.accent.withValues(alpha: 0.42)),
        ),
        selectedIconTheme: IconThemeData(color: palette.accent),
        unselectedIconTheme: const IconThemeData(color: Color(0xFFB9B7B2)),
        selectedLabelTextStyle: textTheme.labelLarge?.copyWith(color: Colors.white),
        unselectedLabelTextStyle: textTheme.labelLarge?.copyWith(
          color: const Color(0xFFB9B7B2),
          fontWeight: FontWeight.w500,
        ),
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: SegmentedButton.styleFrom(
          selectedBackgroundColor: colors.tone(AppTone.brand).background,
          selectedForegroundColor: isDark ? palette.accent : palette.deep,
          side: BorderSide(color: colors.border),
          textStyle: textTheme.labelMedium,
        ),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: isDark ? palette.accent : palette.deep,
        unselectedLabelColor: colors.muted,
        labelStyle: textTheme.labelLarge,
        unselectedLabelStyle: textTheme.labelLarge,
        indicatorSize: TabBarIndicatorSize.tab,
        dividerColor: Colors.transparent,
        indicator: BoxDecoration(
          color: colors.cardBackground,
          borderRadius: BorderRadius.circular(12),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: isDark ? 0.3 : 0.06),
              blurRadius: 8,
              offset: const Offset(0, 2),
            ),
          ],
        ),
      ),
      dividerTheme: DividerThemeData(color: colors.border, space: 1),
      listTileTheme: ListTileThemeData(
        contentPadding: const EdgeInsets.symmetric(horizontal: 16),
        iconColor: colors.muted,
        titleTextStyle: textTheme.titleSmall,
        subtitleTextStyle: textTheme.bodySmall?.copyWith(color: colors.muted, fontSize: 13),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: isDark ? colors.cardBackground : Colors.white,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(Radii.xl)),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: isDark ? colors.cardBackground : Colors.white,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(Radii.lg)),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: isDark ? const Color(0xFFF1EFEB) : palette.deep,
        contentTextStyle: textTheme.bodyMedium?.copyWith(
          color: isDark ? palette.deep : Colors.white,
          fontWeight: FontWeight.w600,
        ),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      ),
      floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: palette.accent,
        foregroundColor: colors.onAccent,
        elevation: 2,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: palette.accent,
        linearTrackColor: colors.border,
      ),
      pageTransitionsTheme: const PageTransitionsTheme(
        builders: {
          TargetPlatform.android: FadeForwardsPageTransitionsBuilder(),
          TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.macOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.linux: FadeForwardsPageTransitionsBuilder(),
          TargetPlatform.windows: FadeForwardsPageTransitionsBuilder(),
        },
      ),
    );
  }
}
