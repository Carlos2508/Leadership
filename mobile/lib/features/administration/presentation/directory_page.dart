import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/refresh.dart';
import '../../../core/utils/text_search.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../core/widgets/states.dart';
import '../../../domain/models/people.dart';
import '../../../shared/page_layout.dart';
import '../application/administration_providers.dart';

class DirectoryPage extends ConsumerStatefulWidget {
  const DirectoryPage({super.key});

  @override
  ConsumerState<DirectoryPage> createState() => _DirectoryPageState();
}

class _DirectoryPageState extends ConsumerState<DirectoryPage> {
  String _query = '';
  String? _grade;
  EnrollmentStatus? _status;

  @override
  Widget build(BuildContext context) {
    final directory = ref.watch(directoryProvider);
    final terms = ref.watch(institutionProvider).terms;
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final all = directory.value ?? const <StudentSummary>[];
    final grades = {for (final s in all) s.gradeLabel}.toList()..sort();
    final filtered = all
        .where(
          (s) =>
              (_grade == null || s.gradeLabel == _grade) &&
              (_status == null || s.status == _status) &&
              matchesSearch('${s.fullName} ${s.code}', _query),
        )
        .toList();

    return TabPage(
      subtitle: 'Gestión académica',
      onRefresh: () => safeRefresh([ref.refresh(directoryProvider.future)]),
      slivers: [
        if (directory.hasValue && filtered.isNotEmpty)
          SliverList.separated(
            itemCount: filtered.length,
            separatorBuilder: (_, _) => const SizedBox(height: Spacing.sm),
            itemBuilder: (context, index) => _StudentRow(student: filtered[index]),
          ),
      ],
      children: [
        PageIntro(
          eyebrow: 'Directorio',
          title: terms.studentPlural,
          subtitle: 'Información organizada, accesible y al día.',
        ),
        TextField(
          onChanged: (value) => setState(() => _query = value),
          decoration: const InputDecoration(
            hintText: 'Buscar por nombre o código…',
            prefixIcon: Icon(Icons.search_rounded),
          ),
        ),
        const SizedBox(height: Spacing.md),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              Padding(
                padding: const EdgeInsets.only(right: Spacing.sm),
                child: _FilterMenu<String>(
                  label: _grade ?? 'Todos los grados',
                  active: _grade != null,
                  options: {null: 'Todos los grados', for (final g in grades) g: g},
                  onSelected: (value) => setState(() => _grade = value),
                ),
              ),
              _FilterMenu<EnrollmentStatus>(
                label: _status?.label ?? 'Cualquier estado',
                active: _status != null,
                options: {
                  null: 'Cualquier estado',
                  for (final s in EnrollmentStatus.values) s: s.label,
                },
                onSelected: (value) => setState(() => _status = value),
              ),
            ],
          ),
        ),
        const SizedBox(height: Spacing.md),
        if (!directory.hasValue)
          AsyncValueView(
            value: directory,
            onRetry: () => ref.invalidate(directoryProvider),
            data: (_) => const SizedBox.shrink(),
          )
        else if (filtered.isEmpty)
          const EmptyState(
            icon: Icons.person_search_rounded,
            title: 'Sin resultados',
            message: 'Ajusta la búsqueda o los filtros.',
          )
        else
          Padding(
            padding: const EdgeInsets.only(bottom: Spacing.sm),
            child: Text(
              '${filtered.length} de ${all.length}',
              style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
            ),
          ),
      ],
    );
  }
}

class _FilterMenu<T> extends StatelessWidget {
  const _FilterMenu({
    required this.label,
    required this.active,
    required this.options,
    required this.onSelected,
  });

  final String label;
  final bool active;
  final Map<T?, String> options;
  final ValueChanged<T?> onSelected;

  @override
  Widget build(BuildContext context) {
    return MenuAnchor(
      menuChildren: [
        for (final entry in options.entries)
          MenuItemButton(onPressed: () => onSelected(entry.key), child: Text(entry.value)),
      ],
      builder: (context, controller, _) => FilterChip(
        label: Row(
          mainAxisSize: MainAxisSize.min,
          children: [Text(label), const Icon(Icons.arrow_drop_down_rounded, size: 20)],
        ),
        selected: active,
        onSelected: (_) => controller.isOpen ? controller.close() : controller.open(),
      ),
    );
  }
}

class _StudentRow extends StatelessWidget {
  const _StudentRow({required this.student});

  final StudentSummary student;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final tone = switch (student.status) {
      EnrollmentStatus.active => AppTone.success,
      EnrollmentStatus.pending => AppTone.accent,
      EnrollmentStatus.withdrawn => AppTone.neutral,
    };
    return AppCard(
      padding: const EdgeInsets.all(Spacing.md),
      onTap: () => context.push('/students/${student.id}'),
      semanticLabel: '${student.fullName}, ${student.placement}, ${student.status.label}',
      child: ExcludeSemantics(
        child: Row(
          children: [
            InitialsAvatar(initials: student.initials, size: 42, tone: AppTone.brand),
            const SizedBox(width: Spacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    student.fullName,
                    style: theme.textTheme.titleSmall,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(
                    '${student.placement} · ${student.code}',
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
              ),
            ),
            const SizedBox(width: Spacing.sm),
            StatusPill(label: student.status.label, tone: tone),
          ],
        ),
      ),
    );
  }
}
