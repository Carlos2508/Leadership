import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/format/formatters.dart';
import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/responsive_grid.dart';
import '../../../core/widgets/states.dart';
import '../../../shared/page_layout.dart';
import '../../academics/application/academic_providers.dart';
import '../../academics/presentation/academic_widgets.dart';
import '../../auth/application/auth_controller.dart';
import 'course_card.dart';

class StudentHomePage extends ConsumerWidget {
  const StudentHomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(requireSessionProvider);
    final studentId = session.studentId!;
    final format = ref.watch(formattersProvider);
    final now = ref.watch(clockProvider)();
    final summary = ref.watch(homeSummaryProvider(studentId));
    final courses = ref.watch(coursesProvider(studentId));
    final assignments = ref.watch(assignmentsProvider(studentId));
    final events = ref.watch(eventsProvider);

    return TabPage(
      subtitle: summary.value?.student.placement ?? 'Mi espacio estudiantil',
      onRefresh: () => safeRefresh([
        ref.refresh(homeSummaryProvider(studentId).future),
        ref.refresh(coursesProvider(studentId).future),
        ref.refresh(assignmentsProvider(studentId).future),
        ref.refresh(eventsProvider.future),
      ]),
      children: [
        PageIntro(
          eyebrow: format.weekdayDayMonth(now),
          title: '¡Hola, ${session.firstName}!',
          subtitle: '¿Qué vas a descubrir hoy?',
        ),
        AsyncValueView(
          value: summary,
          onRetry: () => ref.invalidate(homeSummaryProvider(studentId)),
          loading: const SkeletonBox(height: 300),
          data: (data) => Column(
            children: [
              HeroBanner(
                tag: 'Tu próximo paso',
                title: 'Un pequeño paso, un gran logro.',
                message: data.pendingAssignments == 0
                    ? '¡Estás al día con tus actividades!'
                    : 'Tienes ${data.pendingAssignments} ${data.pendingAssignments == 1 ? 'actividad pendiente' : 'actividades pendientes'}.',
                icon: Icons.rocket_launch_rounded,
                action: FilledButton.icon(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.of(context).accent,
                    foregroundColor: AppColors.of(context).onAccent,
                    minimumSize: const Size(0, 44),
                  ),
                  onPressed: () => context.go('/student/agenda'),
                  icon: const Icon(Icons.checklist_rounded, size: 20),
                  label: const Text('Ver mis tareas'),
                ),
              ),
              const SizedBox(height: Spacing.lg),
              ResponsiveGrid(
                children: [
                  StatTile(
                    label: 'Mi asistencia',
                    value: '${format.decimal(data.attendance.rate)} %',
                    caption: data.attendance.rate >= 90 ? '¡Muy bien!' : 'Sigue así',
                    icon: Icons.check_circle_rounded,
                    tone: AppTone.brand,
                  ),
                  StatTile(
                    label: 'Mi racha',
                    value: '${data.streakDays} días',
                    caption: 'Entregando a tiempo',
                    icon: Icons.local_fire_department_rounded,
                    tone: AppTone.accent,
                  ),
                  StatTile(
                    label: 'Pendientes',
                    value: '${data.pendingAssignments}',
                    caption: 'Tareas por entregar',
                    icon: Icons.pending_actions_rounded,
                    tone: AppTone.coral,
                    onTap: () => context.go('/student/agenda'),
                  ),
                  StatTile(
                    label: 'Esta semana',
                    value: '${data.completedThisWeek}',
                    caption: 'Entregas realizadas',
                    icon: Icons.task_alt_rounded,
                    tone: AppTone.blue,
                  ),
                ],
              ),
            ],
          ),
        ),
        SectionHeader(
          title: 'Continuar aprendiendo',
          actionLabel: 'Ver cursos',
          onAction: () => context.go('/student/courses'),
        ),
        AsyncValueView(
          value: courses,
          onRetry: () => ref.invalidate(coursesProvider(studentId)),
          loading: const SkeletonBox(height: 150),
          data: (list) => ResponsiveGrid(
            children: [
              for (final course in list.take(4))
                CourseCard(
                  course: course,
                  onTap: () => context.push('/courses/$studentId/${course.subject.id}'),
                ),
            ],
          ),
        ),
        SectionHeader(
          title: 'Para entregar',
          actionLabel: 'Ver agenda',
          onAction: () => context.go('/student/agenda'),
        ),
        AsyncValueView(
          value: assignments,
          onRetry: () => ref.invalidate(assignmentsProvider(studentId)),
          loading: const SkeletonBox(),
          data: (list) {
            final pending = list.where((a) => a.status.isOpen).take(3).toList();
            if (pending.isEmpty) {
              return const AppCard(
                child: EmptyState(
                  icon: Icons.celebration_rounded,
                  title: 'Todo al día',
                  message: 'No tienes tareas pendientes.',
                ),
              );
            }
            return Column(
              children: [
                for (final a in pending) ...[
                  AssignmentTile(assignment: a, onTap: () => context.push('/tasks/${a.id}')),
                  const SizedBox(height: Spacing.sm),
                ],
              ],
            );
          },
        ),
        const SectionHeader(title: 'Próximo en el colegio'),
        AsyncValueView(
          value: events,
          onRetry: () => ref.invalidate(eventsProvider),
          loading: const SkeletonBox(),
          data: (list) {
            final upcoming = list.where((e) => !e.start.isBefore(dateOnly(now))).take(2);
            return Column(
              children: [
                for (final e in upcoming) ...[
                  EventTile(event: e),
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
