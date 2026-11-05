import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/academics.dart';
import '../../../shared/page_layout.dart';
import '../../../shared/visuals.dart';
import '../../academics/application/academic_providers.dart';

class CourseDetailPage extends ConsumerWidget {
  const CourseDetailPage({required this.studentId, required this.subjectId, super.key});

  final String studentId;
  final String subjectId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final arg = (studentId: studentId, subjectId: subjectId);
    final detail = ref.watch(courseDetailProvider(arg));
    final theme = Theme.of(context);
    final colors = AppColors.of(context);

    return DetailPage(
      title: detail.value?.progress.subject.name ?? 'Curso',
      children: [
        AsyncValueView(
          value: detail,
          onRetry: () => ref.invalidate(courseDetailProvider(arg)),
          data: (course) {
            final subject = course.progress.subject;
            final pair = colors.tone(subject.area.tone);
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                AppCard(
                  tone: subject.area.tone,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Icon(subject.area.icon, size: 40, color: pair.foreground),
                          const Spacer(),
                          StatusPill(
                            label: '${(course.progress.progress * 100).round()} % completado',
                            tone: subject.area.tone,
                          ),
                        ],
                      ),
                      const SizedBox(height: Spacing.lg),
                      Text(subject.name, style: theme.textTheme.headlineSmall),
                      Text(
                        subject.teacherName,
                        style: theme.textTheme.bodyMedium?.copyWith(color: pair.foreground),
                      ),
                      const SizedBox(height: Spacing.md),
                      ProgressBar(value: course.progress.progress, color: pair.foreground),
                      const SizedBox(height: Spacing.md),
                      Text(
                        course.description,
                        style: theme.textTheme.bodyMedium?.copyWith(height: 1.45),
                      ),
                    ],
                  ),
                ),
                const SectionHeader(title: 'Horario'),
                AppCard(
                  child: Column(
                    children: [
                      for (final slot in course.schedule)
                        InfoRow(
                          icon: Icons.schedule_rounded,
                          label: slot.split(' · ').first,
                          value: slot.split(' · ').last,
                        ),
                    ],
                  ),
                ),
                const SectionHeader(title: 'Unidades'),
                AppCard(
                  padding: const EdgeInsets.symmetric(vertical: Spacing.sm),
                  child: Column(
                    children: [
                      for (final unit in course.units)
                        ListTile(
                          leading: Icon(
                            unit.done
                                ? Icons.check_circle_rounded
                                : Icons.radio_button_unchecked_rounded,
                            color: unit.done
                                ? colors.tone(AppTone.success).foreground
                                : colors.muted,
                          ),
                          title: Text(unit.title),
                          subtitle: Text(unit.done ? 'Completada' : 'Pendiente'),
                        ),
                    ],
                  ),
                ),
                const SectionHeader(title: 'Recursos'),
                for (final resource in course.resources) ...[
                  AppCard(
                    padding: const EdgeInsets.all(Spacing.md),
                    onTap: () => ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(
                        content: Text('Los recursos son de demostración en el prototipo.'),
                      ),
                    ),
                    child: Row(
                      children: [
                        ToneIcon(
                          icon: switch (resource.kind) {
                            ResourceKind.document => Icons.description_rounded,
                            ResourceKind.video => Icons.play_circle_rounded,
                            ResourceKind.link => Icons.link_rounded,
                          },
                          tone: AppTone.neutral,
                        ),
                        const SizedBox(width: Spacing.md),
                        Expanded(child: Text(resource.title, style: theme.textTheme.titleSmall)),
                        Icon(Icons.open_in_new_rounded, size: 18, color: colors.muted),
                      ],
                    ),
                  ),
                  const SizedBox(height: Spacing.sm),
                ],
              ],
            );
          },
        ),
      ],
    );
  }
}
