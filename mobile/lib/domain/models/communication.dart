enum NoticeChannel {
  school('Colegio'),
  teacher('Docentes'),
  system('Sistema');

  const NoticeChannel(this.label);

  final String label;
}

class Notice {
  const Notice({
    required this.id,
    required this.title,
    required this.body,
    required this.sentAt,
    required this.sender,
    required this.channel,
    required this.read,
    this.important = false,
  });

  final String id;
  final String title;
  final String body;
  final DateTime sentAt;
  final String sender;
  final NoticeChannel channel;
  final bool read;
  final bool important;

  Notice copyWith({bool? read}) {
    return Notice(
      id: id,
      title: title,
      body: body,
      sentAt: sentAt,
      sender: sender,
      channel: channel,
      read: read ?? this.read,
      important: important,
    );
  }
}

enum AnnouncementAudience {
  everyone('Toda la comunidad'),
  families('Familias'),
  students('Estudiantes'),
  teachers('Docentes');

  const AnnouncementAudience(this.label);

  final String label;
}

class Announcement {
  const Announcement({
    required this.id,
    required this.title,
    required this.body,
    required this.audience,
    required this.publishedAt,
    required this.author,
    required this.recipients,
    required this.readCount,
    required this.important,
  });

  final String id;
  final String title;
  final String body;
  final AnnouncementAudience audience;
  final DateTime publishedAt;
  final String author;
  final int recipients;
  final int readCount;
  final bool important;
}

class AnnouncementDraft {
  const AnnouncementDraft({
    required this.title,
    required this.body,
    required this.audience,
    required this.important,
  });

  final String title;
  final String body;
  final AnnouncementAudience audience;
  final bool important;
}
