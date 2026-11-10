import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/theme/app_colors.dart';
import 'biometric_vault.dart';
import 'chat_status.dart';
import 'connected_session.dart';
import 'school_api.dart';

typedef ChatMap = Map<String, dynamic>;
bool _foreground() =>
    WidgetsBinding.instance.lifecycleState == null ||
    WidgetsBinding.instance.lifecycleState == AppLifecycleState.resumed;
String _initials(String name) =>
    name.split(' ').where((p) => p.isNotEmpty).take(2).map((p) => p[0]).join().toUpperCase();
String _time(Object? iso) =>
    DateFormat('dd/MM HH:mm').format(DateTime.parse(iso as String).toLocal());
String _clientId() {
  final bytes = List<int>.generate(16, (_) => Random.secure().nextInt(256));
  bytes[6] = (bytes[6] & 15) | 64;
  bytes[8] = (bytes[8] & 63) | 128;
  final s = bytes.map((v) => v.toRadixString(16).padLeft(2, '0')).join();
  return '${s.substring(0, 8)}-${s.substring(8, 12)}-${s.substring(12, 16)}-${s.substring(16, 20)}-${s.substring(20)}';
}

class ChatInboxController extends AsyncNotifier<ChatMap?> {
  bool _busy = false;
  final _presenceClientId = _clientId();
  int _version = 0;
  bool _active = false;
  @override
  Future<ChatMap?> build() async {
    final api = ref.watch(connectedControllerProvider.select((s) => s?.api));
    if (api == null) return null;
    final timer = Timer.periodic(const Duration(seconds: 10), (_) {
      if (_foreground() && !ref.read(biometricLockedProvider)) unawaited(refresh());
    });
    ref.onDispose(() {
      timer.cancel();
      if (_active) unawaited(_announce(api, false));
    });
    final summary = await api.get('/api/v1/chat/summary') as ChatMap;
    if (ref.mounted &&
        _foreground() &&
        !ref.read(biometricLockedProvider) &&
        summary['available'] == true) {
      await _announce(api, true, summary['pendingDeliveries'] as List? ?? const []);
    }
    return summary;
  }

  Future<void> refresh() async {
    final session = ref.read(connectedControllerProvider);
    if (session == null || _busy || ref.read(biometricLockedProvider)) return;
    _busy = true;
    try {
      final summary = await session.api.get('/api/v1/chat/summary') as ChatMap;
      if (ref.mounted && ref.read(connectedControllerProvider)?.api == session.api) {
        state = AsyncData(summary);
        if (_foreground() && !ref.read(biometricLockedProvider) && summary['available'] == true) {
          await _announce(session.api, true, summary['pendingDeliveries'] as List? ?? const []);
        }
      }
    } on ApiFailure catch (e) {
      if (e.unauthorized && ref.mounted) ref.read(connectedControllerProvider.notifier).expire();
      // Keep the last confirmed count; opening the inbox shows a recoverable network error.
    } finally {
      _busy = false;
    }
  }

  Future<void> _announce(SchoolApi api, bool active, [List<dynamic> deliveries = const []]) async {
    _active = active;
    final body = {
      'clientId': _presenceClientId,
      'version': ++_version,
      'active': active,
      'deliveries': deliveries,
    };
    try {
      await api.post('/api/v1/chat/activity', body);
    } on Object {
      /* Presence expires automatically if this request cannot be confirmed. */
    }
  }

  Future<void> setForeground(bool active) async {
    final api = ref.read(connectedControllerProvider)?.api;
    if (api == null) return;
    if (active && _foreground() && !ref.read(biometricLockedProvider)) {
      await refresh();
    } else if (_active) {
      await _announce(api, false);
    }
  }
}

final chatInboxProvider = AsyncNotifierProvider.autoDispose<ChatInboxController, ChatMap?>(
  ChatInboxController.new,
);

