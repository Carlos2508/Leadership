import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/teaching.dart';
import '../../../shared/back_navigation.dart';
import '../../../shared/page_layout.dart';
import '../application/teaching_providers.dart';

class ReviewDetailPage extends ConsumerWidget {
  const ReviewDetailPage({required this.reviewId, super.key});

  final String reviewId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final item = ref.watch(reviewItemProvider(reviewId));
    return DetailPage(
      title: 'Revisar entrega',
      children: [
        AsyncValueView(
          value: item,
          onRetry: () => ref.invalidate(reviewItemProvider(reviewId)),
          data: (data) => _ReviewForm(key: ValueKey(data.id), item: data),
        ),
      ],
    );
  }
}

class _ReviewForm extends ConsumerStatefulWidget {
  const _ReviewForm({required this.item, super.key});

  final ReviewItem item;

  @override
  ConsumerState<_ReviewForm> createState() => _ReviewFormState();
}

class _ReviewFormState extends ConsumerState<_ReviewForm> {
  final _formKey = GlobalKey<FormState>();
  late final _score = TextEditingController(
    text: widget.item.score == null ? '' : ref.read(formattersProvider).decimal(widget.item.score!),
  );
  late final _feedback = TextEditingController(text: widget.item.feedback ?? '');
  bool _saving = false;

  late final String _initialScore = _score.text;
  late final String _initialFeedback = _feedback.text;

  bool get _changed => _score.text != _initialScore || _feedback.text != _initialFeedback;

  @override
  void dispose() {
    _score.dispose();
    _feedback.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() => _saving = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref
          .read(teachingActionsProvider)
          .review(
            widget.item.id,
            score: double.parse(_score.text.replaceAll(',', '.')),
            feedback: _feedback.text,
          );
      messenger.showSnackBar(
        const SnackBar(content: Text('Revisión guardada. El estudiante verá tu comentario.')),
      );
      if (mounted) context.pop();
    } on Object catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(friendlyError(error))));
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final item = widget.item;
    final format = ref.watch(formattersProvider);
    final now = ref.watch(clockProvider)();
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return UnsavedChangesGuard(
      hasChanges: !_saving && _changed,
      child: Form(
        key: _formKey,
        onChanged: () => setState(() {}),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                InitialsAvatar(initials: item.student.initials, size: 52, tone: AppTone.blue),
                const SizedBox(width: Spacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(item.student.fullName, style: theme.textTheme.titleLarge),
                      Text(
                        item.group.placement,
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: Spacing.lg),
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(item.assignmentTitle, style: theme.textTheme.titleMedium),
                  const SizedBox(height: Spacing.xs),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Entregada ${format.relativeDateTime(item.submittedAt, now)}',
                          style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                        ),
                      ),
                      if (item.late)
                        const StatusPill(label: 'Fuera de plazo', tone: AppTone.warning),
                    ],
                  ),
                  const Divider(height: Spacing.xl),
                  for (final file in item.files)
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: const Icon(Icons.picture_as_pdf_outlined),
                      title: Text(file, overflow: TextOverflow.ellipsis),
                      trailing: const Icon(Icons.open_in_new_rounded, size: 18),
                      onTap: () => ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text('Visor de archivos simulado en el prototipo.'),
                        ),
                      ),
                    ),
                  if (item.comment.isNotEmpty) ...[
                    const SizedBox(height: Spacing.sm),
                    Text(
                      '“${item.comment}”',
                      style: theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic),
                    ),
                  ],
                ],
              ),
            ),
            const SectionHeader(title: 'Calificación'),
            TextFormField(
              controller: _score,
              enabled: !_saving,
              keyboardType: const TextInputType.numberWithOptions(decimal: true),
              inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]'))],
              decoration: InputDecoration(labelText: 'Punteo', suffixText: '/ ${item.maxScore}'),
              validator: (value) {
                final number = double.tryParse((value ?? '').replaceAll(',', '.'));
                if (number == null) return 'Escribe el punteo.';
                if (number < 0 || number > item.maxScore) {
                  return 'Debe estar entre 0 y ${item.maxScore}.';
                }
                return null;
              },
            ),
            const SizedBox(height: Spacing.lg),
            TextFormField(
              controller: _feedback,
              enabled: !_saving,
              minLines: 3,
              maxLines: 6,
              maxLength: 600,
              decoration: const InputDecoration(
                labelText: 'Comentario para el estudiante',
                alignLabelWithHint: true,
              ),
            ),
            const SizedBox(height: Spacing.sm),
            Text(
              'El punteo se integra a la libreta; las familias lo ven solo cuando se publica y hay solvencia.',
              style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
            ),
            const SizedBox(height: Spacing.xl),
            FilledButton(
              onPressed: _saving ? null : _save,
              child: _saving
                  ? const SizedBox.square(
                      dimension: 22,
                      child: CircularProgressIndicator(strokeWidth: 2.5),
                    )
                  : Text(item.reviewed ? 'Actualizar revisión' : 'Guardar revisión'),
            ),
          ],
        ),
      ),
    );
  }
}
