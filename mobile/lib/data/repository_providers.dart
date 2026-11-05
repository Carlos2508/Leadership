import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/providers.dart';
import '../domain/repositories/academic_repository.dart';
import '../domain/repositories/administration_repository.dart';
import '../domain/repositories/auth_repository.dart';
import '../domain/repositories/communication_repository.dart';
import '../domain/repositories/finance_repository.dart';
import '../domain/repositories/teaching_repository.dart';
import 'demo/demo_academic_repository.dart';
import 'demo/demo_administration_repository.dart';
import 'demo/demo_auth_repository.dart';
import 'demo/demo_backend.dart';
import 'demo/demo_communication_repository.dart';
import 'demo/demo_database.dart';
import 'demo/demo_finance_repository.dart';
import 'demo/demo_teaching_repository.dart';

/// Latencia simulada de red. Las pruebas la sobrescriben con `Duration.zero`.
final demoLatencyProvider = Provider<Duration>((ref) => const Duration(milliseconds: 450));

/// Permite demostrar los estados de error desde Perfil → Opciones del prototipo.
class SimulateFailuresController extends Notifier<bool> {
  @override
  bool build() => false;

  void toggle(bool value) => state = value;
}

final simulateFailuresProvider = NotifierProvider<SimulateFailuresController, bool>(
  SimulateFailuresController.new,
);

/// Base de datos ficticia compartida por todos los repositorios demo.
final demoDatabaseProvider = Provider<DemoDatabase>(
  (ref) => DemoDatabase(clock: ref.watch(clockProvider)),
);

final demoBackendProvider = Provider<DemoBackend>((ref) {
  return DemoBackend(
    latency: ref.watch(demoLatencyProvider),
    simulateFailures: () => ref.read(simulateFailuresProvider),
  );
});

// Para conectar la API real basta con sustituir estas implementaciones; las
// pantallas solo dependen de las interfaces del dominio.

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => DemoAuthRepository(ref.watch(demoDatabaseProvider), ref.watch(demoBackendProvider)),
);

final academicRepositoryProvider = Provider<AcademicRepository>(
  (ref) => DemoAcademicRepository(ref.watch(demoDatabaseProvider), ref.watch(demoBackendProvider)),
);

final communicationRepositoryProvider = Provider<CommunicationRepository>(
  (ref) =>
      DemoCommunicationRepository(ref.watch(demoDatabaseProvider), ref.watch(demoBackendProvider)),
);

final financeRepositoryProvider = Provider<FinanceRepository>(
  (ref) => DemoFinanceRepository(ref.watch(demoDatabaseProvider), ref.watch(demoBackendProvider)),
);

final teachingRepositoryProvider = Provider<TeachingRepository>(
  (ref) => DemoTeachingRepository(ref.watch(demoDatabaseProvider), ref.watch(demoBackendProvider)),
);

final administrationRepositoryProvider = Provider<AdministrationRepository>(
  (ref) =>
      DemoAdministrationRepository(ref.watch(demoDatabaseProvider), ref.watch(demoBackendProvider)),
);
