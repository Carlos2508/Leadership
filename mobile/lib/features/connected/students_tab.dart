import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/cards.dart';
import '../../core/widgets/states.dart';
import '../../shared/page_layout.dart';
import 'connected_session.dart';
import 'connected_shell.dart';
import 'school_api.dart';
import 'timetable_page.dart';

/// Linked students (family) or the student's own record. Sections follow the faculties the school granted.
class StudentsTab extends ConsumerWidget {
  const StudentsTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(connectedControllerProvider)!;
    if (session.isStudent) {
      final me = session.students.first;
      return ConnectedTabPage(
        onRefresh: () async => _invalidateStudent(ref, me),
        children: [
          PageIntro(eyebrow: me.placement ?? 'Mi ficha', title: me.name, subtitle: me.code),
          StudentSections(student: me),
        ],
      );
    }
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return ConnectedTabPage(
      onRefresh: () => ref.read(connectedControllerProvider.notifier).refresh(),
      children: [
        const PageIntro(
          eyebrow: 'Familia',
          title: 'Mis estudiantes',
          subtitle: 'Lo que el colegio te autorizó a consultar de cada uno.',
        ),
        for (final student in session.students)
          Padding(
            padding: const EdgeInsets.only(bottom: Spacing.md),
            child: AppCard(
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => DetailPage(
                    title: student.name,
                    onRefresh: () async => _invalidateStudent(ref, student),
                    children: [
                      PageIntro(
                        eyebrow: student.placement,
                        title: student.name,
                        subtitle: student.code,
                      ),
                      StudentSections(student: student),
                    ],
                  ),
                ),
              ),
              child: Row(
                children: [
                  InitialsAvatar(initials: _initials(student.name)),
                  const SizedBox(width: Spacing.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(student.name, style: theme.textTheme.titleSmall),
                        Text(
                          [student.code, student.placement].whereType<String>().join(' · '),
                          style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                        ),
                        const SizedBox(height: 6),
                        Wrap(
                          spacing: 6,
                          runSpacing: 6,
                          children: [
                            if (student.canAcademic)
                              const StatusPill(label: 'Académico', tone: AppTone.blue),
                            if (student.canFinance)
                              const StatusPill(label: 'Pagos', tone: AppTone.accent),
                          ],
                        ),
                      ],
                    ),
                  ),
                  Icon(Icons.chevron_right, color: colors.muted),
                ],
              ),
            ),
          ),
      ],
    );
  }
}

void _invalidateStudent(WidgetRef ref, LinkedStudent s) {
  for (final p in ['grades', 'assignments', 'attendance', 'gate', 'statement']) {
    ref.invalidate(apiGetProvider('/api/v1/me/students/${s.id}/$p'));
  }
}

String _initials(String name) {
  final parts = name.split(' ').where((p) => p.isNotEmpty).toList();
  return parts.isEmpty
      ? '?'
      : (parts.first[0] + (parts.length > 1 ? parts[1][0] : '')).toUpperCase();
}

enum _Section { grades, tasks, attendance, account }

class StudentSections extends StatefulWidget {
  const StudentSections({required this.student, super.key});

  final LinkedStudent student;

  @override
  State<StudentSections> createState() => _StudentSectionsState();
}

class _StudentSectionsState extends State<StudentSections> {
  late _Section _section = widget.student.canAcademic ? _Section.grades : _Section.account;

  @override
  Widget build(BuildContext context) {
    final s = widget.student;
    final sections = <_Section, (String, IconData)>{
      if (s.canAcademic) ...{
        _Section.grades: ('Notas', Icons.grade_outlined),
        _Section.tasks: ('Tareas', Icons.assignment_outlined),
        _Section.attendance: ('Asistencia', Icons.event_available),
      },
      if (s.canFinance) _Section.account: ('Cuenta', Icons.account_balance_wallet_outlined),
    };
    if (sections.isEmpty) {
      return const EmptyState(
        icon: Icons.lock_outline,
        title: 'Sin información autorizada',
        message: 'El colegio no habilitó consultas para este vínculo.',
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SectionChips<_Section>(
          options: sections,
          selected: _section,
          onSelected: (v) => setState(() => _section = v),
        ),
        if (s.canAcademic)
          Padding(
            padding: const EdgeInsets.only(top: Spacing.md),
            child: OutlinedButton.icon(
              onPressed: () => Navigator.of(context)
                  .push(MaterialPageRoute<void>(builder: (_) => TimetablePage(studentId: s.id))),
              icon: const Icon(Icons.calendar_month_outlined),
              label: const Text('Consultar horario'),
            ),
          ),
        const SizedBox(height: Spacing.lg),
        switch (_section) {
          _Section.grades => _Grades(student: s),
          _Section.tasks => _Tasks(student: s),
          _Section.attendance => _Attendance(student: s),
          _Section.account => _Account(student: s),
        },
      ],
    );
  }
}

