import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:local_auth/local_auth.dart';

import '../../core/providers.dart';

/// Only a protected Identity ticket is sealed; never a password, TOTP, antiforgery token or biometric template.
abstract class BiometricVault {
  Future<bool> available();
  Future<void> seal(String value);
  Future<String?> unlock();
  Future<void> clear();
}

class NativeBiometricVault implements BiometricVault {
  static const _storage = FlutterSecureStorage(
    aOptions: AndroidOptions.biometric(
      enforceBiometrics: true,
      requireBiometricsPerOperation: true,
      biometricType: AndroidBiometricType.strongBiometricOnly,
      storageNamespace: 'school_biometric_session',
      biometricPromptTitle: 'Desbloquear tu espacio',
      biometricPromptSubtitle: 'Confirma tu huella o biometría del teléfono',
      biometricPromptNegativeButton: 'Cancelar',
    ),
    iOptions: IOSOptions(
      accountName: 'school.biometric.session',
      accessibility: KeychainAccessibility.passcode,
      accessControlFlags: [AccessControlFlag.biometryCurrentSet],
    ),
  );
  static const _key = 'identity-ticket';

  @override
  Future<bool> available() async {
    if (kIsWeb ||
        !(defaultTargetPlatform == TargetPlatform.android ||
            defaultTargetPlatform == TargetPlatform.iOS)) {
      return false;
    }
    try {
      return (await LocalAuthentication().getAvailableBiometrics()).isNotEmpty;
    } on Object {
      return false;
    }
  }

  @override
  Future<void> seal(String value) => _storage.write(key: _key, value: value);
  @override
  Future<String?> unlock() => _storage.read(key: _key);
  @override
  Future<void> clear() => _storage.delete(key: _key);
}

final biometricVaultProvider = Provider<BiometricVault>((_) => NativeBiometricVault());
final biometricAvailableProvider = FutureProvider<bool>(
  (ref) => ref.read(biometricVaultProvider).available(),
);

class BiometricEnabled extends Notifier<bool> {
  static const key = 'connected.biometricEnabled';
  @override
  bool build() => ref.read(sharedPreferencesProvider).getBool(key) ?? false;
  Future<void> setEnabled(bool value) async {
    await ref.read(sharedPreferencesProvider).setBool(key, value);
    state = value;
  }
}

final biometricEnabledProvider = NotifierProvider<BiometricEnabled, bool>(BiometricEnabled.new);

class BiometricLocked extends Notifier<bool> {
  @override
  bool build() => false;
  void setLocked(bool value) => state = value;
}

final biometricLockedProvider = NotifierProvider<BiometricLocked, bool>(BiometricLocked.new);
