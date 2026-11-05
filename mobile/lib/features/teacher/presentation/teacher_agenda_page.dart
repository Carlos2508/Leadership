import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/utils/refresh.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../shared/page_layout.dart';
import '../../academics/application/academic_providers.dart';
import '../../academics/presentation/academic_widgets.dart';

class TeacherAgendaPage extends ConsumerWidget {
  const TeacherAgendaPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final events = ref.watch(eventsProvider);
    return TabPage(
      subtitle: 'Agenda del colegio',
      onRefresh: () => safeRefresh([ref.refresh(eventsProvider.future)]),
      children: [
        const PageIntro(eyebrow: 'Calendario', title: 'Planifica tus semanas'),
        AsyncValueView(
          value: events,
          onRetry: () => ref.invalidate(eventsProvider),
          data: (list) => AgendaCalendar(events: list),
        ),
      ],
    );
  }
}
