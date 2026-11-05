import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_theme.dart';

/// Tarjeta base con borde suave, tono opcional y área táctil accesible.
class AppCard extends StatelessWidget {
  const AppCard({
    required this.child,
    this.onTap,
    this.tone,
    this.padding = const EdgeInsets.all(Spacing.lg),
    this.semanticLabel,
    super.key,
  });

  final Widget child;
  final VoidCallback? onTap;
  final AppTone? tone;
  final EdgeInsetsGeometry padding;
  final String? semanticLabel;

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    final background = tone == null ? colors.cardBackground : colors.tone(tone!).background;
    final radius = BorderRadius.circular(Radii.lg);
    final content = Padding(padding: padding, child: child);
    return Semantics(
      label: semanticLabel,
      button: onTap != null,
      container: true,
      child: Material(
        color: background,
        shape: RoundedRectangleBorder(
          borderRadius: radius,
          side: tone == null ? BorderSide(color: colors.border) : BorderSide.none,
        ),
        clipBehavior: Clip.antiAlias,
        child: onTap == null ? content : InkWell(onTap: onTap, child: content),
      ),
    );
  }
}

/// Insignia redonda con ícono sobre un tono funcional.
class ToneIcon extends StatelessWidget {
  const ToneIcon({required this.icon, required this.tone, this.size = 44, super.key});

  final IconData icon;
  final AppTone tone;
  final double size;

  @override
  Widget build(BuildContext context) {
    final pair = AppColors.of(context).tone(tone);
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: pair.background,
        borderRadius: BorderRadius.circular(size * 0.32),
      ),
      child: Icon(icon, color: pair.foreground, size: size * 0.5),
    );
  }
}

/// Píldora de estado: nunca depende solo del color, siempre lleva texto.
class StatusPill extends StatelessWidget {
  const StatusPill({required this.label, required this.tone, this.icon, super.key});

  final String label;
  final AppTone tone;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final pair = AppColors.of(context).tone(tone);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: pair.background, borderRadius: BorderRadius.circular(999)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 14, color: pair.foreground),
            const SizedBox(width: 4),
          ],
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.labelSmall
                  ?.copyWith(color: pair.foreground, letterSpacing: 0.1, fontSize: 12),
            ),
          ),
        ],
      ),
    );
  }
}

/// Indicador de métrica: etiqueta, valor y comentario breve.
class StatTile extends StatelessWidget {
  const StatTile({
    required this.label,
    required this.value,
    required this.icon,
    required this.tone,
    this.caption,
    this.onTap,
    super.key,
  });

  final String label;
  final String value;
  final String? caption;
  final IconData icon;
  final AppTone tone;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final pair = colors.tone(tone);
    return AppCard(
      tone: tone,
      onTap: onTap,
      semanticLabel: '$label: $value${caption == null ? '' : '. $caption'}',
      child: ExcludeSemantics(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, size: 16, color: pair.foreground),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.labelMedium?.copyWith(color: pair.foreground),
                  ),
                ),
              ],
            ),
            const SizedBox(height: Spacing.sm),
            FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: Text(
                value,
                style: theme.textTheme.headlineSmall?.copyWith(
                  color: theme.brightness == Brightness.dark ? Colors.white : colors.deep,
                ),
              ),
            ),
            if (caption != null) ...[
              const SizedBox(height: 2),
              Text(
                caption!,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Barra de progreso redondeada con etiqueta accesible.
class ProgressBar extends StatelessWidget {
  const ProgressBar({required this.value, this.color, this.height = 8, super.key});

  final double value;
  final Color? color;
  final double height;

  @override
  Widget build(BuildContext context) {
    final clamped = value.clamp(0.0, 1.0);
    return Semantics(
      value: '${(clamped * 100).round()} %',
      child: ClipRRect(
        borderRadius: BorderRadius.circular(height),
        child: LinearProgressIndicator(
          value: clamped,
          minHeight: height,
          color: color,
          backgroundColor: AppColors.of(context).border,
        ),
      ),
    );
  }
}

/// Avatar con iniciales.
class InitialsAvatar extends StatelessWidget {
  const InitialsAvatar({
    required this.initials,
    this.tone = AppTone.accent,
    this.size = 44,
    super.key,
  });

  final String initials;
  final AppTone tone;
  final double size;

  @override
  Widget build(BuildContext context) {
    final pair = AppColors.of(context).tone(tone);
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: pair.background,
        borderRadius: BorderRadius.circular(size * 0.34),
      ),
      child: Text(
        initials,
        style: Theme.of(context).textTheme.titleSmall
            ?.copyWith(color: pair.foreground, fontSize: size * 0.34, fontWeight: FontWeight.w800),
      ),
    );
  }
}