class ChatButton extends ConsumerWidget {
  const ChatButton({super.key});
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summary = ref.watch(chatInboxProvider).value;
    if (summary?['available'] != true) return const SizedBox.shrink();
    final count = (summary?['unread'] as num?)?.toInt() ?? 0;
    return IconButton(
      tooltip: count > 0 ? 'Mensajes: $count sin leer' : 'Mensajes',
      onPressed: () {
        // The unread notice belongs to the home screen; never leave it over the chat.
        ScaffoldMessenger.of(context).hideCurrentSnackBar();
        Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const ChatHomePage()));
      },
      color: Colors.white,
      icon: Badge(
        isLabelVisible: count > 0,
        label: Text(count > 99 ? '99+' : '$count'),
        child: const Icon(Icons.chat_bubble_outline),
      ),
    );
  }
}

class ChatHomePage extends ConsumerStatefulWidget {
  const ChatHomePage({super.key});
  @override
  ConsumerState<ChatHomePage> createState() => _ChatHomePageState();
}

class _ChatHomePageState extends ConsumerState<ChatHomePage> {
  final _search = TextEditingController();
  List<ChatMap> _years = [], _threads = [];
  String? _year, _error;
  bool _loading = true, _polling = false;
  bool _moreBusy = false;
  Timer? _timer, _searchTimer;
  int _generation = 0, _page = 1, _total = 0;
  SchoolApi get _api => ref.read(connectedControllerProvider)!.api;
  String get _base => '/api/v1/years/$_year/chat';
  String _listUrl([int page = 1]) =>
      '$_base/conversations?page=$page&search=${Uri.encodeQueryComponent(_search.text)}';
  @override
  void initState() {
    super.initState();
    unawaited(_load());
    _timer = Timer.periodic(const Duration(seconds: 5), (_) {
      if (_foreground() &&
          !ref.read(biometricLockedProvider) &&
          (ModalRoute.of(context)?.isCurrent ?? false)) {
        unawaited(_poll());
      }
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _searchTimer?.cancel();
    _search.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _error = null;
      _threads = [];
    });
    try {
      final years = [for (final y in await _api.get('/api/v1/chat/years') as List) y as ChatMap];
      if (!mounted || generation != _generation) return;
      _years = years;
      _year ??=
          years.where((y) => y['status'] == 'Active').firstOrNull?['id'] as String? ??
          years.firstOrNull?['id'] as String?;
      final result = _year == null
          ? <String, dynamic>{'items': <ChatMap>[], 'total': 0}
          : await _api.get(_listUrl()) as ChatMap;
      final rows = [for (final t in result['items'] as List) t as ChatMap];
      if (mounted && generation == _generation) {
        setState(() {
          _threads = rows;
          _total = result['total'] as int;
          _page = 1;
        });
      }
    } on Object catch (e) {
      if (mounted && generation == _generation) setState(() => _error = e.toString());
    } finally {
      if (mounted && generation == _generation) setState(() => _loading = false);
    }
  }

  Future<void> _poll() async {
    if (_loading || _polling || _year == null || ref.read(connectedControllerProvider) == null) {
      return;
    }
    final generation = _generation;
    _polling = true;
    try {
      final result = await _api.get(_listUrl()) as ChatMap;
      final rows = [for (final t in result['items'] as List) t as ChatMap];
      if (mounted && generation == _generation) {
        setState(() {
          final ids = rows.map((t) => t['id']).toSet();
          _threads = _page == 1
              ? rows
              : [...rows, ..._threads.where((t) => !ids.contains(t['id']))];
          _total = result['total'] as int;
          _error = null;
        });
      }
    } on Object catch (e) {
      if (mounted && generation == _generation) setState(() => _error = e.toString());
    } finally {
      _polling = false;
    }
  }

  void _searchChanged(String value) {
    _generation++;
    setState(() {});
    _searchTimer?.cancel();
    _searchTimer = Timer(const Duration(milliseconds: 300), () => unawaited(_load()));
  }

