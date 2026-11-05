import 'package:flutter/material.dart';

import '../format/formatters.dart';
import '../theme/app_colors.dart';
import '../theme/app_theme.dart';

/// Calendario mensual compacto con días marcados y selección accesible.
class MonthCalendar extends StatelessWidget {
  const MonthCalendar({
    required this.month,
    required this.today,
    required this.selectedDay,
    required this.markedDays,
    required this.onDaySelected,
    required this.onMonthChanged,
    required this.formatters,
    super.key,
  });

  /// Cualquier fecha dentro del mes visible.
  final DateTime month;
  final DateTime today;
  final DateTime? selectedDay;
  final Set<DateTime> markedDays;
  final ValueChanged<DateTime> onDaySelected;
  final ValueChanged<DateTime> onMonthChanged;
  final Formatters formatters;

  static const _weekdays = ['L', 'M', 'M', 'J', 'V', 'S', 'D'];
  static const _weekdayNames = [
    'lunes',
    'martes',
    'miércoles',
    'jueves',
    'viernes',
    'sábado',
    'domingo',
  ];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final first = DateTime(month.year, month.month);
    final daysInMonth = DateTime(month.year, month.month + 1, 0).day;
    final leading = first.weekday - 1; // lunes = 0
    final cells = leading + daysInMonth;
    final rows = (cells / 7).ceil();
    final brand = colors.tone(AppTone.brand);
    final isDark = theme.brightness == Brightness.dark;

    return Column(
      children: [
        Row(
          children: [
            Expanded(
              child: Semantics(
                header: true,
                liveRegion: true,
                child: Text(formatters.monthYear(first), style: theme.textTheme.titleMedium),
              ),
            ),
            IconButton(
              tooltip: 'Mes anterior',
              onPressed: () => onMonthChanged(DateTime(month.year, month.month - 1)),
              icon: const Icon(Icons.chevron_left_rounded),
            ),
            IconButton(
              tooltip: 'Mes siguiente',
              onPressed: () => onMonthChanged(DateTime(month.year, month.month + 1)),
              icon: const Icon(Icons.chevron_right_rounded),
            ),
          ],
        ),
        const SizedBox(height: Spacing.sm),
        Row(
          children: [
            for (var i = 0; i < 7; i++)
              Expanded(
                child: Center(
                  child: Semantics(
                    label: _weekdayNames[i],
                    excludeSemantics: true,
                    child: Text(
                      _weekdays[i],
                      style: theme.textTheme.labelSmall?.copyWith(color: colors.muted),
                    ),
                  ),
                ),
              ),
          ],
        ),
        const SizedBox(height: Spacing.xs),
        for (var row = 0; row < rows; row++)
          Row(
            children: [
              for (var col = 0; col < 7; col++)
                Expanded(
                  child: () {
                    final dayNumber = row * 7 + col - leading + 1;
                    if (dayNumber < 1 || dayNumber > daysInMonth) {
                      return const SizedBox(height: 44);
                    }
                    final day = DateTime(month.year, month.month, dayNumber);
                    final selected = selectedDay != null && isSameDay(day, selectedDay!);
                    final isToday = isSameDay(day, today);
                    final marked = markedDays.contains(day);
                    return Padding(
                      padding: const EdgeInsets.all(2),
                      child: Semantics(
                        button: true,
                        selected: selected,
                        label:
                            '${formatters.weekdayDayMonth(day)}${isToday ? ', hoy' : ''}${marked ? ', con actividades' : ''}',
                        excludeSemantics: true,
                        child: InkWell(
                          borderRadius: BorderRadius.circular(12),
                          onTap: () => onDaySelected(day),
                          child: Container(
                            height: 40,
                            alignment: Alignment.center,
                            decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(12),
                              color: selected
                                  ? (isDark ? theme.colorScheme.primary : colors.deep)
                                  : marked
                                  ? brand.background
                                  : null,
                              border: isToday && !selected
                                  ? Border.all(color: colors.accent, width: 2)
                                  : null,
                            ),
                            child: Text(
                              '$dayNumber',
                              style: theme.textTheme.bodyMedium?.copyWith(
                                fontWeight: selected || isToday || marked
                                    ? FontWeight.w800
                                    : FontWeight.w500,
                                color: selected
                                    ? (isDark ? theme.colorScheme.onPrimary : Colors.white)
                                    : marked
                                    ? brand.foreground
                                    : null,
                              ),
                            ),
                          ),
                        ),
                      ),
                    );
                  }(),
                ),
            ],
          ),
        const SizedBox(height: Spacing.sm),
        Wrap(
          spacing: Spacing.lg,
          runSpacing: Spacing.xs,
          children: [
            _Legend(color: brand.background, label: 'Con actividades'),
            _Legend(color: colors.accent, label: 'Hoy', outlined: true),
          ],
        ),
      ],
    );
  }
}

class _Legend extends StatelessWidget {
  const _Legend({required this.color, required this.label, this.outlined = false});

  final Color color;
  final String label;
  final bool outlined;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(
            color: outlined ? null : color,
            border: outlined ? Border.all(color: color, width: 2) : null,
            borderRadius: BorderRadius.circular(4),
          ),
        ),
        const SizedBox(width: 6),
        Text(
          label,
          style: Theme.of(context).textTheme.bodySmall
              ?.copyWith(color: AppColors.of(context).muted),
        ),
      ],
    );
  }
}