/// Banner principal en verde de marca, como en el kit visual.
class HeroBanner extends StatelessWidget {
  const HeroBanner({
    required this.title,
    required this.message,
    required this.icon,
    this.tag,
    this.action,
    super.key,
  });

  final String title;
  final String message;
  final String? tag;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    final theme = Theme.of(context);
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(Radii.xl - 4),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [colors.heroStart, colors.heroEnd],
        ),
      ),
      clipBehavior: Clip.antiAlias,
      child: Stack(
        children: [
          Positioned(
            right: -40,
            bottom: -50,
            child: Container(
              width: 150,
              height: 150,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: Colors.white.withValues(alpha: 0.10),
              ),
            ),
          ),
          Positioned(
            right: 18,
            top: 18,
            child: ExcludeSemantics(
              child: Icon(icon, size: 52, color: colors.accent.withValues(alpha: 0.95)),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(22, 22, 22, 24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (tag != null) ...[
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                    decoration: BoxDecoration(
                      color: colors.accent,
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Text(
                      tag!,
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: colors.onAccent,
                        letterSpacing: 0.2,
                        fontSize: 12,
                      ),
                    ),
                  ),
                  const SizedBox(height: Spacing.md),
                ],
                Padding(
                  padding: const EdgeInsets.only(right: 64),
                  child: Text(
                    title,
                    style: theme.textTheme.titleLarge?.copyWith(
                      color: Colors.white,
                      fontWeight: FontWeight.w800,
                      fontSize: 22,
                    ),
                  ),
                ),
                const SizedBox(height: Spacing.sm),
                Text(
                  message,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: Colors.white.withValues(alpha: 0.88),
                    height: 1.4,
                  ),
                ),
                if (action != null) ...[const SizedBox(height: Spacing.lg), action!],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Encabezado de sección con acción opcional ("Ver todo").
class SectionHeader extends StatelessWidget {
  const SectionHeader({required this.title, this.actionLabel, this.onAction, super.key});

  final String title;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(top: Spacing.xl, bottom: Spacing.md),
      child: Row(
        children: [
          Expanded(
            child: Semantics(
              header: true,
              child: Text(title, style: theme.textTheme.titleLarge?.copyWith(fontSize: 19)),
            ),
          ),
          if (actionLabel != null && onAction != null)
            Flexible(
              child: TextButton(
                onPressed: onAction,
                style: TextButton.styleFrom(
                  padding: const EdgeInsets.symmetric(horizontal: 8),
                  minimumSize: const Size(48, 40),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Flexible(child: Text(actionLabel!, overflow: TextOverflow.ellipsis)),
                    const SizedBox(width: 2),
                    const Icon(Icons.arrow_outward_rounded, size: 16),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// Fila de detalle etiqueta/valor para pantallas de información.
class InfoRow extends StatelessWidget {
  const InfoRow({required this.label, required this.value, this.icon, super.key});

  final String label;
  final String value;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 20, color: colors.muted),
            const SizedBox(width: Spacing.md),
          ],
          Expanded(
            child: Text(label, style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted)),
          ),
          const SizedBox(width: Spacing.md),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}