  Future<void> _loadMore() async {
    if (_moreBusy) return;
    final generation = _generation;
    setState(() => _moreBusy = true);
    try {
      final result = await _api.get(_listUrl(_page + 1)) as ChatMap;
      if (mounted && generation == _generation) {
        setState(() {
          final ids = _threads.map((t) => t['id']).toSet();
          _threads.addAll([
            for (final t in result['items'] as List)
              if (!ids.contains((t as ChatMap)['id'])) t,
          ]);
          _page++;
          _total = result['total'] as int;
        });
      }
    } on Object catch (e) {
      if (mounted && generation == _generation) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _moreBusy = false);
    }
  }

  Future<void> _open(ChatMap t) async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => ChatThreadPage(year: _year!, thread: t),
      ),
    );
    if (mounted) {
      unawaited(_poll());
      unawaited(ref.read(chatInboxProvider.notifier).refresh());
    }
  }

  Future<void> _new() async {
    final selected = await showModalBottomSheet<ChatMap>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (_) => ChatContacts(year: _year!),
    );
    if (!mounted || selected == null) return;
    try {
      final result = await _api.post('$_base/conversations', {
        'studentId': selected['studentId'],
        'teacherId': selected['teacherId'],
        'memberId': selected['memberId'],
      }) as ChatMap;
      if (mounted) await _open({...selected, 'id': result['id']});
    } on Object catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    final term = _search.text.toLowerCase();
    final rows = _threads
        .where((t) => '${t['name']} ${t['student']}'.toLowerCase().contains(term))
        .toList();
    return Scaffold(
      appBar: AppBar(
        title: const Text('Mensajes'),
        actions: [
          if (_year != null)
            IconButton(
              onPressed: _new,
              tooltip: 'Nueva conversación',
              icon: const Icon(Icons.edit_square),
            ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            if (_years.isNotEmpty)
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 8, 20, 12),
                child: DropdownButtonFormField<String>(
                  key: ValueKey(_year),
                  initialValue: _year,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Ciclo escolar'),
                  items: [
                    for (final y in _years)
                      DropdownMenuItem(value: y['id'] as String, child: Text(y['label'] as String)),
                  ],
                  onChanged: _loading
                      ? null
                      : (value) {
                          _year = value;
                          unawaited(_load());
                        },
                ),
              ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: TextField(
                controller: _search,
                onChanged: _searchChanged,
                maxLength: 100,
                decoration: const InputDecoration(
                  hintText: 'Buscar conversación',
                  counterText: '',
                  prefixIcon: Icon(Icons.search),
                ),
              ),
            ),
            if (_error != null) _ChatError(message: _error!, retry: _load),
            Expanded(
              child: _loading
                  ? const Center(child: CircularProgressIndicator())
                  : rows.isEmpty
                  ? _ChatEmpty(
                      title: _years.isEmpty
                          ? 'Aún no hay contactos disponibles'
                          : 'Tu próxima conversación empieza aquí',
                      text: 'Solo docentes de tus cursos y estudiantes o encargados autorizados.',
                      action: _year == null ? null : _new,
                    )
                  : RefreshIndicator(
                      onRefresh: _poll,
                      child: ListView.separated(
                        padding: const EdgeInsets.symmetric(vertical: 12),
                        itemCount: rows.length + (_threads.length < _total ? 1 : 0),
                        separatorBuilder: (_, _) => const Divider(height: 1, indent: 78),
                        itemBuilder: (context, i) {
                          if (i == rows.length) {
                            return TextButton(
                              onPressed: _moreBusy ? null : _loadMore,
                              child: Text(_moreBusy ? 'Cargando…' : 'Ver más conversaciones'),
                            );
                          }
                          final t = rows[i];
                          final count = (t['unread'] as num).toInt();
                          return ListTile(
                            contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
                            leading: CircleAvatar(
                              backgroundColor: colors.accent.withValues(alpha: 0.17),
                              child: Text(_initials(t['name'] as String)),
                            ),
                            title: Text(
                              t['name'] as String,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(fontWeight: FontWeight.w600),
                            ),
                            subtitle: Text(
                              '${t['role']} · ${t['student']}\n${t['preview']}',
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                            ),
                            isThreeLine: true,
                            trailing: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Text(
                                  DateFormat('dd/MM')
                                      .format(DateTime.parse(t['updatedAt'] as String).toLocal()),
                                  style: Theme.of(context).textTheme.labelSmall,
                                ),
                                const SizedBox(height: 6),
                                if (count > 0) Badge(label: Text('$count')),
                              ],
                            ),
                            onTap: () => _open(t),
                          );
                        },
                      ),
                    ),
            ),
          ],
        ),
      ),
    );
  }
}

