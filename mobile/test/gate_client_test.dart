import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:school_platform_app/features/gate/gate_client.dart';

void main() {
  final token = 'gd_${List.filled(43, 'a').join()}';
  String payload({String server = 'https://school.example', String? secret}) =>
      jsonEncode({'v': 1, 'server': server, 'token': secret ?? token});

  test('pairing requires a valid HTTPS origin and a device credential', () {
    final paired = GatePairing.parse(payload());
    expect(paired.server.host, 'school.example');
    expect(GatePairing.parse(paired.encode()).token, token);
    for (final server in [
      'http://school.example',
      'https://user:pass@school.example',
      'https://school.example/path',
      'https://school.example?token=bad',
      'https://school.example#fragment',
    ]) {
      expect(() => GatePairing.parse(payload(server: server)), throwsA(isA<GateFailure>()));
    }
    for (final raw in [
      '{}',
      'not-json',
      payload(secret: 'password'),
      jsonEncode({'v': 2, 'server': 'https://school.example', 'token': token}),
    ]) {
      expect(() => GatePairing.parse(raw), throwsA(isA<GateFailure>()));
    }
  });

  test('only sends bearer credentials to the paired device endpoints, without redirects', () async {
    final pairing = GatePairing.parse(payload());
    final requests = <http.Request>[];
    final client = GateClient(
      httpClient: MockClient((request) async {
        requests.add(request);
        expect(request.headers['Authorization'], 'Bearer $token');
        expect(request.headers.containsKey('cookie'), isFalse);
        expect(request.followRedirects, isFalse);
        expect(request.url.query, isEmpty);
        if (request.method == 'GET') {
          return http.Response(
            jsonEncode({'school': 'Synthetic school', 'device': 'Gate reader'}),
            200,
          );
        }
        expect(jsonDecode(request.body), {'code': '0123456789AB', 'mode': 'In'});
        return http.Response(
          jsonEncode({
            'outcome': 'Duplicate',
            'message': 'Entrada ya registrada.',
            'student': {'name': 'Sara Sintética', 'code': 'S-1', 'placement': 'Cuarto'},
          }),
          200,
        );
      }),
    );
    addTearDown(client.close);
    expect(await client.identify(pairing), 'Synthetic school · Gate reader');
    final result = await client.scan(pairing, '0123456789AB', 'In');
    expect(result.outcome, 'Duplicate');
    expect(result.name, 'Sara Sintética');
    expect(requests.map((r) => r.url.path), [
      '/api/v1/gate/device/whoami',
      '/api/v1/gate/device/scan',
    ]);
  });

  test('revocation is explicit and failed scans never fabricate a mark or retry', () async {
    for (final status in [401, 403, 302, 429, 500]) {
      var calls = 0;
      final client = GateClient(
        httpClient: MockClient((request) async {
          calls++;
          return http.Response('', status);
        }),
      );
      addTearDown(client.close);
      await expectLater(
        client.scan(GatePairing.parse(payload()), '0123456789AB', 'Auto'),
        throwsA(
          isA<GateFailure>().having(
            (e) => e.unauthorized,
            'revoked',
            status == 401 || status == 403,
          ),
        ),
      );
      expect(calls, 1);
    }
  });

  test('network errors and invalid server data cannot show a successful mark', () async {
    final responses = [
      http.Response('{bad json', 200),
      http.Response(jsonEncode({'outcome': 'MadeUp', 'message': 'ok'}), 200),
    ];
    for (final response in responses) {
      final client = GateClient(httpClient: MockClient((_) async => response));
      addTearDown(client.close);
      await expectLater(
        client.scan(GatePairing.parse(payload()), '0123456789AB', 'In'),
        throwsA(isA<GateFailure>()),
      );
    }
    final client = GateClient(
      httpClient: MockClient((_) async => throw http.ClientException('offline')),
    );
    addTearDown(client.close);
    await expectLater(
      client.scan(GatePairing.parse(payload()), '0123456789AB', 'Out'),
      throwsA(isA<GateFailure>()),
    );
  });
}
