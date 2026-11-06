import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/theme/app_colors.dart';
import '../../core/theme/app_theme.dart';
import '../../core/widgets/cards.dart';
import '../../core/widgets/states.dart';
import '../../shared/page_layout.dart';
import 'connected_session.dart';
import 'school_api.dart';
import 'school_tab.dart';
import 'students_tab.dart';
import 'teaching_tab.dart';

/// GET against the signed-in API; screens invalidate it to refresh. A 401 ends the local session.
final apiGetProvider = FutureProvider.autoDispose.family<dynamic, String>((ref, path) async {
  final session = ref.watch(connectedControllerProvider);
  if (session == null) throw const ApiFailure('Tu sesión venció. Vuelve a ingresar.', status: 401);
  try {
    return await session.api.get(path);
  } on ApiFailure catch (e) {
    if (e.unauthorized) ref.read(connectedControllerProvider.notifier).expire();
    rethrow;
  }
});

/// Renders a GET with loading, error + retry and pull-to-refresh, like the rest of the app.
class ApiBuilder extends ConsumerWidget {
  const ApiBuilder({required this.path, required this.builder, super.key});

  final String path;
  final Widget Function(BuildContext context, dynamic data) builder;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final value = ref.watch(apiGetProvider(path));
    if (value.hasValue) return builder(context, value.requireValue);
    if (value.hasError) {
      return ErrorState(
        message: value.error is ApiFailure
            ? value.error.toString()
            : 'No pudimos cargar esta información.',
        onRetry: () => ref.invalidate(apiGetProvider(path)),
      );
    }
    return const LoadingState(itemCount: 3);
  }
}

/// Same format as the web and the receipts: «Q 1,250.00».
String money(num value) => 'Q ${NumberFormat('#,##0.00', 'en_US').format(value)}';
String shortDate(String iso) {
  final date = DateTime.tryParse(iso);
  return date == null ? iso : DateFormat("d 'de' MMMM", 'es').format(date.toLocal());
}

class _Destination {
  const _Destination(this.label, this.icon, this.selected, this.page);
  final String label;
  final IconData icon;
  final IconData selected;
  final Widget page;
}

/// Home of the signed-in app. Tabs come from the session (linked students, teaching assignments, permissions);
/// they are navigation only: the server authorizes every request.
class ConnectedHome extends ConsumerStatefulWidget {
  const ConnectedHome({super.key});

  @override
  ConsumerState<ConnectedHome> createState() => _ConnectedHomeState();
}

class _ConnectedHomeState extends ConsumerState<ConnectedHome> {
  int _index = 0;

  List<_Destination> _destinations(ConnectedSession s) => [
    const _Destination(
      'Inicio',
      Icons.space_dashboard_outlined,
      Icons.space_dashboard,
      ConnectedHomeTab(),
    ),
    if (s.students.isNotEmpty)
      _Destination(
        s.isStudent ? 'Mi ficha' : 'Estudiantes',
        s.isStudent ? Icons.school_outlined : Icons.family_restroom_outlined,
        s.isStudent ? Icons.school : Icons.family_restroom,
        const StudentsTab(),
      ),
    if (s.courses.isNotEmpty)
      const _Destination('Docencia', Icons.menu_book_outlined, Icons.menu_book, TeachingTab()),
    if (s.hasSchoolModules)
      const _Destination('Colegio', Icons.apartment_outlined, Icons.apartment, SchoolTab()),
    const _Destination('Avisos', Icons.campaign_outlined, Icons.campaign, NoticesTab()),
  ];

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(connectedControllerProvider);
    if (session == null) {
      // Signed out or expired: back to the sign-in page.
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) context.go('/connect');
      });
      return const Scaffold(body: SizedBox.shrink());
    }
    final destinations = _destinations(session);
    final index = _index.clamp(0, destinations.length - 1);
    return Scaffold(
      body: IndexedStack(index: index, children: [for (final d in destinations) d.page]),
      bottomNavigationBar: DecoratedBox(
        decoration: BoxDecoration(
          border: Border(
            top: BorderSide(color: AppColors.of(context).accent.withValues(alpha: 0.18)),
          ),
        ),
        child: NavigationBar(
          selectedIndex: index,
          onDestinationSelected: (i) => setState(() => _index = i),
          labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
          destinations: [
            for (final d in destinations)
              NavigationDestination(
                icon: Icon(d.icon),
                selectedIcon: Icon(d.selected),
                label: d.label,
                tooltip: '',
              ),
          ],
        ),
      ),
    );
  }
}

/// Section switcher used inside details: wraps on narrow phones instead of cutting labels.
class SectionChips<T> extends StatelessWidget {
  const SectionChips({
    required this.options,
    required this.selected,
    required this.onSelected,
    super.key,
  });