class ChatContacts extends ConsumerStatefulWidget {
  const ChatContacts({required this.year, super.key});
  final String year;
  @override
  ConsumerState<ChatContacts> createState() => _ChatContactsState();
}

class _ChatContactsState extends ConsumerState<ChatContacts> {
  final _search = TextEditingController();
  List<ChatMap> _contacts = [];
  int _page = 1, _total = 0;
  bool _busy = false;
  String? _error;
  @override
  void initState() {
    super.initState();
    unawaited(_find());
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _find({bool append = false}) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    if (!append) _page = 1;
    try {
      final api = ref.read(connectedControllerProvider)!.api;
      final data = await api.get(
        '/api/v1/years/${widget.year}/chat/contacts?page=$_page&search=${Uri.encodeQueryComponent(_search.text)}',
      ) as ChatMap;
      if (mounted) {
        setState(() {
          final rows = [for (final c in data['items'] as List) c as ChatMap];
          _contacts = append ? [..._contacts, ...rows] : rows;
          _total = data['total'] as int;
        });
      }
    } on Object catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => SizedBox(
    height: MediaQuery.sizeOf(context).height * 0.85,
    child: Padding(
      padding: EdgeInsets.fromLTRB(20, 20, 20, MediaQuery.viewInsetsOf(context).bottom + 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Text('Nueva conversación', style: Theme.of(context).textTheme.titleLarge),
              ),
              IconButton(
                onPressed: () => Navigator.pop(context),
                tooltip: 'Cerrar',
                icon: const Icon(Icons.close),
              ),
            ],
          ),
          const Text('Contactos autorizados de los cursos del estudiante.'),
          const SizedBox(height: 16),
          TextField(
            controller: _search,
            maxLength: 100,
            onSubmitted: (_) => _find(),
            decoration: InputDecoration(
              labelText: 'Buscar contacto o estudiante',
              counterText: '',
              suffixIcon: IconButton(
                onPressed: _busy ? null : _find,
                tooltip: 'Buscar',
                icon: const Icon(Icons.search),
              ),
            ),
          ),
          if (_error != null) _ChatError(message: _error!, retry: _find),
          if (_busy) const LinearProgressIndicator(),
          Expanded(
            child: ListView(
              children: [
                for (final c in _contacts)
                  ListTile(
                    leading: CircleAvatar(child: Text(_initials(c['name'] as String))),
                    title: Text(c['name'] as String),
                    subtitle: Text('${c['role']} · Sobre ${c['student']}'),
                    trailing: const Icon(Icons.chat_bubble_outline),
                    onTap: _busy ? null : () => Navigator.pop(context, c),
                  ),
                if (!_busy && _contacts.isEmpty)
                  const Padding(
                    padding: EdgeInsets.all(24),
                    child: Text(
                      'No hay contactos que coincidan. Los accesos y asignaciones deben estar activos.',
                    ),
                  ),
                if (_contacts.length < _total)
                  TextButton(
                    onPressed: _busy
                        ? null
                        : () {
                            _page++;
                            unawaited(_find(append: true));
                          },
                    child: const Text('Ver más contactos'),
                  ),
              ],
            ),
          ),
        ],
      ),
    ),
  );
}

class ChatThreadPage extends ConsumerStatefulWidget {
  const ChatThreadPage({required this.year, required this.thread, super.key});
  final String year;
  final ChatMap thread;
  @override
  ConsumerState<ChatThreadPage> createState() => _ChatThreadPageState();
}

