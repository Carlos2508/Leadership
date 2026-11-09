import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/cards.dart';
import '../../core/widgets/states.dart';
import '../../shared/page_layout.dart';
import 'connected_session.dart';
import 'school_api.dart';

typedef InventoryMap = Map<String, dynamic>;
const inventoryKinds = {'In': 'Entrada', 'Out': 'Salida', 'Count': 'Conteo físico'};
String _attemptId() {
  final bytes = List<int>.generate(16, (_) => Random.secure().nextInt(256));
  bytes[6] = (bytes[6] & 15) | 64;
  bytes[8] = (bytes[8] & 63) | 128;
  final s = bytes.map((v) => v.toRadixString(16).padLeft(2, '0')).join();
  return '${s.substring(0, 8)}-${s.substring(8, 12)}-${s.substring(12, 16)}-${s.substring(16, 20)}-${s.substring(20)}';
}

String _stockState(InventoryMap p) => p['isActive'] != true
    ? 'Archivado'
    : p['onHand'] == 0
    ? 'Agotado'
    : p['lowStock'] == true
    ? 'Stock bajo'
    : 'Disponible';

class InventoryPage extends ConsumerStatefulWidget {
  const InventoryPage({super.key});
  @override
  ConsumerState<InventoryPage> createState() => _InventoryPageState();
}

class _InventoryPageState extends ConsumerState<InventoryPage> {
  final _search = TextEditingController();
  String _status = 'active';
  bool _low = false, _loading = true;
  String? _error;
  List<InventoryMap> _rows = [];
  InventoryMap? _summary;
  int _page = 1, _total = 0, _generation = 0;
  SchoolApi get _api => ref.read(connectedControllerProvider)!.api;
  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _load([int page = 1]) async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _error = null;
      _rows = [];
      _summary = null;
    });
    try {
      final values = await Future.wait([
        _api.get(
          '/api/v1/inventory/products?search=${Uri.encodeQueryComponent(_search.text.trim())}&status=$_status&low=$_low&page=$page',
        ),
        _api.get('/api/v1/inventory/summary'),
      ]);
      if (!mounted || generation != _generation) return;
      final list = values[0] as InventoryMap;
      setState(() {
        _rows = (list['items'] as List).cast<InventoryMap>();
        _total = list['total'] as int;
        _summary = values[1] as InventoryMap;
        _page = page;
      });
    } on Object catch (e) {
      if (mounted && generation == _generation) setState(() => _error = e.toString());
    } finally {
      if (mounted && generation == _generation) setState(() => _loading = false);
    }
  }

  Future<void> _create() async {
    if (await Navigator.of(context)
                .push<bool>(MaterialPageRoute(builder: (_) => const InventoryEditorPage())) ==
            true &&
        mounted) {
      await _load();
    }
  }

  @override
  Widget build(BuildContext context) {
    final canManage = ref.watch(connectedControllerProvider)!.can('inventory.manage');
    final colors = AppColors.of(context);
    return DetailPage(
      title: 'Inventario',
      onRefresh: _load,
      children: [
        const PageIntro(
          eyebrow: 'Inventario',
          title: 'Artículos y existencias',
          subtitle: 'Entradas, salidas y conteos físicos en un solo lugar.',
        ),
        if (_summary case final summary?)
          Padding(
            padding: const EdgeInsets.only(bottom: Spacing.lg),
            child: AppCard(
              child: Wrap(
                spacing: 24,
                runSpacing: 12,
                children: [
                  Text('Artículos: ${summary['activeProducts']}'),
                  Text('Unidades: ${summary['units']}'),
                  Text('Stock bajo: ${summary['lowStockProducts']}'),
                ],
              ),
            ),
          ),
        TextField(
          controller: _search,
          textInputAction: TextInputAction.search,
          onSubmitted: (_) => _load(),
          decoration: InputDecoration(
            labelText: 'Buscar artículo',
            hintText: 'Código, nombre o categoría',
            suffixIcon: IconButton(
              tooltip: 'Buscar',
              onPressed: _load,
              icon: const Icon(Icons.search),
            ),
          ),
        ),
        const SizedBox(height: Spacing.md),
        Wrap(
          spacing: 12,
          runSpacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            DropdownButton<String>(
              value: _status,
              items: const [
                DropdownMenuItem(value: 'active', child: Text('Activos')),
                DropdownMenuItem(value: 'archived', child: Text('Archivados')),
                DropdownMenuItem(value: 'all', child: Text('Todos')),
              ],
              onChanged: (v) {
                if (v != null) {
                  setState(() => _status = v);
                  unawaited(_load());
                }
              },
            ),
            FilterChip(
              label: const Text('Stock bajo'),
              selected: _low,
              onSelected: (v) {
                setState(() => _low = v);
                unawaited(_load());
              },
            ),
            if (canManage)
              FilledButton.icon(
                onPressed: _create,
                icon: const Icon(Icons.add),
                label: const Text('Nuevo artículo'),
              ),
          ],
        ),
        const SizedBox(height: Spacing.lg),
        if (_loading)
          const LoadingState(itemCount: 3)
        else if (_error != null)
          ErrorState(message: _error!, onRetry: _load)
        else if (_rows.isEmpty)
          const EmptyState(
            icon: Icons.inventory_2_outlined,
            title: 'No hay artículos en esta lista',
            message: 'Cambia los filtros o registra el primer artículo.',
          )
        else ...[
          for (final p in _rows)
            Padding(
              padding: const EdgeInsets.only(bottom: Spacing.sm),
              child: AppCard(
                onTap: () async {
                  await Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => InventoryProductPage(id: p['id'] as String)),
                  );
                  if (mounted) await _load(_page);
                },
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(p['name'] as String, style: Theme.of(context).textTheme.titleSmall),
                          const SizedBox(height: 4),
                          Text(
                            '${p['code']} · ${p['category'] == '' ? 'Sin categoría' : p['category']}',
                            style: Theme.of(context).textTheme.bodySmall,
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 12),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Text('${p['onHand']}', style: Theme.of(context).textTheme.titleLarge),
                        Text(
                          _stockState(p),
                          style: Theme.of(context).textTheme.labelSmall?.copyWith(
                            color: p['lowStock'] == true
                                ? colors.tone(AppTone.warning).foreground
                                : colors.muted,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          if (_total > 40)
            Wrap(
              spacing: 12,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                OutlinedButton(
                  onPressed: _page > 1 ? () => _load(_page - 1) : null,
                  child: const Text('Anterior'),
                ),
                Text('Página $_page'),
                OutlinedButton(
                  onPressed: _page * 40 < _total ? () => _load(_page + 1) : null,
                  child: const Text('Siguiente'),
                ),
              ],
            ),
        ],
      ],
    );
  }
}

