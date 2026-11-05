import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import 'gate_client.dart';

/// This screen uses device authentication, independently of the prototype roles.
class GateReaderPage extends StatefulWidget {
  const GateReaderPage({super.key, this.client});
  final GateClient? client;
  @override
  State<GateReaderPage> createState() => _GateReaderPageState();
}

class _GateReaderPageState extends State<GateReaderPage> with WidgetsBindingObserver {
  static const _storage = FlutterSecureStorage();
  static const _storageKey = 'gate.device.v1';
  late final _client = widget.client ?? GateClient();
  final _code = TextEditingController();
  GatePairing? _pairing;
  GateScan? _result;
  String _name = '';
  String _error = '';
  String _mode = 'In';
  bool _busy = true;
  bool _camera = false;
  bool _foreground = true;
  DateTime? _lastRead;
  String? _lastCode;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    unawaited(_restore());
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (mounted) setState(() => _foreground = state == AppLifecycleState.resumed);
  }

  Future<void> _restore() async {
    try {
      if (!kIsWeb) {
        final raw = await _storage.read(key: _storageKey);
        if (raw != null) {
          final pairing = GatePairing.parse(raw);
          final name = await _client.identify(pairing);
          if (mounted) {
            setState(() {
              _pairing = pairing;
              _name = name;
            });
          }
        }
      }
    } on Object {
      if (mounted) {
        setState(() => _error = 'No se pudo restaurar el lector. Empareja el teléfono nuevamente.');
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _client.close();
    _code.dispose();
    super.dispose();
  }

  Future<void> _pair(String raw) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _camera = false;
      _error = '';
    });
    try {
      final pairing = GatePairing.parse(raw);
      final approved = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Emparejar lector'),
          content: Text(
            '¿Conectar este teléfono con ${pairing.server.host}? Confirma que es el servidor del colegio.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Conectar'),
            ),
          ],
        ),
      );
      if (approved != true || !mounted) return;
      final name = await _client.identify(pairing);
      if (!mounted) return;
      if (!kIsWeb) await _storage.write(key: _storageKey, value: pairing.encode());
      if (mounted) {
        setState(() {
          _pairing = pairing;
          _name = name;
          _result = null;
          _code.clear();
        });
      }
    } on GateFailure catch (e) {
      if (mounted) setState(() => _error = e.message);
    } on Object {
      if (mounted) {
        setState(
          () => _error =
              'No se pudo guardar el acceso de forma segura. Intenta emparejar nuevamente.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _forget() async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _camera = false;
      _error = '';
    });
    try {
      if (!kIsWeb) await _storage.delete(key: _storageKey);
      if (mounted) {
        setState(() {
          _pairing = null;
          _result = null;
          _name = '';
          _code.clear();
          _lastCode = null;
        });
      }
    } on Object {
      if (mounted) {
        setState(
          () => _error = 'No se pudo borrar el acceso guardado. Revoca el teléfono desde la web.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _scan(String code) async {
    final pairing = _pairing;
    if (_busy || pairing == null || code.trim().isEmpty) return;
    setState(() {
      _busy = true;
      _error = '';
      _result = null;
    });
    try {
      final result = await _client.scan(pairing, code.trim(), _mode);
      if (mounted) {
        setState(() {
          _result = result;
          _code.clear();
        });
      }
    } on GateFailure catch (e) {
      if (e.unauthorized) {
        if (mounted) {
          setState(() {
            _pairing = null;
            _camera = false;
          });
        }
        if (!kIsWeb) {
          try {
            await _storage.delete(key: _storageKey);
          } on Object {
            /* Server already rejects this credential. */
          }
        }
      }
      if (mounted) setState(() => _error = e.message);
    } on Object {
      if (mounted) setState(() => _error = 'No se pudo confirmar la lectura. Consulta el reporte.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _detect(BarcodeCapture capture) {
    if (_busy || !_foreground) return;
    final code = capture.barcodes.firstOrNull?.rawValue;
    if (code == null || code.isEmpty) return;
    final now = DateTime.now();
    if (code == _lastCode &&
        _lastRead != null &&
        now.difference(_lastRead!) < const Duration(seconds: 3)) {
      return;
    }
    _lastCode = code;
    _lastRead = now;
    if (_pairing == null) {
      unawaited(_pair(code));
    } else {
      unawaited(_scan(code));
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Lector de portería'),
      leading: IconButton(
        tooltip: 'Volver',
        onPressed: () => context.pop(),
        icon: const Icon(Icons.arrow_back),
      ),
    ),
    body: SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 560),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  _pairing == null ? 'Emparejar teléfono' : _name,
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 12),
                Text(
                  _pairing == null
                      ? 'En la web, abre Teléfonos lectores, registra este teléfono y escanea su QR. Se requiere HTTPS.'
                      : 'Las marcas se confirman en el servidor. Se necesita conexión.',
                ),
                if (kIsWeb)
                  const Padding(
                    padding: EdgeInsets.only(top: 8),
                    child: Text(
                      'En el navegador el acceso se conserva solo mientras esta pantalla esté abierta.',
                    ),
                  ),
                const SizedBox(height: 16),
                if (_pairing != null)
                  DropdownButtonFormField<String>(
                    initialValue: _mode,
                    decoration: const InputDecoration(labelText: 'Modo de lectura'),
                    items: const [
                      DropdownMenuItem(value: 'In', child: Text('Solo entrada')),
                      DropdownMenuItem(value: 'Out', child: Text('Solo salida')),
                      DropdownMenuItem(value: 'Auto', child: Text('Automático')),
                    ],
                    onChanged: _busy
                        ? null
                        : (mode) => setState(() {
                            _mode = mode!;
                            _lastCode = null;
                          }),
                  ),
                const SizedBox(height: 16),
                if (_camera && _foreground)
                  SizedBox(
                    height: 280,
                    child: MobileScanner(
                      onDetect: _detect,
                      errorBuilder: (_, _) => const Center(
                        child: Text(
                          'Cámara no disponible. Revisa el permiso de cámara o escribe el código.',
                        ),
                      ),
                    ),
                  ),
                FilledButton.icon(
                  onPressed: _busy ? null : () => setState(() => _camera = !_camera),
                  icon: const Icon(Icons.qr_code_scanner),
                  label: Text(
                    _camera
                        ? 'Detener cámara'
                        : _pairing == null
                        ? 'Escanear QR de emparejamiento'
                        : 'Escanear carnet',
                  ),
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: _code,
                  enabled: !_busy,
                  obscureText: _pairing == null,
                  autocorrect: false,
                  enableSuggestions: false,
                  decoration: InputDecoration(
                    labelText: _pairing == null
                        ? 'Contenido del QR (alternativa)'
                        : 'Código del carnet',
                  ),
                  onSubmitted: (value) => _pairing == null ? _pair(value) : _scan(value),
                ),
                TextButton(
                  onPressed: _busy
                      ? null
                      : () => _pairing == null ? _pair(_code.text) : _scan(_code.text),
                  child: Text(_pairing == null ? 'Leer emparejamiento' : 'Registrar lectura'),
                ),
                if (_busy) const Center(child: CircularProgressIndicator()),
                if (_error.isNotEmpty)
                  Semantics(
                    liveRegion: true,
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      child: Text(
                        _error,
                        style: TextStyle(color: Theme.of(context).colorScheme.error),
                      ),
                    ),
                  ),
                if (_result case final result?)
                  Semantics(
                    liveRegion: true,
                    child: Card(
                      child: Padding(
                        padding: const EdgeInsets.all(20),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(result.message, style: Theme.of(context).textTheme.titleMedium),
                            if (result.name != null) Text(result.name!),
                            if (result.code != null)
                              Text('${result.code} · ${result.placement ?? ''}'),
                          ],
                        ),
                      ),
                    ),
                  ),
                if (_pairing != null)
                  TextButton(
                    onPressed: _busy ? null : _forget,
                    child: const Text('Desvincular este teléfono'),
                  ),
              ],
            ),
          ),
        ),
      ),
    ),
  );
}
