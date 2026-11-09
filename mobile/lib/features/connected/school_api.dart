import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:http/http.dart' as http;

/// Error returned by the school server, with the user-facing message from its problem details.
class ApiFailure implements Exception {
  const ApiFailure(this.message, {this.status, this.code});

  final String message;
  final int? status;
  final String? code;

  bool get unauthorized => status == 401;

  @override
  String toString() => message;
}

/// Outcome of the password step (ADR-014).
enum LoginStep { signedIn, secondFactor }

/// Validates the server address typed by the user: HTTPS only, except localhost/emulator during development.
Uri parseServer(String raw) {
  final text = raw.trim();
  final uri = Uri.tryParse(text.contains('://') ? text : 'https://$text');
  const local = {'localhost', '127.0.0.1', '10.0.2.2'};
  if (uri == null ||
      uri.host.isEmpty ||
      uri.userInfo.isNotEmpty ||
      uri.hasQuery ||
      uri.hasFragment ||
      (uri.path != '' && uri.path != '/') ||
      !(uri.scheme == 'https' || (uri.scheme == 'http' && local.contains(uri.host)))) {
    throw const ApiFailure(
      'Escribe la dirección HTTPS del colegio, por ejemplo https://colegio.ejemplo.',
    );
  }
  return uri.replace(path: '');
}

/// HTTP client for the signed-in app. It keeps the Identity session cookie and the antiforgery cookie in memory
/// and sends `X-XSRF-TOKEN` on every write, exactly like the web. An explicit biometric opt-in may export only the
/// Identity ticket for the separate protected vault; passwords and antiforgery cookies are never persisted.
/// Every response is authorized by the server; the app never decides permissions.
class SchoolApi {
  SchoolApi(this.server, {http.Client? client}) : _http = client ?? http.Client();

  final Uri server;
  final http.Client _http;
  final Map<String, String> _cookies = {};
  String? _token;

  // Tracked separately from the cookie jar: on Flutter web the browser keeps the cookies and hides Set-Cookie.
  bool _signedIn = false;

  static const _timeout = Duration(seconds: 20);

  bool get hasSession => _signedIn;

  /// Export only this application's session cookie (including Identity chunks). Never antiforgery/2FA cookies.
  Map<String, String> sessionTicket() => Map.fromEntries(
    _cookies.entries.where((e) => RegExp(r'^SchoolPlatform\.Session(?:C[0-9]+)?$').hasMatch(e.key)),
  );

  void restoreTicket(Map<String, String> ticket) {
    if (ticket.isEmpty ||
        ticket.keys.any((k) => !RegExp(r'^SchoolPlatform\.Session(?:C[0-9]+)?$').hasMatch(k))) {
      throw const ApiFailure('Vuelve a ingresar para preparar el desbloqueo.');
    }
    _cookies.clear();
    _cookies.addAll(ticket);
    _token = null;
    _signedIn = true;
  }

  Future<dynamic> get(String path) async => _decode(await _send('GET', path));

  /// Private PDFs use the same cookie and revocation handling as the JSON APIs.
  Future<Uint8List> pdf(String path) async {
    final response = await _send('GET', path);
    if (response.statusCode != 200) {
      _decode(response);
    }
    if (!response.headers['content-type'].toString().contains('application/pdf') ||
        response.bodyBytes.length < 4 ||
        ascii.decode(response.bodyBytes.take(4).toList()) != '%PDF') {
      throw const ApiFailure('El colegio no devolvió un documento válido. Intenta de nuevo.');
    }
    return response.bodyBytes;
  }

  Future<dynamic> post(String path, [Object? body]) async {
    _token ??= await _antiforgery();
    var response = await _send('POST', path, body: body ?? const {}, token: _token);
    if (response.statusCode == 400 && _problemCode(response) == 'invalid-csrf') {
      // The token belongs to the previous identity (e.g. right after signing in): refresh once and retry.
      _token = await _antiforgery();
      response = await _send('POST', path, body: body ?? const {}, token: _token);
    }
    return _decode(response);
  }

  Future<LoginStep> login(String username, String password) async {
    _cookies.clear();
    _signedIn = false;
    _token = await _antiforgery(anonymous: true);
    final data = await _decode(
      await _send(
        'POST',
        '/api/v1/auth/login',
        body: {'username': username, 'password': password},
        token: _token,
      ),
    );
    _token = null;
    return _step(data);
  }