class InventoryProductPage extends ConsumerStatefulWidget {
  const InventoryProductPage({required this.id, super.key});
  final String id;
  @override
  ConsumerState<InventoryProductPage> createState() => _InventoryProductPageState();
}

class _InventoryProductPageState extends ConsumerState<InventoryProductPage> {
  InventoryMap? _product;
  List<InventoryMap> _movements = [];
  String? _error;
  bool _loading = true;
  int _page = 1, _total = 0, _generation = 0;
  SchoolApi get _api => ref.read(connectedControllerProvider)!.api;
  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _error = null;
      _product = null;
      _movements = [];
    });
    try {
      final values = await Future.wait([
        _api.get('/api/v1/inventory/products/${widget.id}'),
        _api.get('/api/v1/inventory/products/${widget.id}/movements?page=$_page'),
      ]);
      if (mounted && generation == _generation) {
        setState(() {
          _product = values[0] as InventoryMap;
          final history = values[1] as InventoryMap;
          _movements = (history['items'] as List).cast<InventoryMap>();
          _total = history['total'] as int;
        });
      }
    } on Object catch (e) {
      if (mounted && generation == _generation) setState(() => _error = e.toString());
    } finally {
      if (mounted && generation == _generation) setState(() => _loading = false);
    }
  }

  Future<void> _edit([String? kind]) async {
    if (await Navigator.of(context).push<bool>(
              MaterialPageRoute(
                builder: (_) => InventoryEditorPage(product: _product, kind: kind),
              ),
            ) ==
            true &&
        mounted) {
      _page = 1;
      await _load();
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(connectedControllerProvider)!;
    final p = _product;
    return DetailPage(
      title: 'Detalle del artículo',
      onRefresh: _load,
      children: [
        if (_loading)
          const LoadingState()
        else if (_error != null)
          ErrorState(message: _error!, onRetry: _load)
        else if (p != null) ...[
          PageIntro(
            eyebrow: p['code'] as String,
            title: p['name'] as String,
            subtitle:
                '${p['category'] == '' ? 'Sin categoría' : p['category']} · ${p['location'] == '' ? 'Sin ubicación' : p['location']}',
          ),
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('${p['onHand']} unidades', style: Theme.of(context).textTheme.headlineMedium),
                Text('${_stockState(p)} · Mínimo: ${p['minimum']}'),
                const SizedBox(height: Spacing.md),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    if (p['isActive'] == true && session.can('inventory.move')) ...[
                      FilledButton(onPressed: () => _edit('In'), child: const Text('Entrada')),
                      OutlinedButton(
                        onPressed: (p['onHand'] as num) > 0 ? () => _edit('Out') : null,
                        child: const Text('Salida'),
                      ),
                    ],
                    if (p['isActive'] == true && session.can('inventory.manage'))
                      OutlinedButton(
                        onPressed: () => _edit('Count'),
                        child: const Text('Conteo físico'),
                      ),
                    if (session.can('inventory.manage'))
                      OutlinedButton(onPressed: _edit, child: const Text('Editar artículo')),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: Spacing.lg),
          Text('Historial de movimientos', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: Spacing.md),
          if (_movements.isEmpty) const Text('Todavía no hay movimientos.'),
          for (final m in _movements)
            Padding(
              padding: const EdgeInsets.only(bottom: Spacing.sm),
              child: AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${inventoryKinds[m['kind']]}  ${(m['delta'] as num) > 0 ? '+' : ''}${m['delta']}',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                    Text('${m['before']} → ${m['after']} unidades'),
                    const SizedBox(height: 6),
                    Text(m['reason'] as String),
                    if (m['reference'] != '') Text('Referencia: ${m['reference']}'),
                    Text(
                      '${DateFormat('dd/MM/yyyy HH:mm').format(DateTime.parse(m['occurredAt'] as String).toLocal())} · ${m['actor']}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
            ),
          if (_total > 40)
            Wrap(
              spacing: 12,
              children: [
                OutlinedButton(
                  onPressed: _page > 1
                      ? () {
                          _page--;
                          unawaited(_load());
                        }
                      : null,
                  child: const Text('Más recientes'),
                ),
                OutlinedButton(
                  onPressed: _page * 40 < _total
                      ? () {
                          _page++;
                          unawaited(_load());
                        }
                      : null,
                  child: const Text('Más antiguos'),
                ),
              ],
            ),
        ],
      ],
    );
  }
}

