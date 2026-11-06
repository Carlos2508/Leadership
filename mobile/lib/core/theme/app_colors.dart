import 'package:flutter/material.dart';

import '../config/institution_config.dart';

/// Tonos funcionales del producto (no de marca) para tarjetas, chips e íconos.
enum AppTone { brand, accent, blue, purple, coral, success, warning, danger, neutral }

/// Par de colores fondo/primer plano accesible para un tono.
@immutable
class TonePair {
  const TonePair(this.background, this.foreground);

  final Color background;
  final Color foreground;
}

/// Extensión de tema con los colores de marca (desde configuración) y los
/// colores funcionales del sistema de diseño.
@immutable
class AppColors extends ThemeExtension<AppColors> {
  const AppColors({
    required this.deep,
    required this.accent,
    required this.onAccent,
    required this.muted,
    required this.border,
    required this.cardBackground,
    required this.heroStart,
    required this.heroEnd,
    required this.tones,
  });

  factory AppColors.from(BrandPalette palette, Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final card = dark ? const Color(0xFF161B1F) : Colors.white;

    TonePair tone(Color base, {Color? lightForeground}) {
      if (dark) {
        return TonePair(Color.lerp(base, card, 0.78)!, Color.lerp(base, Colors.white, 0.45)!);
      }
      return TonePair(
        Color.lerp(base, Colors.white, 0.88)!,
        lightForeground ?? Color.lerp(base, Colors.black, 0.35)!,
      );
    }

    return AppColors(
      deep: palette.deep,
      accent: palette.accent,
      onAccent: _readableOn(palette.accent, palette.deep),
      muted: dark ? const Color(0xFF9EA4A9) : const Color(0xFF6B6F73),
      border: dark ? const Color(0xFF272D32) : const Color(0xFFE7E2D6),
      cardBackground: card,
      heroStart: Color.lerp(palette.deep, Colors.white, dark ? 0.04 : 0.07)!,
      heroEnd: Color.lerp(palette.deep, palette.accent, 0.16)!,
      tones: {
        AppTone.brand: tone(palette.primary, lightForeground: palette.deep),
        AppTone.accent: tone(
          palette.accent,
          lightForeground: Color.lerp(palette.accent, Colors.black, 0.55),
        ),
        AppTone.blue: tone(const Color(0xFF4D87D8)),
        AppTone.purple: tone(const Color(0xFF846AD3)),
        AppTone.coral: tone(const Color(0xFFE7796B)),
        AppTone.success: tone(const Color(0xFF19814F)),
        AppTone.warning: tone(const Color(0xFFB66C10)),
        AppTone.danger: tone(const Color(0xFFB63232)),
        AppTone.neutral: dark
            ? const TonePair(Color(0xFF22282D), Color(0xFFCDD1D4))
            : const TonePair(Color(0xFFF1EEE7), Color(0xFF45494D)),
      },
    );
  }

  final Color deep;
  final Color accent;
  final Color onAccent;
  final Color muted;
  final Color border;
  final Color cardBackground;
  final Color heroStart;
  final Color heroEnd;
  final Map<AppTone, TonePair> tones;

  TonePair tone(AppTone tone) => tones[tone]!;

  static AppColors of(BuildContext context) => Theme.of(context).extension<AppColors>()!;

  /// Escoge el color de texto con mejor contraste sobre [background].
  static Color _readableOn(Color background, Color preferredDark) {
    final darkContrast = _contrast(background, preferredDark);
    final whiteContrast = _contrast(background, Colors.white);
    return darkContrast >= whiteContrast ? preferredDark : Colors.white;
  }

  static double _contrast(Color a, Color b) {
    final la = a.computeLuminance();
    final lb = b.computeLuminance();
    final hi = la > lb ? la : lb;
    final lo = la > lb ? lb : la;
    return (hi + 0.05) / (lo + 0.05);
  }

  @override
  AppColors copyWith({
    Color? deep,
    Color? accent,
    Color? onAccent,
    Color? muted,
    Color? border,
    Color? cardBackground,
    Color? heroStart,
    Color? heroEnd,
    Map<AppTone, TonePair>? tones,
  }) {
    return AppColors(
      deep: deep ?? this.deep,
      accent: accent ?? this.accent,
      onAccent: onAccent ?? this.onAccent,
      muted: muted ?? this.muted,
      border: border ?? this.border,
      cardBackground: cardBackground ?? this.cardBackground,
      heroStart: heroStart ?? this.heroStart,
      heroEnd: heroEnd ?? this.heroEnd,
      tones: tones ?? this.tones,
    );
  }

  @override
  AppColors lerp(covariant AppColors? other, double t) {
    if (other == null) return this;
    return AppColors(
      deep: Color.lerp(deep, other.deep, t)!,
      accent: Color.lerp(accent, other.accent, t)!,
      onAccent: Color.lerp(onAccent, other.onAccent, t)!,
      muted: Color.lerp(muted, other.muted, t)!,
      border: Color.lerp(border, other.border, t)!,
      cardBackground: Color.lerp(cardBackground, other.cardBackground, t)!,
      heroStart: Color.lerp(heroStart, other.heroStart, t)!,
      heroEnd: Color.lerp(heroEnd, other.heroEnd, t)!,
      tones: {
        for (final key in AppTone.values)
          key: TonePair(
            Color.lerp(tone(key).background, other.tone(key).background, t)!,
            Color.lerp(tone(key).foreground, other.tone(key).foreground, t)!,
          ),
      },
    );
  }
}
