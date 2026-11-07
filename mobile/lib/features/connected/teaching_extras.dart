import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/cards.dart';
import 'connected_session.dart';
import 'connected_shell.dart';
import 'offline_store.dart';
import 'school_api.dart';

/// Shown on a class list or grade sheet that comes from the copy kept on the phone.
class OfflineNotice extends StatelessWidget {
  const OfflineNotice({super.key});

  @override
  Widget build(BuildContext context) => const Padding(
    padding: EdgeInsets.only(bottom: Spacing.md),
    child: StatusPill(
      label: 'Sin conexión: lista guardada en el teléfono. Lo que registres se enviará al reconectar.',
      tone: AppTone.warning,
      icon: Icons.cloud_off_outlined,
    ),
  );
}

/// Waiting writes of this account: tries to send them every minute, when the app returns to the foreground and on
/// demand, and reports any write the school did not accept (e.g. someone changed the same grade meanwhile).
class SyncBanner extends ConsumerStatefulWidget {
  const SyncBanner({super.key});

  @override
  ConsumerState<SyncBanner> createState() => _SyncBannerState();
}

class _SyncBannerState extends ConsumerState<SyncBanner> with WidgetsBindingObserver {
  Timer? _timer;
  bool _busy = false;
  List<String> _failed = const [];

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _timer = Timer.periodic(const Duration(minutes: 1), (_) => _sync(silent: true));
    WidgetsBinding.instance.addPostFrameCallback((_) => _sync(silent: true));
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _timer?.cancel();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _sync(silent: true);
  }

  Future<void> _sync({bool silent = false}) async {
    final store = ref.read(offlineStoreProvider);
    final session = ref.read(connectedControllerProvider);
    if (_busy || store == null || session == null || store.pending().isEmpty) return;
    setState(() => _busy = true);
    try {
      final result = await store.flush(session.api);
      if (!mounted) return;
      ref.read(pendingCountProvider.notifier).refresh();
      if (result.sent > 0) {
        // The server now has newer data: reload the open lists.
        ref.invalidate(apiGetProvider);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Se enviaron ${result.sent} registros guardados sin conexión.')),
        );
      } else if (!silent && result.remaining > 0) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Aún no hay conexión con el colegio. Se intentará de nuevo.')),
        );
      }
      if (result.failed.isNotEmpty) setState(() => _failed = result.failed);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final pending = ref.watch(pendingCountProvider);
    final theme = Theme.of(context);
    if (pending == 0 && _failed.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: Spacing.md),
      child: AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (pending > 0) ...[
              Row(
                children: [
                  const Icon(Icons.cloud_upload_outlined),
                  const SizedBox(width: Spacing.sm),
                  Expanded(
                    child: Text(
                      pending == 1 ? '1 registro pendiente de enviar' : '$pending registros pendientes de enviar',
                      style: theme.textTheme.titleSmall,
                    ),
                  ),
                  TextButton(onPressed: _busy ? null : () => _sync(), child: Text(_busy ? 'Enviando…' : 'Enviar ahora')),
                ],
              ),
            ],
            for (final f in _failed)
              Padding(
                padding: const EdgeInsets.only(top: Spacing.xs),
                child: Text('No se aplicó: $f Vuelve a registrarlo.', style: TextStyle(color: theme.colorScheme.error)),
              ),
            if (_failed.isNotEmpty)
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(onPressed: () => setState(() => _failed = const []), child: const Text('Entendido')),
              ),
          ],
        ),
      ),
    );
  }
}

/// Create (and optionally publish) a task for a course from the phone, with the same API and rules as the web.
Future<void> showNewTaskSheet(BuildContext context, WidgetRef ref, String courseId) => showModalBottomSheet<void>(
  context: context,
  isScrollControlled: true,
  useSafeArea: true,
  builder: (_) => _NewTaskSheet(courseId: courseId),
);

class _NewTaskSheet extends ConsumerStatefulWidget {
  const _NewTaskSheet({required this.courseId});

  final String courseId;

  @override
  ConsumerState<_NewTaskSheet> createState() => _NewTaskSheetState();
}

