import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_value_view.dart';
import '../../../core/widgets/cards.dart';
import '../../../domain/models/communication.dart';
import '../../../shared/page_layout.dart';
import '../application/administration_providers.dart';

class AnnouncementFormPage extends ConsumerStatefulWidget {
  const AnnouncementFormPage({super.key});

  @override
  ConsumerState<AnnouncementFormPage> createState() => _AnnouncementFormPageState();
}

class _AnnouncementFormPageState extends ConsumerState<AnnouncementFormPage> {
  final _formKey = GlobalKey<FormState>();
  final _title = TextEditingController();
  final _body = TextEditingController();
  AnnouncementAudience _audience = AnnouncementAudience.families;
  bool _important = false;
  bool _publishing = false;

  bool get _hasContent => _title.text.trim().isNotEmpty || _body.text.trim().isNotEmpty;

  @override
  void dispose() {
    _title.dispose();
    _body.dispose();
    super.dispose();
  }

  Future<void> _publish() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('¿Publicar comunicado?'),
        content: Text(
          'Se enviará a: ${_audience.label}. En el prototipo solo aparece en Avisos; no se envían correos ni SMS.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Revisar')),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Publicar'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    setState(() => _publishing = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref
          .read(administrationActionsProvider)
          .publish(
            AnnouncementDraft(
              title: _title.text,
              body: _body.text,
              audience: _audience,
              important: _important,
            ),
          );
      messenger.showSnackBar(const SnackBar(content: Text('Comunicado publicado.')));
      _title.clear();
      _body.clear();
      if (mounted) context.pop();
    } on Object catch (error) {
      messenger.showSnackBar(SnackBar(content: Text(friendlyError(error))));
      if (mounted) setState(() => _publishing = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return PopScope<Object?>(
      canPop: !_hasContent || _publishing,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        final navigator = Navigator.of(context);
        final discard = await showDialog<bool>(
          context: context,
          builder: (context) => AlertDialog(
            title: const Text('¿Descartar borrador?'),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(context, false),
                child: const Text('Seguir editando'),
              ),
              FilledButton(
                onPressed: () => Navigator.pop(context, true),
                child: const Text('Descartar'),
              ),
            ],
          ),
        );
        if (discard == true) {
          _title.clear();
          _body.clear();
          navigator.pop();
        }
      },
      child: DetailPage(
        title: 'Nuevo comunicado',
        bottomBar: FilledButton.icon(
          onPressed: _publishing ? null : _publish,
          icon: const Icon(Icons.send_rounded),
          label: const Text('Publicar'),
        ),
        children: [
          Form(
            key: _formKey,
            onChanged: () => setState(() {}),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text('Audiencia', style: theme.textTheme.labelLarge),
                const SizedBox(height: Spacing.sm),
                Wrap(
                  spacing: Spacing.sm,
                  runSpacing: Spacing.sm,
                  children: [
                    for (final a in AnnouncementAudience.values)
                      ChoiceChip(
                        label: Text(a.label),
                        selected: _audience == a,
                        onSelected: _publishing ? null : (_) => setState(() => _audience = a),
                      ),
                  ],
                ),
                const SizedBox(height: Spacing.xl),
                TextFormField(
                  controller: _title,
                  enabled: !_publishing,
                  maxLength: 80,
                  textCapitalization: TextCapitalization.sentences,
                  decoration: const InputDecoration(labelText: 'Título'),
                  validator: (v) => (v ?? '').trim().length < 4 ? 'Escribe un título claro.' : null,
                ),
                const SizedBox(height: Spacing.md),
                TextFormField(
                  controller: _body,
                  enabled: !_publishing,
                  minLines: 5,
                  maxLines: 10,
                  maxLength: 1000,
                  textCapitalization: TextCapitalization.sentences,
                  decoration: const InputDecoration(labelText: 'Mensaje', alignLabelWithHint: true),
                  validator: (v) =>
                      (v ?? '').trim().length < 10 ? 'El mensaje es muy corto.' : null,
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  value: _important,
                  onChanged: _publishing ? null : (v) => setState(() => _important = v),
                  title: const Text('Marcar como importante'),
                  subtitle: const Text('Se destaca en la bandeja de avisos.'),
                ),
                const SizedBox(height: Spacing.md),
                AppCard(
                  tone: AppTone.neutral,
                  child: Text(
                    'Evita incluir datos personales, notas o saldos de estudiantes en comunicados masivos.',
                    style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
