import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/theme/app_colors.dart';

bool chatRead(Map<String, dynamic> message, Map<String, dynamic> thread) =>
    message['readAt'] != null ||
    (message['sequence'] as num) <= ((thread['peerReadSequence'] as num?) ?? 0);
bool chatDelivered(Map<String, dynamic> message, Map<String, dynamic> thread) =>
    chatRead(message, thread) ||
    message['deliveredAt'] != null ||
    (message['sequence'] as num) <= ((thread['peerDeliveredSequence'] as num?) ?? 0);
String chatReceiptLabel(Map<String, dynamic> message, Map<String, dynamic> thread) {
  if (chatRead(message, thread)) {
    final read = DateTime.tryParse(message['readAt'] as String? ?? '');
    return read == null ? 'Visto' : 'Visto a las ${DateFormat('HH:mm').format(read.toLocal())}';
  }
  return chatDelivered(message, thread) ? 'Entregado' : 'Enviado';
}

String chatReceiptDetails(Map<String, dynamic> message, Map<String, dynamic> thread) {
  String full(Object? value) =>
      DateFormat('dd/MM/yyyy HH:mm').format(DateTime.parse(value as String).toLocal());
  return 'Enviado: ${full(message['sentAt'])}${message['deliveredAt'] != null ? '\nEntregado: ${full(message['deliveredAt'])}' : ''}${message['readAt'] != null
      ? '\nVisto: ${full(message['readAt'])}'
      : chatRead(message, thread)
      ? '\nVisto · hora no registrada para este mensaje anterior'
      : ''}';
}

class ChatPresenceLabel extends StatefulWidget {
  const ChatPresenceLabel({required this.thread, this.now, super.key});
  final Map<String, dynamic> thread;
  final DateTime Function()? now;
  @override
  State<ChatPresenceLabel> createState() => _ChatPresenceLabelState();
}

class _ChatPresenceLabelState extends State<ChatPresenceLabel> {
  Timer? _timer;
  Duration _offset = Duration.zero;
  DateTime _now() => widget.now?.call() ?? DateTime.now();
  void _clock() {
    final server = DateTime.tryParse(widget.thread['serverNow'] as String? ?? '');
    if (server != null) _offset = server.difference(_now());
  }

  @override
  void initState() {
    super.initState();
    _clock();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      final state = WidgetsBinding.instance.lifecycleState;
      if (state == null || state == AppLifecycleState.resumed) setState(() {});
    });
  }

  @override
  void didUpdateWidget(covariant ChatPresenceLabel oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.thread['serverNow'] != widget.thread['serverNow']) _clock();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final until = DateTime.tryParse(widget.thread['peerActiveUntil'] as String? ?? '');
    final last = DateTime.tryParse(widget.thread['peerLastActiveAt'] as String? ?? '');
    final online = until != null && until.isAfter(_now().add(_offset));
    final text = online
        ? 'En línea'
        : last == null
        ? 'Sin actividad reciente'
        : 'Últ. vez: ${DateFormat('dd/MM HH:mm').format(last.toLocal())}';
    final colors = AppColors.of(context);
    return Text(
      text,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: Theme.of(context).textTheme.labelSmall?.copyWith(
        color: online ? colors.tone(AppTone.success).foreground : colors.muted,
        fontWeight: online ? FontWeight.w600 : FontWeight.normal,
      ),
    );
  }
}