class _Grades extends StatelessWidget {
  const _Grades({required this.student});

  final LinkedStudent student;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return ApiBuilder(
      path: '/api/v1/me/students/${student.id}/grades',
      builder: (context, data) {
        final periods = ((data as Map<String, dynamic>)['periods'] as List<dynamic>)
            .cast<Map<String, dynamic>>();
        if (periods.isEmpty) {
          return const EmptyState(
            icon: Icons.grade_outlined,
            title: 'Aún no hay notas publicadas',
            message: 'Aparecerán cuando el colegio publique el período.',
          );
        }
        return Column(
          children: [
            for (final p in periods.reversed)
              Padding(
                padding: const EdgeInsets.only(bottom: Spacing.md),
                child: AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(p['name'] as String, style: theme.textTheme.titleMedium),
                          ),
                          if (p['status'] != 'Available')
                            const StatusPill(
                              label: 'No disponible',
                              tone: AppTone.warning,
                              icon: Icons.lock_outline,
                            ),
                        ],
                      ),
                      const SizedBox(height: Spacing.sm),
                      if (p['status'] == 'Available')
                        for (final g in (p['grades'] as List<dynamic>).cast<Map<String, dynamic>>())
                          Padding(
                            padding: const EdgeInsets.symmetric(vertical: 6),
                            child: Row(
                              children: [
                                Expanded(child: Text(g['subject'] as String)),
                                Text(
                                  _score(g),
                                  style: theme.textTheme.titleSmall?.copyWith(
                                    color: g['status'] == 'Scored' ? null : colors.muted,
                                  ),
                                ),
                              ],
                            ),
                          )
                      else
                        Text(
                          p['status'] == 'PendingFinancialSetup'
                              ? 'El colegio está terminando la configuración de pagos de este período.'
                              : 'Las notas de este período se mostrarán cuando la cuenta esté solvente al corte.',
                          style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
                        ),
                      for (final pending
                          in (p['pending'] as List<dynamic>? ?? const [])
                              .cast<Map<String, dynamic>>())
                        Padding(
                          padding: const EdgeInsets.only(top: 6),
                          child: Text(
                            '${pending['label']}: ${money(pending['balance'] as num)}',
                            style: theme.textTheme.bodySmall,
                          ),
                        ),
                    ],
                  ),
                ),
              ),
          ],
        );
      },
    );
  }

  // Pending is not zero and the app never recomputes approval or averages.
  static String _score(Map<String, dynamic> g) => switch (g['status']) {
    'Scored' => '${g['score']}',
    'Missing' => 'Pendiente',
    'Excused' => 'Exonerado',
    _ => '—',
  };
}

class _Tasks extends ConsumerWidget {
  const _Tasks({required this.student});

