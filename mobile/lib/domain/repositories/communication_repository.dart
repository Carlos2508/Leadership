import '../models/communication.dart';
import '../models/people.dart';

abstract interface class CommunicationRepository {
  Future<List<Notice>> fetchNotices(UserRole role);

  Future<void> markRead(String noticeId);

  Future<void> markAllRead(UserRole role);

  Future<List<Announcement>> fetchAnnouncements();

  Future<Announcement> publishAnnouncement(AnnouncementDraft draft, {required String author});
}