class _ChatThreadPageState extends ConsumerState<ChatThreadPage> {
  final _draft = TextEditingController(), _scroll = ScrollController();
  List<ChatMap> _messages = [];
  late ChatMap _thread = widget.thread;
  bool _loading = true,
      _polling = false,
      _sending = false,
      _more = false,
      _older = false,
      _retry = false,
      _unseen = false,
      _leaving = false,
      _reading = false;
  String? _error;
  ChatMap? _pending;
  Timer? _timer;
  int _read = 0;
  SchoolApi get _api => ref.read(connectedControllerProvider)!.api;
  String get _base => '/api/v1/years/${widget.year}/chat/conversations/${widget.thread['id']}';
  bool get _atEnd => !_scroll.hasClients || _scroll.offset < 70;
  @override
  void initState() {
    super.initState();
    _scroll.addListener(_onScroll);
    unawaited(_load());
    _timer = Timer.periodic(const Duration(seconds: 3), (_) {
      if (_foreground() &&
          !ref.read(biometricLockedProvider) &&
          (ModalRoute.of(context)?.isCurrent ?? false)) {
        unawaited(_poll());
      }
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _draft.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final page = await _api.get('$_base/messages') as ChatMap;
      if (!mounted) return;
      setState(() {
        _thread = page['conversation'] as ChatMap;
        _messages = [for (final m in page['items'] as List) m as ChatMap];
        _more = page['hasMore'] as bool;
      });
      _bottom();
    } on Object catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _poll() async {
    if (_loading || _polling || ref.read(connectedControllerProvider) == null) return;
    _polling = true;
    final atEnd = _atEnd;
    try {
      final after = _messages.lastOrNull?['sequence'] ?? 0;
      final page = await _api.get(
        '$_base/messages?after=$after&knownRead=${_thread['peerReadSequence'] ?? 0}&knownDelivered=${_thread['peerDeliveredSequence'] ?? 0}&receiptFrom=${_messages.firstOrNull?['sequence'] ?? 1}',
      ) as ChatMap;
      if (!mounted) return;
      final keys = _messages.map((m) => m['sequence']).toSet();
      final added = [
        for (final m in page['items'] as List)
          if (!keys.contains((m as ChatMap)['sequence'])) m,
      ];
      setState(() {
        final receipts = {
          for (final r in page['receipts'] as List? ?? const []) (r as ChatMap)['sequence']: r,
        };
        _messages = [
          for (final m in _messages)
            receipts.containsKey(m['sequence']) ? {...m, ...receipts[m['sequence']] as ChatMap} : m,
        ];
        _thread = page['conversation'] as ChatMap;
        _messages.addAll(added);
        if (!_retry) _error = null;
        if (_pending != null &&
            _messages.any(
              (m) => m['mine'] == true && m['clientMessageId'] == _pending!['clientMessageId'],
            )) {
          _sent();
        }
        if (added.isNotEmpty && !atEnd) _unseen = true;
      });
      if (atEnd) _bottom();
    } on ApiFailure catch (e) {
      if (mounted) {
        setState(() {
          _error = e.message;
          if (e.status == 401 || e.status == 403 || e.status == 404) {
            _messages = [];
            _thread = {..._thread, 'canSend': false};
          }
        });
      }
    } finally {
      _polling = false;
    }
  }

  Future<void> _send() async {
    if (_sending || _draft.text.trim().isEmpty || _thread['canSend'] != true) return;
    _pending ??= {'clientMessageId': _clientId(), 'body': _draft.text.trim()};
    final attempt = _pending!;
    setState(() {
      _sending = true;
      _error = null;
    });
    try {
      await _api.post('$_base/messages', attempt);
      if (mounted && identical(_pending, attempt)) {
        setState(_sent);
        await _poll();
        _bottom();
      }
    } on Object catch (e) {
      if (mounted && identical(_pending, attempt)) {
        setState(() {
          _error = e.toString();
          _retry = true;
        });
      }
    } finally {
      if (mounted && identical(_pending, attempt)) setState(() => _sending = false);
    }
  }

  void _sent() {
    _error = null;
    _sending = false;
    _pending = null;
    _retry = false;
    _draft.clear();
    unawaited(ref.read(chatInboxProvider.notifier).refresh());
  }

  Future<void> _previous() async {
    if (_older || _messages.isEmpty) return;
    setState(() => _older = true);
    try {
      final page =
          await _api.get('$_base/messages?before=${_messages.first['sequence']}') as ChatMap;
      if (mounted) {
        setState(() {
          final keys = _messages.map((m) => m['sequence']).toSet();
          _messages = [
            for (final m in page['items'] as List)
              if (!keys.contains((m as ChatMap)['sequence'])) m,
            ..._messages,
          ];
          _more = page['hasMore'] as bool;
        });
      }
    } on Object catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _older = false);
    }
  }