  final LinkedStudent student;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final path = '/api/v1/me/students/${student.id}/assignments';
    return ApiBuilder(
      path: path,
      builder: (context, data) {
        final items = (data as List<dynamic>).cast<Map<String, dynamic>>();
        if (items.isEmpty) {
          return const EmptyState(icon: Icons.assignment_outlined, title: 'Sin tareas publicadas');
        }
        return Column(
          children: [
            for (final t in items)
              Padding(
                padding: const EdgeInsets.only(bottom: Spacing.md),
                child: AppCard(
                  onTap: student.isSelf && t['canSubmit'] == true
                      ? () => _submit(context, ref, t, path)
                      : null,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(t['title'] as String, style: theme.textTheme.titleSmall),
                          ),
                          _statusPill(t),
                        ],
                      ),
                      const SizedBox(height: 2),
                      Text(
                        '${t['course']} · entrega ${t['dueAt'] == null ? 'sin fecha' : shortDate(t['dueAt'] as String)}',
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                      if ((t['instructions'] as String?)?.isNotEmpty ?? false) ...[
                        const SizedBox(height: 6),
                        Text(
                          t['instructions'] as String,
                          maxLines: 3,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                      if (t['feedback'] != null) ...[
                        const SizedBox(height: Spacing.sm),
                        InfoRow(label: 'Comentario del docente', value: t['feedback'] as String),
                      ],
                      if (student.isSelf && t['canSubmit'] == true) ...[
                        const SizedBox(height: Spacing.sm),
                        Text(
                          t['submissionStatus'] == null
                              ? 'Toca para entregar'
                              : 'Toca para actualizar tu entrega',
                          style: theme.textTheme.labelMedium?.copyWith(color: colors.accent),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
          ],
        );
      },
    );
  }

  static Widget _statusPill(Map<String, dynamic> t) => switch (t['submissionStatus']) {
    'Reviewed' => const StatusPill(label: 'Revisada', tone: AppTone.success),
    'Submitted' => StatusPill(
      label: t['late'] == true ? 'Entregada tarde' : 'Entregada',
      tone: AppTone.blue,
    ),
    _ =>
      t['closed'] == true
          ? const StatusPill(label: 'Cerrada', tone: AppTone.neutral)
          : const StatusPill(label: 'Pendiente', tone: AppTone.warning),
  };

  Future<void> _submit(
    BuildContext context,
    WidgetRef ref,
    Map<String, dynamic> task,
    String path,
  ) async {
    final controller = TextEditingController(text: task['text'] as String? ?? '');
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (sheet) => _SubmitSheet(task: task, controller: controller),
    );
    controller.dispose();
    if (saved == true) ref.invalidate(apiGetProvider(path));
  }
}

class _SubmitSheet extends ConsumerStatefulWidget {
  const _SubmitSheet({required this.task, required this.controller});

  final Map<String, dynamic> task;
  final TextEditingController controller;

  @override
  ConsumerState<_SubmitSheet> createState() => _SubmitSheetState();
}

class _SubmitSheetState extends ConsumerState<_SubmitSheet> {
  bool _busy = false;
  String? _error;

  Future<void> _save() async {
    final text = widget.controller.text.trim();
    if (text.isEmpty) {
      setState(() => _error = 'Escribe tu entrega.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(connectedControllerProvider)!.api.post(
        '/api/v1/me/assignments/${widget.task['id']}/submission',
        {'text': text, 'version': widget.task['version']},
      );
      if (mounted) Navigator.of(context).pop(true);
    } on ApiFailure catch (e) {
      setState(() {
        _error = e.message;
        _busy = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: EdgeInsets.fromLTRB(
        Spacing.page,
        0,
        Spacing.page,
        Spacing.lg + MediaQuery.viewInsetsOf(context).bottom,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(widget.task['title'] as String, style: theme.textTheme.titleMedium),
          const SizedBox(height: Spacing.md),
          TextField(
            controller: widget.controller,
            enabled: !_busy,
            minLines: 4,
            maxLines: 10,
            maxLength: 4000,
            decoration: const InputDecoration(labelText: 'Tu entrega', alignLabelWithHint: true),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(bottom: Spacing.sm),
              child: Text(_error!, style: TextStyle(color: theme.colorScheme.error)),
            ),
          FilledButton(
            onPressed: _busy ? null : _save,
            child: Text(_busy ? 'Enviando…' : 'Entregar'),
          ),
        ],
      ),
    );
  }
}

class _Attendance extends StatelessWidget {
  const _Attendance({required this.student});

  final LinkedStudent student;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ApiBuilder(
          path: '/api/v1/me/students/${student.id}/attendance',
          builder: (context, data) {
            final d = data as Map<String, dynamic>;
            final incidents = (d['incidents'] as List<dynamic>).cast<Map<String, dynamic>>();
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                GridView.count(
                  crossAxisCount: 2,
                  shrinkWrap: true,
                  physics: const NeverScrollableScrollPhysics(),
                  mainAxisSpacing: Spacing.md,
                  crossAxisSpacing: Spacing.md,
                  childAspectRatio: 1.6,
                  children: [
                    StatTile(
                      label: 'Presente',
                      value: '${d['present']}',
                      icon: Icons.check_circle_outline,
                      tone: AppTone.success,
                    ),
                    StatTile(
                      label: 'Ausente',
                      value: '${d['absent']}',
                      icon: Icons.cancel_outlined,
                      tone: AppTone.danger,
                    ),
                    StatTile(
                      label: 'Tarde',
                      value: '${d['late']}',
                      icon: Icons.schedule,
                      tone: AppTone.warning,
                    ),
                    StatTile(
                      label: 'Justificado',
                      value: '${d['excused']}',
                      icon: Icons.verified_outlined,
                      tone: AppTone.blue,
                    ),
                  ],
                ),
                if (incidents.isNotEmpty) ...[
                  const SizedBox(height: Spacing.lg),
                  const SectionHeader(title: 'Ausencias y llegadas tarde'),
                  for (final i in incidents)
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      title: Text('${i['subject']} · ${_status(i['status'] as String)}'),
                      subtitle: Text(
                        [
                          shortDate(i['date'] as String),
                          i['note'],
                        ].where((x) => x != '').join(' · '),
                      ),
                    ),
                ],
              ],
            );
          },
        ),
        const SizedBox(height: Spacing.lg),
        const SectionHeader(title: 'Entradas y salidas del colegio'),
        ApiBuilder(
          path: '/api/v1/me/students/${student.id}/gate',
          builder: (context, data) {
            final days = (data as List<dynamic>).cast<Map<String, dynamic>>();
            if (days.isEmpty) {
              return Text(
                'Sin marcas recientes en portería.',
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
              );
            }
            return Column(
              children: [
                for (final day in days)
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const ToneIcon(icon: Icons.qr_code_2, tone: AppTone.blue, size: 40),
                    title: Text(shortDate(day['date'] as String)),
                    subtitle: Text(
                      'Entrada ${_time(day['firstIn'] as String?)} · Salida ${_time(day['lastOut'] as String?)}',
                    ),
                  ),
              ],
            );
          },
        ),
      ],
    );
  }

  static String _status(String s) =>
      const {
        'Absent': 'Ausente',
        'Late': 'Tarde',
        'Excused': 'Justificado',
        'Present': 'Presente',
      }[s] ??
      s;

  static String _time(String? iso) {
    final d = iso == null ? null : DateTime.tryParse(iso);
    if (d == null) return '—';
    final local = d.toLocal();
    return '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
  }
}