class _NewTaskSheetState extends ConsumerState<_NewTaskSheet> {
  final _title = TextEditingController();
  final _instructions = TextEditingController();
  DateTime _due = DateTime.now().add(const Duration(days: 7));
  bool _allowLate = false;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _title.dispose();
    _instructions.dispose();
    super.dispose();
  }

  Future<void> _submit({required bool publish}) async {
    final title = _title.text.trim();
    if (title.isEmpty) {
      setState(() => _error = 'Escribe el título de la tarea.');
      return;
    }
    final api = ref.read(connectedControllerProvider)!.api;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final dueAt = DateTime(_due.year, _due.month, _due.day, 23, 59);
      final created = await api.post('/api/v1/courses/${widget.courseId}/assignments', {
        'title': title,
        'instructions': _instructions.text.trim(),
        'dueAt': dueAt.toUtc().toIso8601String(),
        'allowLate': _allowLate,
      }) as Map<String, dynamic>;
      if (publish) {
        // Publishing needs the current version of the new task.
        final list = (await api.get('/api/v1/courses/${widget.courseId}/assignments') as List<dynamic>).cast<Map<String, dynamic>>();
        final item = list.firstWhere((t) => t['id'] == created['id']);
        await api.post('/api/v1/courses/${widget.courseId}/assignments/${created['id']}/publish', {'version': item['version']});
      }
      ref.invalidate(apiGetProvider('/api/v1/courses/${widget.courseId}/assignments'));
      if (mounted) {
        Navigator.of(context).pop();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(publish ? 'Tarea publicada para el curso.' : 'Tarea guardada como borrador.')),
        );
      }
    } on ApiFailure catch (e) {
      setState(() => _error = isOfflineFailure(e) ? 'Sin conexión. Las tareas se crean cuando hay conexión con el colegio.' : e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Padding(
      padding: EdgeInsets.fromLTRB(Spacing.lg, Spacing.lg, Spacing.lg, MediaQuery.viewInsetsOf(context).bottom + Spacing.lg),
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('Nueva tarea', style: theme.textTheme.titleLarge),
            const SizedBox(height: Spacing.md),
            TextField(
              controller: _title,
              maxLength: 160,
              textCapitalization: TextCapitalization.sentences,
              decoration: const InputDecoration(labelText: 'Título'),
            ),
            TextField(
              controller: _instructions,
              maxLength: 4000,
              minLines: 3,
              maxLines: 8,
              textCapitalization: TextCapitalization.sentences,
              decoration: const InputDecoration(labelText: 'Instrucciones'),
            ),
            OutlinedButton.icon(
              onPressed: _busy
                  ? null
                  : () async {
                      final picked = await showDatePicker(
                        context: context,
                        initialDate: _due,
                        firstDate: DateTime.now(),
                        lastDate: DateTime.now().add(const Duration(days: 365)),
                      );
                      if (picked != null) setState(() => _due = picked);
                    },
              icon: const Icon(Icons.event_outlined),
              label: Text('Entrega: ${DateFormat("EEEE d 'de' MMMM", 'es').format(_due)}'),
            ),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              value: _allowLate,
              onChanged: _busy ? null : (v) => setState(() => _allowLate = v),
              title: const Text('Aceptar entregas tardías'),
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(bottom: Spacing.sm),
                child: Text(_error!, style: TextStyle(color: theme.colorScheme.error, fontWeight: FontWeight.w600)),
              ),
            Text(
              'Al publicar, la tarea llega a los estudiantes del curso y a sus familias.',
              style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
            ),
            const SizedBox(height: Spacing.sm),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: _busy ? null : () => _submit(publish: false),
                    child: const Text('Guardar borrador'),
                  ),
                ),
                const SizedBox(width: Spacing.sm),
                Expanded(
                  child: FilledButton(
                    onPressed: _busy ? null : () => _submit(publish: true),
                    child: Text(_busy ? 'Guardando…' : 'Publicar'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// Publishes a draft from the task list.
Future<void> publishTask(BuildContext context, WidgetRef ref, String courseId, Map<String, dynamic> task) async {
  final api = ref.read(connectedControllerProvider)!.api;
  try {
    await api.post('/api/v1/courses/$courseId/assignments/${task['id']}/publish', {'version': task['version']});
    ref.invalidate(apiGetProvider('/api/v1/courses/$courseId/assignments'));
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Tarea publicada.')));
    }
  } on ApiFailure catch (e) {
    if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
  }
}