  Future<LoginStep> secondFactor(String code, {bool recovery = false}) async {
    _token ??= await _antiforgery(anonymous: true);
    final data = await _decode(
      await _send(
        'POST',
        '/api/v1/auth/login/second-factor',
        body: {'code': code, 'recovery': recovery},
        token: _token,
      ),
    );
    _token = null;
    return _step(data);
  }

  Future<void> logout() async {
    try {
      if (hasSession) await post('/api/v1/auth/logout');
    } finally {
      _cookies.clear();
      _token = null;
      _signedIn = false;
    }
  }

  LoginStep _step(dynamic data) {
    final status = data is Map ? data['status'] : null;
    if (status == 'signed-in') {
      _signedIn = true;
      return LoginStep.signedIn;
    }
    if (status == 'second-factor') return LoginStep.secondFactor;
    throw const ApiFailure('El servidor no respondió como la plataforma escolar.');
  }

  Future<String> _antiforgery({bool anonymous = false}) async {
    final data = await _decode(
      await _send('GET', anonymous ? '/api/v1/auth/antiforgery' : '/api/v1/antiforgery'),
    );
    final token = data is Map ? data['token'] : null;
    if (token is! String || token.isEmpty) {
      throw const ApiFailure('El servidor no respondió como la plataforma escolar.');
    }
    return token;
  }

  Future<http.Response> _send(String method, String path, {Object? body, String? token}) async {
    final request = http.Request(method, server.resolve(path))
      ..followRedirects = false
      ..headers['Accept'] = 'application/json';
    if (_cookies.isNotEmpty) {
      request.headers['Cookie'] = _cookies.entries.map((e) => '${e.key}=${e.value}').join('; ');
    }
    if (token != null) request.headers['X-XSRF-TOKEN'] = token;
    if (body != null) {
      request.headers['Content-Type'] = 'application/json';
      request.body = jsonEncode(body);
    }
    try {
      final response = await http.Response.fromStream(await _http.send(request)).timeout(_timeout);
      _storeCookies(response);
      return response;
    } on TimeoutException {
      throw const ApiFailure(
        'El colegio no respondió a tiempo. Revisa tu conexión e intenta de nuevo.',
      );
    } on ApiFailure {
      rethrow;
    } on Object {
      throw const ApiFailure(
        'No pudimos conectar con el colegio. Revisa tu conexión e intenta de nuevo.',
      );
    }
  }

  void _storeCookies(http.Response response) {
    for (final header in response.headersSplitValues['set-cookie'] ?? const <String>[]) {
      final pair = header.split(';').first;
      final index = pair.indexOf('=');
      if (index <= 0) continue;
      final name = pair.substring(0, index).trim();
      final value = pair.substring(index + 1).trim();
      final expired = header.toLowerCase().contains('expires=thu, 01 jan 1970');
      if (value.isEmpty || expired) {
        _cookies.remove(name);
      } else {
        _cookies[name] = value;
      }
    }
  }

  static String? _problemCode(http.Response response) {
    try {
      final data = jsonDecode(response.body);
      return data is Map ? data['code'] as String? : null;
    } on Object {
      return null;
    }
  }

  dynamic _decode(http.Response response) {
    final status = response.statusCode;
    if (status == 204) return null;
    dynamic data;
    try {
      data = response.body.isEmpty ? null : jsonDecode(utf8.decode(response.bodyBytes));
    } on Object {
      data = null;
    }
    if (status >= 200 && status < 300) return data;
    if (status == 401 && data is! Map) {
      _cookies.remove('SchoolPlatform.Session');
      _signedIn = false;
      throw const ApiFailure(
        'Tu sesión venció. Vuelve a ingresar.',
        status: 401,
        code: 'session-expired',
      );
    }
    if (status == 403) {
      throw const ApiFailure('Tu cuenta no tiene acceso a esta información.', status: 403);
    }
    if (status == 404 && data is! Map) {
      throw const ApiFailure(
        'No encontramos esta información o ya no está disponible.',
        status: 404,
      );
    }
    if (status == 429) {
      throw const ApiFailure(
        'Demasiados intentos. Espera un minuto e intenta de nuevo.',
        status: 429,
      );
    }
    final title = data is Map ? data['title'] as String? : null;
    throw ApiFailure(
      title ?? 'El colegio no pudo completar la operación. Intenta de nuevo.',
      status: status,
      code: data is Map ? data['code'] as String? : null,
    );
  }
}
