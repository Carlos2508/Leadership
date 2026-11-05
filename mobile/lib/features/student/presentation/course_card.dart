import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/academics.dart';
import '../../../shared/visuals.dart';

class CourseCard extends StatelessWidget {
  const CourseCard({required this.course, this.onTap, super.key});

  final CourseProgress course;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final tone = course.subject.area.tone;
    final pair = colors.tone(tone);
    final percent = (course.progress * 100).round();
    return AppCard(
      tone: tone,
      onTap: onTap,
      semanticLabel: '${course.subject.name}, progreso $percent por ciento',
      child: ExcludeSemantics(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(course.subject.area.icon, size: 32, color: pair.foreground),
            const SizedBox(height: Spacing.lg),
            Text(
              course.subject.name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: theme.textTheme.titleSmall,
            ),
            const SizedBox(height: 2),
            Text(
              'Progreso $percent %',
              style: theme.textTheme.bodySmall?.copyWith(color: pair.foreground),
            ),
            const SizedBox(height: Spacing.sm),
            ProgressBar(value: course.progress, color: pair.foreground, height: 6),
          ],
        ),
      ),
    );
  }
}