class InventoryEditorPage extends ConsumerStatefulWidget {
  const InventoryEditorPage({this.product, this.kind, super.key});
  final InventoryMap? product;
  final String? kind;
  @override
  ConsumerState<InventoryEditorPage> createState() => _InventoryEditorPageState();
}

class _InventoryEditorPageState extends ConsumerState<InventoryEditorPage> {
  final _form = GlobalKey<FormState>();
  final _fields = <String, TextEditingController>{};
  final _requestId = _attemptId();
  InventoryMap? _product, _intent;
  bool _active = true, _busy = false, _saved = false;
  String? _error, _url;
  late String _baseline;
  bool get _new => widget.product == null;
  bool get _move => widget.kind != null;
  String get _title => _move
      ? inventoryKinds[widget.kind]!
      : _new
      ? 'Nuevo artículo'
      : 'Editar artículo';
  SchoolApi get _api => ref.read(connectedControllerProvider)!.api;
  String _value(String key) => _fields[key]!.text;
  @override
  void initState() {
    super.initState();
    _product = widget.product == null ? null : Map.of(widget.product!);
    for (final key in [
      'code',
      'name',
      'category',
      'location',
      'minimum',
      'initialQuantity',
      'quantity',
      'reference',
      'reason',
    ]) {
      final value = switch (key) {
        'minimum' => '${_product?['minimum'] ?? 0}',
        'initialQuantity' => '0',
        'quantity' => widget.kind == 'Count' ? '${_product?['onHand'] ?? 0}' : '1',
        'reason' =>
          _move
              ? widget.kind == 'In'
                    ? 'Ingreso de artículos a bodega.'
                    : widget.kind == 'Out'
                    ? 'Entrega de artículos de bodega.'
                    : 'Conteo físico de existencias.'
              : _new
              ? 'Registro inicial de artículo.'
              : 'Actualización de ficha del artículo.',
        _ => _product?[key] as String? ?? '',
      };
      _fields[key] = TextEditingController(text: value);
    }
    _active = _product?['isActive'] as bool? ?? true;
    _baseline = _snapshot();
  }

