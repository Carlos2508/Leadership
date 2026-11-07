import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../core/providers.dart';
import 'connected_session.dart';
import 'school_api.dart';

/// True when the request never reached the school (no signal, timeout): the server did not answer, so nothing changed.
bool isOfflineFailure(Object error) => error is ApiFailure && error.status == null && error.code == null;

/// A teacher write kept on the phone until the school can be reached again.
class PendingWrite {
  PendingWrite({required this.path, required this.body, required this.label, required this.mergeKey, DateTime? createdAt})
    : createdAt = createdAt ?? DateTime.now();

  final String path;
  final Map<String, dynamic> body;
  final String label;

  /// Writes with the same key (same course + period, or course + date) are merged by student before sending.
  final String mergeKey;
  final DateTime createdAt;

  Map<String, dynamic> toJson() => {
    'path': path,
    'body': body,
    'label': label,
    'mergeKey': mergeKey,
    'createdAt': createdAt.toIso8601String(),
  };

  static PendingWrite fromJson(Map<String, dynamic> json) => PendingWrite(
    path: json['path'] as String,
    body: (json['body'] as Map).cast<String, dynamic>(),
    label: json['label'] as String,
    mergeKey: json['mergeKey'] as String,
    createdAt: DateTime.tryParse(json['createdAt'] as String? ?? ''),
  );
}

class SyncResult {
  const SyncResult(this.sent, this.failed, this.remaining);
  final int sent;
  final List<String> failed;
  final int remaining;
}

/// Offline support for daily teaching: the last class lists and grade sheets seen are kept per account so attendance
/// and grades can be captured without signal; the writes wait in a queue and are sent in order when the school answers.
/// The server still validates every write (permissions, row versions): a write that no longer applies is reported and
/// discarded, never forced. Only this account's data is kept; signing out clears the copies of class lists.
class OfflineStore {
  OfflineStore(this._prefs, String account) : _prefix = 'offline.${base64Url.encode(utf8.encode(account))}';

  final SharedPreferences _prefs;
  final String _prefix;
  static const _maxCached = 60;

  String get _queueKey => '$_prefix.queue';
  String get _indexKey => '$_prefix.index';

  /// Paths whose last answer is kept for offline use: own courses, grade sheets and class lists.
  static bool cacheable(String path) =>
      path.startsWith('/api/v1/courses/') && (path.contains('/grades') || path.contains('/attendance')) ||
      path == '/api/v1/me/courses';

  Future<void> remember(String path, dynamic data) async {
    if (!cacheable(path)) return;
    await _prefs.setString('$_prefix.cache.$path', jsonEncode(data));
    final attendance = RegExp(r'^/api/v1/courses/([^/]+)/attendance').firstMatch(path);
    if (attendance != null) await _prefs.setString('$_prefix.attendance.${attendance.group(1)}', jsonEncode(data));
    final index = _prefs.getStringList(_indexKey) ?? [];
    index.remove(path);
    index.add(path);
    while (index.length > _maxCached) {
      await _prefs.remove('$_prefix.cache.${index.removeAt(0)}');
    }
    await _prefs.setStringList(_indexKey, index);
  }

  /// Last copy of the answer; for a class list of another date, the latest list of that course with nothing marked.
  dynamic recall(String path) {
    final raw = _prefs.getString('$_prefix.cache.$path');
    if (raw != null) return _offline(jsonDecode(raw));
    final attendance = RegExp(r'^/api/v1/courses/([^/]+)/attendance\?date=([0-9-]+)').firstMatch(path);
    if (attendance == null) return null;
    final latest = _prefs.getString('$_prefix.attendance.${attendance.group(1)}');
    if (latest == null) return null;
    final sheet = (jsonDecode(latest) as Map).cast<String, dynamic>();
    sheet['date'] = attendance.group(2);
    sheet['rows'] = [
      for (final r in (sheet['rows'] as List).cast<Map>())
        {...r.cast<String, dynamic>(), 'status': null, 'note': '', 'version': null},
    ];
    return _offline(sheet);
  }

  static dynamic _offline(dynamic data) {
    if (data is Map) return {...data.cast<String, dynamic>(), 'offline': true};
    return data;
  }

  List<PendingWrite> pending() {
    final raw = _prefs.getString(_queueKey);
    if (raw == null) return [];
    return [for (final item in (jsonDecode(raw) as List).cast<Map>()) PendingWrite.fromJson(item.cast<String, dynamic>())];
  }

  Future<void> _save(List<PendingWrite> items) async {
    if (items.isEmpty) {
      await _prefs.remove(_queueKey);
    } else {
      await _prefs.setString(_queueKey, jsonEncode([for (final i in items) i.toJson()]));
    }
  }

  /// Adds a write; entries of the same student in a waiting write with the same key are replaced by the newest.
  Future<void> enqueue(PendingWrite write) async {
    final items = pending();
    final index = items.indexWhere((i) => i.mergeKey == write.mergeKey);
    if (index < 0) {
      items.add(write);
    } else {
      final previous = items[index];
      final entries = <String, Map<String, dynamic>>{
        for (final e in (previous.body['entries'] as List).cast<Map>()) e['studentId'] as String: e.cast<String, dynamic>(),
      };
      for (final e in (write.body['entries'] as List).cast<Map>()) {
        final old = entries[e['studentId'] as String];
        // Keep the version the first offline edit was based on: the server has not seen any of them yet.
        entries[e['studentId'] as String] = {...e.cast<String, dynamic>(), if (old != null) 'version': old['version']};
      }
      items[index] = PendingWrite(
        path: previous.path,
        body: {...write.body, 'entries': entries.values.toList()},
        label: write.label,
        mergeKey: write.mergeKey,
        createdAt: previous.createdAt,
      );
    }
    await _save(items);
  }

  /// Sends the waiting writes in order. Stops at the first one that cannot reach the school.
  Future<SyncResult> flush(SchoolApi api) async {
    final items = pending();
    var sent = 0;
    final failed = <String>[];
    while (items.isNotEmpty) {
      final item = items.first;
      try {
        await api.post(item.path, item.body);
        sent++;
      } on ApiFailure catch (e) {
        if (isOfflineFailure(e) || e.unauthorized) break;
        failed.add('${item.label}: ${e.message}');
      }
      items.removeAt(0);
      await _save(items);
    }
    return SyncResult(sent, failed, items.length);
  }

  /// Removes the copies of class lists and grade sheets (the waiting writes are kept for this account).
  Future<void> forgetCopies() async {
    for (final key in _prefs.getKeys().where((k) => k.startsWith('$_prefix.cache.') || k.startsWith('$_prefix.attendance.')).toList()) {
      await _prefs.remove(key);
    }
    await _prefs.remove(_indexKey);
  }
}

final offlineStoreProvider = Provider<OfflineStore?>((ref) {
  final session = ref.watch(connectedControllerProvider);
  if (session == null) return null;
  return OfflineStore(ref.watch(sharedPreferencesProvider), '${session.api.server.host}|${session.username}');
});

/// Number of writes waiting on this phone; screens refresh it after enqueueing or syncing.
class PendingCount extends Notifier<int> {
  @override
  int build() => ref.watch(offlineStoreProvider)?.pending().length ?? 0;
  void refresh() => state = ref.read(offlineStoreProvider)?.pending().length ?? 0;
}

final pendingCountProvider = NotifierProvider<PendingCount, int>(PendingCount.new);