  void _bottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      if (_scroll.hasClients) _scroll.jumpTo(0);
      setState(() => _unseen = false);
      unawaited(_markRead());
    });
  }

  void _onScroll() {
    if (_atEnd) unawaited(_markRead());
  }

  Future<void> _markRead() async {
    final last = (_messages.lastOrNull?['sequence'] as num?)?.toInt() ?? 0;
    if (!mounted ||
        !_atEnd ||
        !_foreground() ||
        ref.read(biometricLockedProvider) ||
        _reading ||
        !(ModalRoute.of(context)?.isCurrent ?? false) ||
        last <= _read) {
      return;
    }
    _reading = true;
    try {
      await _api.post('$_base/read', {'throughSequence': last});
      _read = max(_read, last);
      if (mounted) unawaited(ref.read(chatInboxProvider.notifier).refresh());
    } on Object {
      /* Retry while visible. */
    } finally {
      _reading = false;
    }
  }

  Future<void> _back() async {
    if (_sending) return;
    if (_draft.text.trim().isNotEmpty || _pending != null) {
      final leave = await showDialog<bool>(
        context: context,
        builder: (c) => AlertDialog(
          title: const Text('Hay un mensaje sin enviar'),
          content: const Text('Puedes quedarte para enviarlo o salir y descartarlo.'),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(c, false),
              child: const Text('Seguir escribiendo'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(c, true),
              child: const Text('Salir y descartar'),
            ),
          ],
        ),
      );
      if (leave != true) return;
    }
    if (mounted) {
      setState(() => _leaving = true);
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) Navigator.pop(context);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    return PopScope(
      canPop: _leaving || (!_sending && _draft.text.isEmpty && _pending == null),
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) unawaited(_back());
      },
      child: Scaffold(
        appBar: AppBar(
          toolbarHeight: 80 * MediaQuery.textScalerOf(context).scale(1).clamp(1, 1.6),
          leading: BackButton(onPressed: _back),
          title: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(_thread['name'] as String? ?? '', maxLines: 1, overflow: TextOverflow.ellipsis),
              Text(
                'Sobre ${_thread['student']}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.labelSmall,
              ),
              ChatPresenceLabel(thread: _thread),
            ],
          ),
        ),
        body: SafeArea(
          child: Column(
            children: [
              Expanded(
                child: _loading
                    ? const Center(child: CircularProgressIndicator())
                    : _messages.isEmpty
                    ? const _ChatEmpty(
                        title: 'Saluda para empezar',
                        text: 'Conversación privada dentro de la plataforma escolar.',
                      )
                    : ListView.builder(
                        controller: _scroll,
                        reverse: true,
                        padding: const EdgeInsets.all(16),
                        itemCount: _messages.length + (_more ? 1 : 0),
                        itemBuilder: (context, i) {
                          if (i == _messages.length) {
                            return TextButton(
                              onPressed: _older ? null : _previous,
                              child: Text(_older ? 'Cargando…' : 'Ver mensajes anteriores'),
                            );
                          }
                          final m = _messages[_messages.length - 1 - i];
                          final mine = m['mine'] == true;
                          final read = chatRead(m, _thread);
                          return Align(
                            alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
                            child: ConstrainedBox(
                              constraints: BoxConstraints(
                                maxWidth: MediaQuery.sizeOf(context).width * 0.84,
                              ),
                              child: Container(
                                margin: const EdgeInsets.only(bottom: 12),
                                padding: const EdgeInsets.fromLTRB(14, 10, 14, 8),
                                decoration: BoxDecoration(
                                  color: mine
                                      ? colors.accent.withValues(alpha: 0.18)
                                      : Theme.of(context).colorScheme.surface,
                                  border: Border.all(color: colors.border),
                                  borderRadius: BorderRadius.only(
                                    topLeft: Radius.circular(mine ? 16 : 3),
                                    topRight: Radius.circular(mine ? 3 : 16),
                                    bottomLeft: const Radius.circular(16),
                                    bottomRight: const Radius.circular(16),
                                  ),
                                ),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.end,
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    SelectableText(
                                      m['body'] as String,
                                      style: Theme.of(context).textTheme.bodyMedium,
                                    ),
                                    const SizedBox(height: 5),
                                    Tooltip(
                                      message: mine
                                          ? chatReceiptDetails(m, _thread)
                                          : _time(m['sentAt']),
                                      child: Text(
                                        '${_time(m['sentAt'])}${mine ? '  ${chatDelivered(m, _thread) ? '✓✓' : '✓'} ${chatReceiptLabel(m, _thread)}' : ''}',
                                        style: Theme.of(context).textTheme.labelSmall?.copyWith(
                                          color: read && mine
                                              ? colors.tone(AppTone.blue).foreground
                                              : colors.muted,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          );
                        },
                      ),
              ),
              if (_unseen)
                TextButton.icon(
                  onPressed: _bottom,
                  icon: const Icon(Icons.arrow_downward),
                  label: const Text('Ver mensajes nuevos'),
                ),
              if (_error != null) _ChatError(message: _error!, retry: _retry ? _send : _load),
              if (_thread['canSend'] == true)
                Container(
                  padding: const EdgeInsets.fromLTRB(12, 10, 12, 12),
                  decoration: BoxDecoration(
                    color: Theme.of(context).colorScheme.surface,
                    border: Border(top: BorderSide(color: colors.border)),
                  ),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Expanded(
                        child: TextField(
                          controller: _draft,
                          onChanged: (_) => setState(() {}),
                          readOnly: _sending || _retry,
                          minLines: 1,
                          maxLines: 5,
                          maxLength: 4000,
                          textCapitalization: TextCapitalization.sentences,
                          decoration: const InputDecoration(
                            hintText: 'Escribe un mensaje…',
                            counterText: '',
                            contentPadding: EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      IconButton.filled(
                        onPressed: _sending || _retry || _draft.text.trim().isEmpty ? null : _send,
                        tooltip: 'Enviar mensaje',
                        icon: _sending
                            ? const SizedBox.square(
                                dimension: 20,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.send),
                      ),
                    ],
                  ),
                ),
              if (!_loading && _thread['canSend'] != true)
                const Padding(
                  padding: EdgeInsets.all(16),
                  child: Text(
                    'Conversación de consulta. El ciclo está cerrado o el acceso cambió.',
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ChatError extends StatelessWidget {
  const _ChatError({required this.message, required this.retry});
  final String message;
  final VoidCallback retry;
  @override
  Widget build(BuildContext context) => Semantics(
    liveRegion: true,
    child: Container(
      width: double.infinity,
      color: Theme.of(context).colorScheme.errorContainer,
      padding: const EdgeInsets.all(12),
      child: Column(
        children: [
          Text(message),
          TextButton(onPressed: retry, child: const Text('Reintentar')),
        ],
      ),
    ),
  );
}

class _ChatEmpty extends StatelessWidget {
  const _ChatEmpty({required this.title, required this.text, this.action});
  final String title, text;
  final VoidCallback? action;
  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(28),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.chat_bubble_outline, size: 48),
          const SizedBox(height: 20),
          Text(title, textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 10),
          Text(text, textAlign: TextAlign.center),
          if (action != null)
            Padding(
              padding: const EdgeInsets.only(top: 20),
              child: FilledButton(onPressed: action, child: const Text('Nueva conversación')),
            ),
        ],
      ),
    ),
  );
}