  @override
  void dispose() {
    for (final field in _fields.values) {
      field.dispose();
    }
    super.dispose();
  }

  String _snapshot() => '${_fields.values.map((f) => f.text).toList()}|$_active';
  Future<void> _back() async {
    if (_busy) return;
    if (!_saved && (_intent != null || _snapshot() != _baseline)) {
      final leave = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(
            _intent != null ? 'Hay un registro sin confirmar' : 'Hay cambios sin guardar',
          ),
          content: Text(
            _intent != null
                ? 'Podría haberse guardado. Revisa el historial antes de repetir la operación. ¿Quieres salir?'
                : '¿Quieres descartar los cambios?',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Seguir revisando'),
            ),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Salir')),
          ],
        ),
      );
      if (leave != true || !mounted) return;
    }
    if (mounted) {
      setState(() => _saved = true);
      Navigator.of(context).pop(true);
    }
  }

  Future<void> _save() async {
    if (_busy || (_intent == null && !_form.currentState!.validate())) return;
    if (_intent == null) {
      final id = _product?['id'];
      _url =
          '/api/v1/inventory/products${_new ? '' : '/$id${_move
                    ? widget.kind == 'Count'
                          ? '/counts'
                          : '/movements'
                    : ''}'}';
      _intent = _move
          ? {
              'requestId': _requestId,
              'kind': widget.kind,
              'quantity': int.parse(_value('quantity')),
              'reference': _value('reference'),
              'reason': _value('reason'),
              'version': _product!['version'],
            }
          : {
              'code': _value('code'),
              'name': _value('name'),
              'category': _value('category'),
              'location': _value('location'),
              'minimum': int.parse(_value('minimum')),
              'reason': _value('reason'),
              if (_new) ...{
                'requestId': _requestId,
                'initialQuantity': int.parse(_value('initialQuantity')),
              } else ...{
                'version': _product!['version'],
                'isActive': _active,
              },
            };
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await _api.post(_url!, _intent!);
      if (mounted) {
        setState(() => _saved = true);
        Navigator.of(context).pop(true);
      }
    } on Object catch (e) {
      if (!mounted) return;
      if (e is ApiFailure && [400, 401, 403, 404, 409, 429].contains(e.status)) _intent = null;
      setState(
        () => _error = _intent == null ? e.toString() : 'No se pudo confirmar el registro. Reintenta: se conserva el mismo intento para evitar duplicados.',
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _refresh() async {
    if (_product == null || _busy || _intent != null) return;
    if (!_move) {
      final confirm = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Recargar ficha'),
          content: const Text(
            'Se reemplazarán los cambios del formulario con la ficha actual para revisarlos.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Recargar'),
            ),
          ],
        ),
      );
      if (confirm != true || !mounted) return;
    }
    setState(() => _busy = true);
    try {
      final product =
          await _api.get('/api/v1/inventory/products/${_product!['id']}') as InventoryMap;
      if (mounted) {
        setState(() {
          _product = product;
          _error = null;
          if (!_move) {
            for (final key in ['code', 'name', 'category', 'location', 'minimum']) {
              _fields[key]!.text = '${product[key]}';
            }
            _active = product['isActive'] as bool;
          }
        });
      }
    } on Object catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _field(
    String key,
    String label, {
    bool number = false,
    int min = 0,
    int max = 100,
    bool required = false,
    bool readOnly = false,
  }) => Padding(
    padding: const EdgeInsets.only(bottom: Spacing.md),
    child: TextFormField(
      controller: _fields[key],
      readOnly: _busy || _intent != null || readOnly,
      maxLength: number ? 7 : max,
      keyboardType: number ? TextInputType.number : TextInputType.text,
      inputFormatters: number ? [FilteringTextInputFormatter.digitsOnly] : null,
      decoration: InputDecoration(labelText: label, counterText: ''),
      onChanged: (_) {
        if (key == 'quantity') setState(() {});
      },
      validator: (value) {
        if (number) {
          final n = int.tryParse(value ?? '');
          return n == null || n < min || n > 1000000
              ? 'Escribe un entero entre $min y 1,000,000.'
              : null;
        }
        final text = value?.trim() ?? '';
        return (required && text.length < min) ? 'Completa al menos $min caracteres.' : null;
      },
    ),
  );
  @override
  Widget build(BuildContext context) {
    final quantity = int.tryParse(_value('quantity')) ?? 0;
    final before = (_product?['onHand'] as num?)?.toInt() ?? 0;
    final after = widget.kind == 'In'
        ? before + quantity
        : widget.kind == 'Out'
        ? before - quantity
        : quantity;
    final canMove = ref.watch(connectedControllerProvider)!.can('inventory.move');
    return PopScope(
      canPop: _saved,
      onPopInvokedWithResult: (didPop, result) {
        if (!didPop) unawaited(_back());
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(_title),
          leading: BackButton(onPressed: _back),
        ),
        body: PageBody(
          children: [
            Form(
              key: _form,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (_product != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: Spacing.lg),
                      child: Text(
                        '${_product!['code']} · ${_product!['name']}',
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                    ),
                  if (_move) ...[
                    Padding(
                      padding: const EdgeInsets.only(bottom: Spacing.lg),
                      child: AppCard(
                        child: Wrap(
                          spacing: 24,
                          children: [Text('Disponible: $before'), Text('Quedará: $after')],
                        ),
                      ),
                    ),
                    _field(
                      'quantity',
                      widget.kind == 'Count'
                          ? 'Unidades contadas físicamente'
                          : 'Cantidad de unidades',
                      number: true,
                      min: widget.kind == 'Count' ? 0 : 1,
                    ),
                    if (widget.kind == 'Count')
                      const Padding(
                        padding: EdgeInsets.only(bottom: Spacing.md),
                        child: Text(
                          'Escribe el total que encontraste. El ajuste conservará su motivo y tu cuenta.',
                        ),
                      ),
                    _field('reference', 'Referencia (opcional)'),
                  ] else ...[
                    _field('code', 'Código del artículo', required: true, min: 1, max: 40),
                    _field('name', 'Nombre del artículo', required: true, min: 2, max: 160),
                    _field('category', 'Categoría', max: 80),
                    _field('location', 'Ubicación'),
                    _field('minimum', 'Stock mínimo', number: true),
                    if (_new)
                      _field(
                        'initialQuantity',
                        'Unidades iniciales',
                        number: true,
                        readOnly: !canMove,
                      ),
                    if (!_new)
                      SwitchListTile(
                        contentPadding: EdgeInsets.zero,
                        title: const Text('Artículo activo'),
                        subtitle: const Text('Para archivar, primero debe quedar sin existencias.'),
                        value: _active,
                        onChanged: _busy || _intent != null || before > 0
                            ? null
                            : (v) => setState(() => _active = v),
                      ),
                  ],
                  _field(
                    'reason',
                    _move ? 'Motivo del movimiento' : 'Motivo del registro',
                    required: true,
                    min: 8,
                    max: 500,
                  ),
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: Spacing.md),
                      child: Text(
                        _error!,
                        style: TextStyle(color: Theme.of(context).colorScheme.error),
                      ),
                    ),
                  if (_error != null && _intent == null && !_new)
                    OutlinedButton(
                      onPressed: _busy ? null : _refresh,
                      child: const Text('Actualizar existencia y revisar'),
                    ),
                  FilledButton(
                    onPressed: _busy ? null : _save,
                    child: Text(
                      _busy
                          ? 'Guardando…'
                          : _intent != null
                          ? 'Reintentar registro'
                          : _move
                          ? 'Registrar ${inventoryKinds[widget.kind]!.toLowerCase()}'
                          : 'Guardar artículo',
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
