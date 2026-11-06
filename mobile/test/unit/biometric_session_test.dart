import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_platform_app/core/providers.dart';
import 'package:school_platform_app/features/connected/biometric_vault.dart';
import 'package:school_platform_app/features/connected/connected_session.dart';
import 'package:school_platform_app/features/connected/school_api.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'connected_api_test.dart' show FakeSchool;

class FakeVault implements BiometricVault {
  String? value;
  bool supported = true, cancelled = false;
  int reads = 0;
  @override
  Future<bool> available() async => supported;
  @override
  Future<void> seal(String data) async => value = data;
  @override
  Future<String?> unlock() async {
    reads++;
    if (cancelled) throw StateError('cancelled');
    return value;
  }

  @override
  Future<void> clear() async => value = null;
}

void main() {
  late SharedPreferences preferences;
  late FakeSchool school;
  late FakeVault vault;
  ProviderContainer container() {
    final result = ProviderContainer(
      overrides: [
        sharedPreferencesProvider.overrideWithValue(preferences),
        connectedHttpClientProvider.overrideWithValue(school.client),
        biometricVaultProvider.overrideWithValue(vault),
      ],
    );
    addTearDown(result.dispose);
    return result;
  }

  Future<ProviderContainer> prepare() async {
    final result = container();
    await result
        .read(connectedControllerProvider.notifier)
        .signIn(server: 'https://colegio.ejemplo', username: 'fam', password: 'Correct-1');
    return result;
  }

  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    preferences = await SharedPreferences.getInstance();
    school = FakeSchool();
    vault = FakeVault();
  });

  test('password sign-in is in memory until explicit biometric opt-in; only the Identity cookie is sealed', () async {
    final c = await prepare();
    expect(vault.value, isNull);
    await c.read(connectedControllerProvider.notifier).enableBiometrics();
    final data = jsonDecode(vault.value!) as Map;
    expect((data['cookies'] as Map).keys, ['SchoolPlatform.Session']);
    expect(vault.value, isNot(contains('Correct-1')));
    expect(vault.value, isNot(contains('Antiforgery')));
    expect(preferences.getKeys(), isNot(contains('cookies')));
    expect(c.read(biometricEnabledProvider), isTrue);
  });
  test('cold restart restores only after protected read and a fresh server validation, without password login', () async {
    final first = await prepare();
    await first.read(connectedControllerProvider.notifier).enableBiometrics();
    final logins = school.requests.where((r) => r.url.path == '/api/v1/auth/login').length;
    final next = container();
    expect(next.read(connectedControllerProvider), isNull);
    await next.read(connectedControllerProvider.notifier).unlockBiometrics();
    expect(vault.reads, 1);
    expect(next.read(connectedControllerProvider)!.username, 'fam');
    expect(school.requests.where((r) => r.url.path == '/api/v1/auth/login').length, logins);
    expect(school.requests.last.url.path, '/api/v1/me/courses');
  });
  test('cancelled biometrics never uses the saved ticket or discards it', () async {
    final first = await prepare();
    await first.read(connectedControllerProvider.notifier).enableBiometrics();
    vault.cancelled = true;
    final next = container();
    final requests = school.requests.length;
    await expectLater(
      next.read(connectedControllerProvider.notifier).unlockBiometrics(),
      throwsStateError,
    );
    expect(next.read(connectedControllerProvider), isNull);
    expect(school.requests.length, requests);
    expect(vault.value, isNotNull);
  });
  test('revoked or expired session erases the remembered ticket and never falls back to a stored password', () async {
    final first = await prepare();
    await first.read(connectedControllerProvider.notifier).enableBiometrics();
    school.revoked = true;
    final next = container();
    await expectLater(
      next.read(connectedControllerProvider.notifier).unlockBiometrics(),
      throwsA(isA<ApiFailure>()),
    );
    expect(vault.value, isNull);
    expect(next.read(biometricEnabledProvider), isFalse);
    expect(next.read(connectedControllerProvider), isNull);
  });
  test('temporary offline error preserves the sealed ticket for a later retry', () async {
    final first = await prepare();
    await first.read(connectedControllerProvider.notifier).enableBiometrics();
    school.offline = true;
    final next = container();
    await expectLater(
      next.read(connectedControllerProvider.notifier).unlockBiometrics(),
      throwsA(isA<ApiFailure>()),
    );
    expect(vault.value, isNotNull);
    expect(next.read(connectedControllerProvider), isNull);
    school.offline = false;
    await next.read(connectedControllerProvider.notifier).unlockBiometrics();
    expect(next.read(connectedControllerProvider), isNotNull);
  });
  test('logout removes the protected ticket and opt-in', () async {
    final c = await prepare();
    await c.read(connectedControllerProvider.notifier).enableBiometrics();
    await c.read(connectedControllerProvider.notifier).signOut();
    expect(vault.value, isNull);
    expect(c.read(biometricEnabledProvider), isFalse);
  });
  test(
    'disabling biometrics returns the server to an ordinary session and removes the vault ticket',
    () async {
      final c = await prepare();
      final controller = c.read(connectedControllerProvider.notifier);
      await controller.enableBiometrics();
      await controller.disableBiometrics();
      expect(school.requests.any((r) => r.url.path == '/api/v1/auth/forget-device'), isTrue);
      expect(vault.value, isNull);
      expect(c.read(biometricEnabledProvider), isFalse);
      expect(c.read(connectedControllerProvider), isNotNull);
    },
  );
  test('MFA must finish before a device can be remembered', () async {
    school.requireSecondFactor = true;
    final c = container();
    final controller = c.read(connectedControllerProvider.notifier);
    expect(
      await controller.signIn(
        server: 'https://colegio.ejemplo',
        username: 'fam',
        password: 'Correct-1',
      ),
      LoginStep.secondFactor,
    );
    await expectLater(controller.enableBiometrics(), throwsA(isA<ApiFailure>()));
    expect(vault.value, isNull);
    await controller.completeSecondFactor('123456');
    await controller.enableBiometrics();
    expect(vault.value, isNotNull);
  });
  test('missing or malformed vault data requires a fresh sign-in', () async {
    final first = await prepare();
    await first.read(connectedControllerProvider.notifier).enableBiometrics();
    vault.value =
        '{"server":"http://malicious.ejemplo","cookies":{"SchoolPlatform.Session":"abc"}}';
    final next = container();
    await expectLater(
      next.read(connectedControllerProvider.notifier).unlockBiometrics(),
      throwsA(isA<ApiFailure>()),
    );
    expect(vault.value, isNull);
  });
  test('devices without enrolled biometrics keep normal sign-in and do not opt in', () async {
    final c = await prepare();
    vault.supported = false;
    await expectLater(
      c.read(connectedControllerProvider.notifier).enableBiometrics(),
      throwsA(isA<ApiFailure>()),
    );
    expect(vault.value, isNull);
    expect(c.read(connectedControllerProvider), isNotNull);
  });
}
