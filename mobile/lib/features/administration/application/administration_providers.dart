import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repository_providers.dart';
import '../../../domain/models/administration.dart';
import '../../../domain/models/communication.dart';
import '../../../domain/models/people.dart';
import '../../auth/application/auth_controller.dart';
import '../../notices/application/notices_providers.dart';

final kpisProvider = FutureProvider.autoDispose<SchoolKpis>(
  (ref) => ref.watch(administrationRepositoryProvider).fetchKpis(),
);

final directoryProvider = FutureProvider.autoDispose<List<StudentSummary>>(
  (ref) => ref.watch(administrationRepositoryProvider).fetchDirectory(),
);

final studentProfileProvider = FutureProvider.autoDispose.family<StudentProfile, String>(
  (ref, id) => ref.watch(administrationRepositoryProvider).fetchStudentProfile(id),
);

final enrollmentsProvider = FutureProvider.autoDispose<List<EnrollmentCase>>(
  (ref) => ref.watch(administrationRepositoryProvider).fetchEnrollments(),
);

final enrollmentProvider = FutureProvider.autoDispose.family<EnrollmentCase, String>(
  (ref, id) => ref.watch(administrationRepositoryProvider).fetchEnrollment(id),
);

final accountsProvider = FutureProvider.autoDispose<List<AccountSummary>>(
  (ref) => ref.watch(administrationRepositoryProvider).fetchAccounts(),
);

final announcementsProvider = FutureProvider.autoDispose<List<Announcement>>(
  (ref) => ref.watch(communicationRepositoryProvider).fetchAnnouncements(),
);

class AdministrationActions {
  AdministrationActions(this._ref);

  final Ref _ref;

  void _refreshEnrollment(String caseId) {
    _ref
      ..invalidate(enrollmentsProvider)
      ..invalidate(enrollmentProvider(caseId))
      ..invalidate(kpisProvider);
  }

  Future<EnrollmentCase> advance(String caseId) async {
    final updated = await _ref.read(administrationRepositoryProvider).advanceEnrollment(caseId);
    _refreshEnrollment(caseId);
    return updated;
  }

  Future<EnrollmentCase> resolveIssue(String caseId, String issue) async {
    final updated = await _ref.read(administrationRepositoryProvider).resolveIssue(caseId, issue);
    _refreshEnrollment(caseId);
    return updated;
  }

  Future<ActivationCode> issueCode(String accountId) =>
      _ref.read(administrationRepositoryProvider).issueActivationCode(accountId);

  Future<AccountSummary> setActive(String accountId, {required bool active}) async {
    final updated = await _ref
        .read(administrationRepositoryProvider)
        .setAccountActive(accountId, active: active);
    _ref.invalidate(accountsProvider);
    return updated;
  }

  Future<Announcement> publish(AnnouncementDraft draft) async {
    final author = _ref.read(requireSessionProvider).roleTitle;
    final created = await _ref
        .read(communicationRepositoryProvider)
        .publishAnnouncement(draft, author: author);
    _ref
      ..invalidate(announcementsProvider)
      ..invalidate(noticesProvider);
    return created;
  }
}

final administrationActionsProvider = Provider<AdministrationActions>(AdministrationActions.new);
