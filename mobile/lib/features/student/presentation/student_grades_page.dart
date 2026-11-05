import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../shared/page_layout.dart';
import '../../academics/application/academic_providers.dart';
import '../../academics/presentation/grade_report_view.dart';
import '../../auth/application/auth_controller.dart';

class StudentGradesPage extends ConsumerWidget {
  const StudentGradesPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(requireSessionProvider);
    final arg = (studentId: session.studentId!, requester: session.role);
    final report = ref.watch(gradeReportProvider(arg));

    return TabPage(
      subtitle: 'Rendimiento académico',
      onRefresh: () => safeRefresh([ref.refresh(gradeReportProvider(arg).future)]),
      children: [
        PageIntro(eyebrow: 'Mis calificaciones', title: 'Así vas, ${session.firstName}'),
        AsyncValueView(
          value: report,
          onRetry: () => ref.invalidate(gradeReportProvider(arg)),
          data: (data) => GradeReportView(report: data, studentId: arg.studentId),
        ),
      ],
    );
  }
}