  final Map<T, (String, IconData)> options;
  final T selected;
  final ValueChanged<T> onSelected;

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    return Wrap(
      spacing: Spacing.sm,
      runSpacing: Spacing.sm,
      children: [
        for (final e in options.entries)
          ChoiceChip(
            avatar: Icon(
              e.value.$2,
              size: 18,
              color: e.key == selected ? colors.deep : colors.muted,
            ),
            label: Text(e.value.$1),
            selected: e.key == selected,
            selectedColor: colors.accent.withValues(alpha: 0.28),
            side: BorderSide(color: e.key == selected ? colors.accent : colors.border),
            onSelected: (_) => onSelected(e.key),
          ),
      ],
    );
  }
}

/// Scrollable tab body with the brand header bound to the connected account.
class ConnectedTabPage extends ConsumerWidget {
  const ConnectedTabPage({required this.children, this.onRefresh, super.key});

  final List<Widget> children;
  final Future<void> Function()? onRefresh;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(connectedControllerProvider);
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: PageBody(
          onRefresh: onRefresh,
          children: [
            InstitutionHeader(
              subtitle: session?.profile ?? '',
              initials: session?.initials,
              profileLabel: session?.name,
              onProfile: () => showConnectedProfile(context, ref),
            ),
            ...children,
          ],
        ),
      ),
    );
  }
}

Future<void> showConnectedProfile(BuildContext context, WidgetRef ref) async {
  final session = ref.read(connectedControllerProvider);
  if (session == null) return;
  final theme = Theme.of(context);
  final colors = AppColors.of(context);
  await showModalBottomSheet<void>(
    context: context,
    builder: (sheet) => SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(Spacing.page, 0, Spacing.page, Spacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                InitialsAvatar(initials: session.initials, size: 52),
                const SizedBox(width: Spacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(session.name, style: theme.textTheme.titleMedium),
                      Text(
                        '${session.username} · ${session.profile}',
                        style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            if (session.mfaPending.isNotEmpty) ...[
              const SizedBox(height: Spacing.md),
              const StatusPill(
                label: 'Activa la verificación en dos pasos desde la web para usar todos tus permisos.',
                tone: AppTone.warning,
              ),
            ],
            const SizedBox(height: Spacing.lg),
            OutlinedButton.icon(
              onPressed: () async {
                Navigator.of(sheet).pop();
                await ref.read(connectedControllerProvider.notifier).signOut();
              },
              icon: const Icon(Icons.logout),
              label: const Text('Cerrar sesión'),
            ),
          ],
        ),
      ),
    ),
  );
}

// ---------------------------------------------------------------- Inicio

class ConnectedHomeTab extends ConsumerWidget {
  const ConnectedHomeTab({super.key});

  static const path = '/api/v1/dashboard';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(connectedControllerProvider)!;
    final theme = Theme.of(context);
    final hour = DateTime.now().hour;
    final greeting = hour < 12 ? 'Buenos días' : (hour < 19 ? 'Buenas tardes' : 'Buenas noches');
    return ConnectedTabPage(
      onRefresh: () async => ref.invalidate(apiGetProvider(path)),
      children: [
        PageIntro(
          eyebrow: DateFormat("EEEE d 'de' MMMM", 'es').format(DateTime.now()),
          title: '$greeting, ${session.firstName}',
          subtitle: 'Lo importante de hoy en tu espacio escolar.',
        ),
        ApiBuilder(
          path: path,
          builder: (context, data) {
            final d = data as Map<String, dynamic>;
            final academic = d['academic'] as Map<String, dynamic>?;
            final finance = d['finance'] as Map<String, dynamic>?;
            final gate = d['gate'] as Map<String, dynamic>?;
            final teaching = (d['teaching'] as List<dynamic>? ?? const [])
                .cast<Map<String, dynamic>>();
            final students = (d['students'] as List<dynamic>? ?? const [])
                .cast<Map<String, dynamic>>();
            final tiles = <Widget>[
              if (academic != null)
                StatTile(
                  label: 'Inscritos',
                  value: '${academic['enrolled']}',
                  caption: academic['year'] as String?,
                  icon: Icons.school_outlined,
                  tone: AppTone.brand,
                ),
              if (gate != null)
                StatTile(
                  label: 'En el colegio',
                  value: '${gate['inside']}',
                  caption: '${gate['arrived']} de ${gate['expected']} llegaron hoy',
                  icon: Icons.qr_code_scanner,
                  tone: AppTone.blue,
                ),
              if (finance != null)
                StatTile(
                  label: 'Cobrado hoy',
                  value: money(finance['collectedToday'] as num),
                  caption: '${finance['paymentsToday']} recibos',
                  icon: Icons.payments_outlined,
                  tone: AppTone.success,
                ),
              if (finance != null)
                StatTile(
                  label: 'Saldo vencido',
                  value: money(finance['overdue'] as num),
                  caption: '${finance['studentsWithBalance']} estudiantes con saldo',
                  icon: Icons.warning_amber_outlined,
                  tone: AppTone.accent,
                ),
            ];
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (tiles.isNotEmpty)
                  GridView.count(
                    crossAxisCount: 2,
                    shrinkWrap: true,
                    physics: const NeverScrollableScrollPhysics(),
                    mainAxisSpacing: Spacing.md,
                    crossAxisSpacing: Spacing.md,
                    childAspectRatio: 1.25,
                    children: tiles,
                  ),
                for (final course in teaching) ...[
                  const SizedBox(height: Spacing.md),
                  AppCard(
                    child: Row(
                      children: [
                        const ToneIcon(icon: Icons.menu_book_outlined, tone: AppTone.accent),
                        const SizedBox(width: Spacing.md),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(course['subject'] as String, style: theme.textTheme.titleSmall),
                              Text(
                                '${course['grade']} · ${course['students']} estudiantes · '
                                '${course['pendingReviews']} por revisar',
                                style: theme.textTheme.bodySmall?.copyWith(
                                  color: AppColors.of(context).muted,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                for (final s in students) ...[
                  const SizedBox(height: Spacing.md),
                  _StudentSummary(data: s),
                ],
                if (tiles.isEmpty && teaching.isEmpty && students.isEmpty)
                  const EmptyState(
                    icon: Icons.inbox_outlined,
                    title: 'Todo en orden',
                    message: 'No hay pendientes para tu cuenta hoy.',
                  ),
              ],
            );
          },
        ),
      ],
    );
  }
}

class _StudentSummary extends StatelessWidget {
  const _StudentSummary({required this.data});

  final Map<String, dynamic> data;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    final balance = data['balance'] as num?;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              InitialsAvatar(initials: _initials(data['name'] as String)),
              const SizedBox(width: Spacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(data['name'] as String, style: theme.textTheme.titleSmall),
                    Text(
                      '${data['code'] ?? ''} · ${data['courses'] ?? 0} cursos',
                      style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: Spacing.md),
          Wrap(
            spacing: Spacing.sm,
            runSpacing: Spacing.sm,
            children: [
              if (data['canAcademic'] == true)
                StatusPill(
                  label: '${data['pendingTasks']} tareas pendientes',
                  tone: (data['pendingTasks'] as num? ?? 0) > 0 ? AppTone.warning : AppTone.success,
                  icon: Icons.assignment_outlined,
                ),
              if (balance != null)
                StatusPill(
                  label: balance > 0 ? 'Saldo ${money(balance)}' : 'Al día',
                  tone: balance > 0 ? AppTone.danger : AppTone.success,
                  icon: Icons.account_balance_wallet_outlined,
                ),
            ],
          ),
        ],
      ),
    );
  }
}

