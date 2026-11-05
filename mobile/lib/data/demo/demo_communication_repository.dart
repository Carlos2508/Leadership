import '../../domain/models/communication.dart';
import '../../domain/models/people.dart';
import '../../domain/repositories/communication_repository.dart';
import 'demo_backend.dart';
import 'demo_database.dart';

class DemoCommunicationRepository implements CommunicationRepository {
  DemoCommunicationRepository(this._db, this._backend);

  final DemoDatabase _db;
  final DemoBackend _backend;

  @override
  Future<List<Notice>> fetchNotices(UserRole role) {
    return _backend.call(() {
      final list = List.of(_db.notices[role] ?? const <Notice>[])
        ..sort((a, b) => b.sentAt.compareTo(a.sentAt));
      return List.unmodifiable(list);
    });
  }

  @override
  Future<void> markRead(String noticeId) {
    return _backend.call(() {
      for (final role in _db.notices.keys) {
        _db.notices[role] = [
          for (final n in _db.notices[role]!) n.id == noticeId ? n.copyWith(read: true) : n,
        ];
      }
    });
  }

  @override
  Future<void> markAllRead(UserRole role) {
    return _backend.call(() {
      _db.notices[role] = [for (final n in _db.notices[role] ?? <Notice>[]) n.copyWith(read: true)];
    });
  }

  @override
  Future<List<Announcement>> fetchAnnouncements() {
    return _backend.call(() {
      final list = List.of(_db.announcements)
        ..sort((a, b) => b.publishedAt.compareTo(a.publishedAt));
      return List.unmodifiable(list);
    });
  }

  @override
  Future<Announcement> publishAnnouncement(AnnouncementDraft draft, {required String author}) {
    return _backend.call(() {
      final title = draft.title.trim();
      final body = draft.body.trim();
      if (title.length < 4 || body.length < 10) {
        throw ArgumentError('El comunicado necesita título y mensaje.');
      }
      final recipients = switch (draft.audience) {
        AnnouncementAudience.everyone => 96,
        AnnouncementAudience.families => 58,
        AnnouncementAudience.students => 42,
        AnnouncementAudience.teachers => 9,
      };
      final announcement = Announcement(
        id: _db.nextId('ann'),
        title: title,
        body: body,
        audience: draft.audience,
        publishedAt: _db.now,
        author: author,
        recipients: recipients,
        readCount: 0,
        important: draft.important,
      );
      _db.announcements.insert(0, announcement);
      _db.broadcastNotice(
        draft.audience,
        Notice(
          id: _db.nextId('ntc'),
          title: title,
          body: body,
          sentAt: announcement.publishedAt,
          sender: author,
          channel: NoticeChannel.school,
          read: false,
          important: draft.important,
        ),
      );
      return announcement;
    });
  }
}
