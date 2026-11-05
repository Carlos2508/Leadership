import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/repository_providers.dart';
import '../../../domain/models/communication.dart';
import '../../../domain/models/people.dart';
import '../../auth/application/auth_controller.dart';

final noticesProvider = FutureProvider.autoDispose<List<Notice>>((ref) {
  final role = ref.watch(requireSessionProvider).role;
  return ref.watch(communicationRepositoryProvider).fetchNotices(role);
});

final unreadNoticesProvider = Provider.autoDispose<int>((ref) {
  return ref.watch(noticesProvider).value?.where((n) => !n.read).length ?? 0;
});

class NoticeActions {
  NoticeActions(this._ref);

  final Ref _ref;

  Future<void> markRead(String noticeId) async {
    await _ref.read(communicationRepositoryProvider).markRead(noticeId);
    _ref.invalidate(noticesProvider);
  }

  Future<void> markAllRead(UserRole role) async {
    await _ref.read(communicationRepositoryProvider).markAllRead(role);
    _ref.invalidate(noticesProvider);
  }
}

final noticeActionsProvider = Provider<NoticeActions>(NoticeActions.new);