class _Account extends StatelessWidget {
  const _Account({required this.student});

  final LinkedStudent student;

  @override
  Widget build(BuildContext context) =>
      StatementView(path: '/api/v1/me/students/${student.id}/statement');
}

/// Charges, payments and balance as returned by the server (shared by family and cashier views).
class StatementView extends StatelessWidget {
  const StatementView({required this.path, super.key});

  final String path;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return ApiBuilder(
      path: path,
      builder: (context, data) {
        final d = data as Map<String, dynamic>;
        final charges = (d['charges'] as List<dynamic>).cast<Map<String, dynamic>>();
        final payments = (d['payments'] as List<dynamic>).cast<Map<String, dynamic>>();
        final balance = d['totalBalance'] as num;
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            HeroBanner(
              title: balance > 0 ? money(balance) : 'Al día',
              message: balance > 0 ? 'Saldo pendiente' : 'Sin saldo pendiente',
              icon: Icons.account_balance_wallet_outlined,
            ),
            const SizedBox(height: Spacing.lg),
            const SectionHeader(title: 'Cargos'),
            if (charges.isEmpty)
              Text(
                'Sin cargos registrados.',
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
              ),
            for (final c in charges)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(c['description'] as String? ?? c['concept'] as String),
                subtitle: Text(
                  'Vence ${shortDate(c['dueOn'] as String)} · pagado ${money(c['paid'] as num)}',
                ),
                trailing: Text(
                  money(c['balance'] as num),
                  style: theme.textTheme.titleSmall?.copyWith(
                    color: (c['balance'] as num) > 0
                        ? colors.tone(AppTone.danger).foreground
                        : colors.muted,
                  ),
                ),
              ),
            const SizedBox(height: Spacing.md),
            const SectionHeader(title: 'Pagos'),
            if (payments.isEmpty)
              Text(
                'Sin pagos registrados.',
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
              ),
            for (final p in payments)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: ToneIcon(
                  icon: p['reversed'] == true ? Icons.undo : Icons.receipt_long_outlined,
                  tone: p['reversed'] == true ? AppTone.neutral : AppTone.success,
                  size: 40,
                ),
                title: Text(
                  'Recibo No. ${p['receiptNumber']}${p['reversed'] == true ? ' · revertido' : ''}',
                ),
                subtitle: Text('${shortDate(p['receivedOn'] as String)} · ${p['payerName']}'),
                trailing: Text(money(p['amount'] as num), style: theme.textTheme.titleSmall),
              ),
          ],
        );
      },
    );
  }
}