String _initials(String name) {
  final parts = name.split(' ').where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) return '?';
  return (parts.first[0] + (parts.length > 1 ? parts[1][0] : '')).toUpperCase();
}

// ---------------------------------------------------------------- Avisos

class NoticesTab extends ConsumerWidget {
  const NoticesTab({super.key});

  static const path = '/api/v1/me/announcements';

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return ConnectedTabPage(
      onRefresh: () async => ref.invalidate(apiGetProvider(path)),
      children: [
        const PageIntro(
          eyebrow: 'Comunicación',
          title: 'Avisos',
          subtitle: 'Mensajes del colegio dentro de la plataforma.',
        ),
        ApiBuilder(
          path: path,
          builder: (context, data) {
            final items = (data as List<dynamic>).cast<Map<String, dynamic>>();
            if (items.isEmpty) {
              return const EmptyState(icon: Icons.campaign_outlined, title: 'No tienes avisos');
            }
            return Column(
              children: [
                for (final item in items)
                  Padding(
                    padding: const EdgeInsets.only(bottom: Spacing.md),
                    child: AppCard(
                      onTap: () => _open(context, ref, item),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Padding(
                            padding: const EdgeInsets.only(top: 6, right: Spacing.md),
                            child: CircleAvatar(
                              radius: 4,
                              backgroundColor: item['read'] == true
                                  ? Colors.transparent
                                  : colors.accent,
                            ),
                          ),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(item['title'] as String, style: theme.textTheme.titleSmall),
                                const SizedBox(height: 2),
                                Text(
                                  '${item['author']} · ${shortDate(item['publishedAt'] as String)} · ${item['audience']}',
                                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                                ),
                                const SizedBox(height: 6),
                                Text(
                                  item['body'] as String,
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                  style: theme.textTheme.bodyMedium,
                                ),
                              ],
                            ),
                          ),
                          Icon(Icons.chevron_right, color: colors.muted),
                        ],
                      ),
                    ),
                  ),
              ],
            );
          },
        ),
      ],
    );
  }

  Future<void> _open(BuildContext context, WidgetRef ref, Map<String, dynamic> item) async {
    final session = ref.read(connectedControllerProvider);
    if (item['read'] != true && session != null) {
      try {
        await session.api.post('/api/v1/me/announcements/${item['id']}/read');
        ref.invalidate(apiGetProvider(path));
      } on ApiFailure {
        // Reading still works; the unread mark refreshes next time.
      }
    }
    if (!context.mounted) return;
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => DetailPage(
          title: 'Aviso',
          children: [
            PageIntro(
              eyebrow: '${item['author']} · ${shortDate(item['publishedAt'] as String)}',
              title: item['title'] as String,
              subtitle: item['audience'] as String?,
            ),
            AppCard(child: SelectableText(item['body'] as String)),
          ],
        ),
      ),
    );
  }
}
