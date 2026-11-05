import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../domain/models/assignments.dart';
import '../../../shared/back_navigation.dart';
import '../../academics/application/academic_providers.dart';

/// Hoja de entrega. En el prototipo los archivos se simulan; la app real
/// usará cámara/selector con carga reanudable y confirmación del servidor.
class SubmitAssignmentSheet extends ConsumerStatefulWidget {
  const SubmitAssignmentSheet({required this.assignment, super.key});

  final Assignment assignment;

  @override
  ConsumerState<SubmitAssignmentSheet> createState() => _SubmitAssignmentSheetState();
}

class _SubmitAssignmentSheetState extends ConsumerState<SubmitAssignmentSheet> {
  final _comment = TextEditingController();
  final List<String> _files = [];
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  void _addFile(String kind) {
    final now = ref.read(clockProvider)();
    final stamp =
        '${now.hour.toString().padLeft(2, '0')}${now.minute.toString().padLeft(2, '0')}${now.second.toString().padLeft(2, '0')}';
    setState(() {
      _files.add(
        kind == 'photo'
            ? 'foto_$stamp${_files.length}.jpg'
            : 'documento_$stamp${_files.length}.pdf',
      );
      _error = null;
    });
  }

  Future<void> _submit() async {
    if (_files.isEmpty) {
      setState(() => _error = 'Adjunta al menos un archivo.');
      return;
    }
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await ref
          .read(academicActionsProvider)
          .submitAssignment(
            widget.assignment,
            SubmissionDraft(files: _files, comment: _comment.text),
          );
      if (mounted) Navigator.of(context).pop(true);
    } on Object catch (error) {
      if (mounted) {
        setState(() {
          _submitting = false;
          _error = friendlyError(error);
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final late = ref.watch(clockProvider)().isAfter(widget.assignment.dueAt);
    return UnsavedChangesGuard(
      hasChanges: !_submitting && (_files.isNotEmpty || _comment.text.trim().isNotEmpty),
      message: 'Los archivos y el comentario no se han entregado.',
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(Spacing.xl, 0, Spacing.xl, Spacing.xl),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('Entregar tarea', style: theme.textTheme.headlineSmall),
              const SizedBox(height: Spacing.xs),
              Text(
                widget.assignment.title,
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
              ),
              if (late) ...[
                const SizedBox(height: Spacing.md),
                Text(
                  'El plazo ya venció: tu entrega quedará marcada como tardía.',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: colors.tone(AppTone.warning).foreground,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
              const SizedBox(height: Spacing.lg),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _submitting ? null : () => _addFile('photo'),
                      icon: const Icon(Icons.photo_camera_outlined),
                      label: const Text('Foto'),
                    ),
                  ),
                  const SizedBox(width: Spacing.md),
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _submitting ? null : () => _addFile('document'),
                      icon: const Icon(Icons.attach_file_rounded),
                      label: const Text('Archivo'),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: Spacing.md),
              for (final file in _files)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: Icon(
                    file.endsWith('.jpg') ? Icons.image_outlined : Icons.description_outlined,
                  ),
                  title: Text(file, overflow: TextOverflow.ellipsis),
                  trailing: IconButton(
                    tooltip: 'Quitar $file',
                    onPressed: _submitting ? null : () => setState(() => _files.remove(file)),
                    icon: const Icon(Icons.close_rounded),
                  ),
                ),
              const SizedBox(height: Spacing.sm),
              TextField(
                controller: _comment,
                onChanged: (_) => setState(() {}),
                enabled: !_submitting,
                maxLength: 500,
                minLines: 2,
                maxLines: 4,
                decoration: const InputDecoration(
                  labelText: 'Comentario para tu docente (opcional)',
                  alignLabelWithHint: true,
                ),
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: Spacing.md),
                  child: Semantics(
                    liveRegion: true,
                    child: Text(
                      _error!,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: colors.tone(AppTone.danger).foreground,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ),
              FilledButton(
                onPressed: _submitting ? null : _submit,
                child: _submitting
                    ? const SizedBox.square(
                        dimension: 22,
                        child: CircularProgressIndicator(strokeWidth: 2.5),
                      )
                    : const Text('Confirmar entrega'),
              ),
              const SizedBox(height: Spacing.sm),
              Text(
                'Prototipo: los archivos son simulados y no salen del dispositivo.',
                textAlign: TextAlign.center,
                style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
