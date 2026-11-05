import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Gráfica de barras simple y accesible, sin dependencias externas.
class BarChart extends StatelessWidget {
  const BarChart({
    required this.values,
    required this.labels,
    required this.valueLabel,
    required this.semanticsLabel,
    this.height = 150,
    this.minValue = 0,
    super.key,
  }) : assert(values.length == labels.length);

  final List<double> values;
  final List<String> labels;
  final String Function(double value) valueLabel;
  final String semanticsLabel;
  final double height;

  /// Base del eje; útil para porcentajes altos (p. ej. asistencia 85–100).
  final double minValue;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final maxValue = values.isEmpty ? 1.0 : values.reduce(math.max);
    final range = math.max(maxValue - minValue, 1e-6);
    final summary = [
      for (var i = 0; i < values.length; i++) '${labels[i]}: ${valueLabel(values[i])}',
    ].join(', ');

    return Semantics(
      label: '$semanticsLabel. $summary',
      child: ExcludeSemantics(
        child: SizedBox(
          height: height + 44,
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              for (var i = 0; i < values.length; i++)
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 5),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.end,
                      children: [
                        FittedBox(
                          fit: BoxFit.scaleDown,
                          child: Text(
                            valueLabel(values[i]),
                            style: theme.textTheme.labelSmall?.copyWith(
                              letterSpacing: 0,
                              color: i == values.length - 1 ? null : colors.muted,
                            ),
                          ),
                        ),
                        const SizedBox(height: 4),
                        TweenAnimationBuilder<double>(
                          tween: Tween(
                            begin: 0,
                            end: ((values[i] - minValue) / range).clamp(0.04, 1.0),
                          ),
                          duration: const Duration(milliseconds: 600),
                          curve: Curves.easeOutCubic,
                          builder: (context, factor, _) => Container(
                            height: height * factor,
                            decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(8),
                              color: i == values.length - 1
                                  ? colors.accent
                                  : colors.tone(AppTone.brand).foreground.withValues(alpha: 0.75),
                            ),
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          labels[i],
                          maxLines: 1,
                          style: theme.textTheme.labelSmall?.copyWith(
                            color: colors.muted,
                            letterSpacing: 0,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
