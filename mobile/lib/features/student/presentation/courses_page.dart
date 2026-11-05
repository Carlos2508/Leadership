import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../shared/page_layout.dart';
import '../../../shared/visuals.dart';
import '../../academics/application/academic_providers.dart';
import '../../auth/application/auth_controller.dart';

class CoursesPage extends ConsumerWidget {
  const CoursesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final studentId = ref.watch(requireSessionProvider).studentId!;
    final courses = ref.watch(coursesProvider(studentId));
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return TabPage(
      subtitle: 'Mis cursos',
      onRefresh: () => safeRefresh([ref.refresh(coursesProvider(studentId).future)]),
      children: [
        const PageIntro(
          eyebrow: 'Aprendizaje',
          title: 'Lo que aprendo hoy me abre puertas',
          subtitle: 'Descubre tus actividades, recursos y avance por materia.',
        ),
        AsyncValueView(
          value: courses,
          onRetry: () => ref.invalidate(coursesProvider(studentId)),
          data: (list) => Column(
            children: [
              for (final course in list) ...[
                AppCard(
                  onTap: () => context.push('/courses/$studentId/${course.subject.id}'),
                  semanticLabel:
                      '${course.subject.name}, ${course.subject.teacherName}, progreso ${(course.progress * 100).round()} por ciento',
                  child: ExcludeSemantics(
                    child: Row(
                      children: [
                        ToneIcon(
                          icon: course.subject.area.icon,
                          tone: course.subject.area.tone,
                          size: 52,
                        ),
                        const SizedBox(width: Spacing.lg),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(course.subject.name, style: theme.textTheme.titleSmall),
                              Text(
                                course.subject.teacherName,
                                style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                              ),
                              const SizedBox(height: Spacing.sm),
                              ProgressBar(
                                value: course.progress,
                                color: colors.tone(course.subject.area.tone).foreground,
                              ),
                              const SizedBox(height: 6),
                              Text(
                                '${course.completedActivities} de ${course.totalActivities} actividades · Sigue: ${course.nextTopic}',
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(width: Spacing.sm),
                        Icon(Icons.chevron_right_rounded, color: colors.muted),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: Spacing.md),
              ],
            ],
          ),
        ),
      ],
    );
  }
}
