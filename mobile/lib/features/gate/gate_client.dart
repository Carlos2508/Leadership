import 'dart:convert';

import 'package:http/http.dart' as http;

/// Device credentials are never part of a URL, log, or the demo account session.
class GatePairing {
  const GatePairing(this.server, this.token);
  final Uri server;
  final String token;

  factory GatePairing.parse(String raw) {
    try {
      if (raw.length > 4096) throw const FormatException();
      final data = jsonDecode(raw);
      if (data is! Map<String, dynamic> ||
          data['v'] != 1 ||
          data['server'] is! String ||
          data['token'] is! String) {
        throw const FormatException();
      }
      final server = Uri.parse(data['server'] as String);
      final token = data['token'] as String;
      if (server.scheme != 'https' ||
          server.host.isEmpty ||
          server.userInfo.isNotEmpty ||
          server.hasQuery ||
          server.hasFragment ||
          (server.path != '' && server.path != '/') ||
          !RegExp(r'^gd_[A-Za-z0-9_-]{43}$').hasMatch(token)) {
        throw const FormatException();
      }
      return GatePairing(server.replace(path: ''), token);
    } on Object {
      throw const GateFailure(
        'Este QR no es un emparejamiento válido. Usa la dirección HTTPS del colegio.',
      );
    }
  }
  String encode() => jsonEncode({'v': 1, 'server': server.toString(), 'token': token});
}

class GateFailure implements Exception {
  const GateFailure(this.message, {this.unauthorized = false});
  final String message;
  final bool unauthorized;
  @override
  String toString() => message;
}

class GateScan {
  const GateScan({
    required this.outcome,
    required this.message,
    this.name,
    this.code,
    this.placement,
  });
  final String outcome;
  final String message;
  final String? name;
  final String? code;
  final String? placement;
}

/// No offline queue and no automatic retries: marks are decided using the server clock.
class GateClient {
  GateClient({http.Client? httpClient}) : _http = httpClient ?? http.Client();
  final http.Client _http;

  Future<Map<String, dynamic>> _request(
    GatePairing pairing,
    String path, {
    Map<String, String>? body,
  }) async {
    final request = http.Request(body == null ? 'GET' : 'POST', pairing.server.resolve(path))
      ..followRedirects = false
      ..headers['Authorization'] = 'Bearer ${pairing.token}';
    if (body != null) {
      request.headers['Content-Type'] = 'application/json';
      request.body = jsonEncode(body);
    }
    try {
      final response = await (() async => http.Response.fromStream(
        await _http.send(request),
      ))().timeout(const Duration(seconds: 10));
      if (response.statusCode == 401 || response.statusCode == 403) {
        throw const GateFailure(
          'Acceso del teléfono revocado o inválido. Vuelve a emparejarlo.',
          unauthorized: true,
        );
      }
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw const GateFailure(
          'El servidor no confirmó la operación. Consulta el reporte antes de repetir la lectura.',
        );
      }
      final data = jsonDecode(response.body);
      if (data is! Map<String, dynamic>) throw const FormatException();
      return data;
    } on GateFailure {
      rethrow;
    } on Object {
      throw const GateFailure(
        'No recibimos confirmación del servidor. Revisa la conexión y consulta el reporte antes de repetir.',
      );
    }
  }

  Future<String> identify(GatePairing pairing) async {
    final data = await _request(pairing, '/api/v1/gate/device/whoami');
    if (data['device'] is! String || data['school'] is! String) {
      throw const GateFailure('El servidor no respondió como lector de portería.');
    }
    return '${data['school']} · ${data['device']}';
  }

  Future<GateScan> scan(GatePairing pairing, String code, String mode) async {
    if (!{'Auto', 'In', 'Out'}.contains(mode)) throw const GateFailure('Modo de lectura inválido.');
    final data = await _request(
      pairing,
      '/api/v1/gate/device/scan',
      body: {'code': code, 'mode': mode},
    );
    final student = data['student'];
    if (data['outcome'] is! String ||
        data['message'] is! String ||
        !{'Recorded', 'Duplicate', 'Unknown', 'Revoked', 'NotEnrolled'}.contains(data['outcome'])) {
      throw const GateFailure('Respuesta no reconocida. Consulta el reporte antes de repetir.');
    }
    return GateScan(
      outcome: data['outcome'] as String,
      message: data['message'] as String,
      name: student is Map<String, dynamic> ? student['name'] as String? : null,
      code: student is Map<String, dynamic> ? student['code'] as String? : null,
      placement: student is Map<String, dynamic> ? student['placement'] as String? : null,
    );
  }

  void close() => _http.close();
}
