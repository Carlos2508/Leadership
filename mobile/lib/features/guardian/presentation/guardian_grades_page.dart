import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/states.dart';
import '../../../domain/models/people.dart';
import '../../../shared/page_layout.dart';
import '../../academics/application/academic_providers.dart';
import '../../academics/presentation/grade_report_view.dart';
import 'child_selector.dart';

class GuardianGradesPage extends ConsumerWidget {
  const GuardianGradesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final childId = ref.watch(selectedChildProvider);
    final student = childId == null ? null : ref.watch(studentProvider(childId)).value;

    return TabPage(
      subtitle: 'Rendimiento académico',
      onRefresh: childId == null
          ? null
          : () => safeRefresh([
              ref.refresh(
                gradeReportProvider((studentId: childId, requester: UserRole.guardian)).future,
              ),
            ]),
      children: [
        PageIntro(
          eyebrow: 'Calificaciones',
          title: student == null ? 'Avances' : 'Avances de ${student.firstName}',
        ),
        const ChildSelector(),
        if (childId == null)
          const EmptyState(icon: Icons.link_off_rounded, title: 'Sin estudiantes vinculados')
        else
          Consumer(
            builder: (context, ref, _) {
              final arg = (studentId: childId, requester: UserRole.guardian);
              final report = ref.watch(gradeReportProvider(arg));
              return AsyncValueView(
                value: report,
                onRetry: () => ref.invalidate(gradeReportProvider(arg)),
                data: (data) => GradeReportView(report: data, studentId: childId),
              );
            },
          ),
      ],
    );
  }
}
