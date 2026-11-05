import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../domain/models/people.dart';
import '../../features/administration/presentation/accounts_page.dart';
import '../../features/administration/presentation/admin_dashboard_page.dart';
import '../../features/administration/presentation/announcement_form_page.dart';
import '../../features/administration/presentation/announcements_page.dart';
import '../../features/administration/presentation/directory_page.dart';
import '../../features/administration/presentation/enrollment_detail_page.dart';
import '../../features/administration/presentation/enrollments_page.dart';
import '../../features/administration/presentation/student_profile_page.dart';
import '../../features/auth/application/auth_controller.dart';
import '../../features/auth/presentation/login_page.dart';
import '../../features/auth/presentation/recovery_page.dart';
import '../../features/auth/presentation/welcome_page.dart';
import '../../features/finance/presentation/cashier_search_page.dart';
import '../../features/finance/presentation/evidence_form_page.dart';
import '../../features/finance/presentation/evidences_review_page.dart';
import '../../features/finance/presentation/finance_overview_page.dart';
import '../../features/finance/presentation/guardian_payments_page.dart';
import '../../features/finance/presentation/payments_page.dart';
import '../../features/finance/presentation/receipt_page.dart';
import '../../features/finance/presentation/register_payment_page.dart';
import '../../features/finance/presentation/student_account_page.dart';
import '../../features/guardian/presentation/guardian_agenda_page.dart';
import '../../features/guardian/presentation/guardian_grades_page.dart';
import '../../features/guardian/presentation/guardian_home_page.dart';
import '../../features/notices/presentation/notice_detail_page.dart';
import '../../features/notices/presentation/notices_page.dart';
import '../../features/profile/presentation/profile_page.dart';
import '../../features/student/presentation/course_detail_page.dart';
import '../../features/student/presentation/courses_page.dart';
import '../../features/student/presentation/student_agenda_page.dart';
import '../../features/student/presentation/student_grades_page.dart';
import '../../features/student/presentation/student_home_page.dart';
import '../../features/student/presentation/task_detail_page.dart';
import '../../features/teacher/presentation/group_detail_page.dart';
import '../../features/teacher/presentation/groups_page.dart';
import '../../features/teacher/presentation/review_detail_page.dart';
import '../../features/teacher/presentation/review_queue_page.dart';
import '../../features/teacher/presentation/teacher_agenda_page.dart';
import '../../features/teacher/presentation/teacher_today_page.dart';
import '../shell/role_shell.dart';
import 'not_found_page.dart';

/// Ruta de inicio de cada perfil.
String homePathFor(UserRole role) => switch (role) {
  UserRole.student => '/student',
  UserRole.guardian => '/family',
  UserRole.teacher => '/teacher',
  UserRole.admin => '/admin',
  UserRole.finance => '/finance',
};

const _publicPaths = {'/welcome', '/login', '/recover'};

/// Rutas de detalle restringidas por perfil. Es solo una guía de navegación:
/// la autorización real la aplica el servidor en cada recurso.
const _detailRoles = <String, Set<UserRole>>{
  '/tasks': {UserRole.student},
  '/courses': {UserRole.student, UserRole.guardian},
  '/groups': {UserRole.teacher},
  '/reviews': {UserRole.teacher},
  '/students': {UserRole.admin},
  '/enrollments': {UserRole.admin},
  '/announcements': {UserRole.admin},
  '/accounts': {UserRole.finance},
  '/receipts': {UserRole.finance, UserRole.guardian},
  '/evidence': {UserRole.guardian},
};

bool _isAllowed(String location, UserRole role) {
  if (location == '/profile' || location.startsWith('/notices/')) return true;
  final home = homePathFor(role);
  if (location == home || location.startsWith('$home/')) return true;
  for (final entry in _detailRoles.entries) {
    if (location == entry.key || location.startsWith('${entry.key}/')) {
      return entry.value.contains(role);
    }
  }
  return false;
}

final _rootKey = GlobalKey<NavigatorState>(debugLabel: 'root');

class _SessionListenable extends ChangeNotifier {
  void notify() => notifyListeners();
}

final routerProvider = Provider<GoRouter>((ref) {
  final listenable = _SessionListenable();
  ref.listen(authControllerProvider, (_, _) => listenable.notify());
  ref.listen(welcomeSeenProvider, (_, _) => listenable.notify());

  final router = GoRouter(
    navigatorKey: _rootKey,
    initialLocation: '/welcome',
    refreshListenable: listenable,
    errorBuilder: (context, state) => const NotFoundPage(),
    redirect: (context, state) {
      final session = ref.read(authControllerProvider);
      final location = state.matchedLocation;
      if (session == null) {
        if (_publicPaths.contains(location)) {
          if (location == '/welcome' && ref.read(welcomeSeenProvider)) return '/login';
          return null;
        }
        return ref.read(welcomeSeenProvider) ? '/login' : '/welcome';
      }
      if (_publicPaths.contains(location) || location == '/') {
        return homePathFor(session.role);
      }
      if (!_isAllowed(location, session.role)) {
        return homePathFor(session.role);
      }
      return null;
    },
    routes: [
      GoRoute(path: '/', redirect: (_, _) => '/welcome'),
      GoRoute(path: '/welcome', builder: (_, _) => const WelcomePage()),
      GoRoute(path: '/login', builder: (_, _) => const LoginPage()),
      GoRoute(path: '/recover', builder: (_, _) => const RecoveryPage()),
      GoRoute(path: '/profile', builder: (_, _) => const ProfilePage()),
      GoRoute(
        path: '/notices/:noticeId',
        builder: (_, state) => NoticeDetailPage(noticeId: state.pathParameters['noticeId']!),
      ),
      GoRoute(
        path: '/tasks/:taskId',
        builder: (_, state) => TaskDetailPage(taskId: state.pathParameters['taskId']!),
      ),
      GoRoute(
        path: '/courses/:studentId/:subjectId',
        builder: (_, state) => CourseDetailPage(
          studentId: state.pathParameters['studentId']!,
          subjectId: state.pathParameters['subjectId']!,
        ),
      ),
      GoRoute(
        path: '/groups/:groupId',
        builder: (_, state) => GroupDetailPage(groupId: state.pathParameters['groupId']!),
      ),
      GoRoute(
        path: '/reviews/:reviewId',
        builder: (_, state) => ReviewDetailPage(reviewId: state.pathParameters['reviewId']!),
      ),
      GoRoute(
        path: '/students/:studentId',
        builder: (_, state) => StudentProfilePage(studentId: state.pathParameters['studentId']!),
      ),
      GoRoute(
        path: '/enrollments/:caseId',
        builder: (_, state) => EnrollmentDetailPage(caseId: state.pathParameters['caseId']!),
      ),
      GoRoute(path: '/announcements/new', builder: (_, _) => const AnnouncementFormPage()),
      GoRoute(
        path: '/accounts/:studentId',
        builder: (_, state) => StudentAccountPage(studentId: state.pathParameters['studentId']!),
        routes: [
          GoRoute(
            path: 'pay',
            builder: (_, state) =>
                RegisterPaymentPage(studentId: state.pathParameters['studentId']!),
          ),
        ],
      ),
      GoRoute(
        path: '/receipts/:paymentId',
        builder: (_, state) => ReceiptPage(paymentId: state.pathParameters['paymentId']!),
      ),
      GoRoute(
        path: '/evidence/:studentId',
        builder: (_, state) => EvidenceFormPage(studentId: state.pathParameters['studentId']!),
      ),
      _shell(
        destinations: const [
          ShellDestination(
            label: 'Inicio',
            icon: Icons.home_outlined,
            selectedIcon: Icons.home_rounded,
          ),
          ShellDestination(
            label: 'Cursos',
            icon: Icons.auto_stories_outlined,
            selectedIcon: Icons.auto_stories_rounded,
          ),
          ShellDestination(
            label: 'Agenda',
            icon: Icons.event_note_outlined,
            selectedIcon: Icons.event_note_rounded,
          ),
          ShellDestination(
            label: 'Notas',
            icon: Icons.insights_outlined,
            selectedIcon: Icons.insights_rounded,
          ),
          ShellDestination(
            label: 'Avisos',
            icon: Icons.notifications_none_rounded,
            selectedIcon: Icons.notifications_rounded,
          ),
        ],
        branches: {
          '/student': (_) => const StudentHomePage(),
          '/student/courses': (_) => const CoursesPage(),
          '/student/agenda': (_) => const StudentAgendaPage(),
          '/student/grades': (_) => const StudentGradesPage(),
          '/student/notices': (_) => const NoticesPage(),
        },
      ),
      _shell(
        destinations: const [
          ShellDestination(
            label: 'Inicio',
            icon: Icons.home_outlined,
            selectedIcon: Icons.home_rounded,
          ),
          ShellDestination(
            label: 'Notas',
            icon: Icons.insights_outlined,
            selectedIcon: Icons.insights_rounded,
          ),
          ShellDestination(
            label: 'Pagos',
            icon: Icons.account_balance_wallet_outlined,
            selectedIcon: Icons.account_balance_wallet_rounded,
          ),
          ShellDestination(
            label: 'Agenda',
            icon: Icons.event_note_outlined,
            selectedIcon: Icons.event_note_rounded,
          ),
          ShellDestination(
            label: 'Avisos',
            icon: Icons.notifications_none_rounded,
            selectedIcon: Icons.notifications_rounded,
          ),
        ],
        branches: {
          '/family': (_) => const GuardianHomePage(),
          '/family/grades': (_) => const GuardianGradesPage(),
          '/family/payments': (_) => const GuardianPaymentsPage(),
          '/family/agenda': (_) => const GuardianAgendaPage(),
          '/family/notices': (_) => const NoticesPage(),
        },
      ),
      _shell(
        destinations: const [
          ShellDestination(
            label: 'Hoy',
            icon: Icons.today_outlined,
            selectedIcon: Icons.today_rounded,
          ),
          ShellDestination(
            label: 'Grupos',
            icon: Icons.groups_outlined,
            selectedIcon: Icons.groups_rounded,
          ),
          ShellDestination(
            label: 'Revisar',
            icon: Icons.fact_check_outlined,
            selectedIcon: Icons.fact_check_rounded,
          ),
          ShellDestination(
            label: 'Agenda',
            icon: Icons.event_note_outlined,
            selectedIcon: Icons.event_note_rounded,
          ),
          ShellDestination(
            label: 'Avisos',
            icon: Icons.notifications_none_rounded,
            selectedIcon: Icons.notifications_rounded,
          ),
        ],
        branches: {
          '/teacher': (_) => const TeacherTodayPage(),
          '/teacher/groups': (_) => const GroupsPage(),
          '/teacher/reviews': (_) => const ReviewQueuePage(),
          '/teacher/agenda': (_) => const TeacherAgendaPage(),
          '/teacher/notices': (_) => const NoticesPage(),
        },
      ),
      _shell(
        destinations: const [
          ShellDestination(
            label: 'Panel',
            icon: Icons.space_dashboard_outlined,
            selectedIcon: Icons.space_dashboard_rounded,
          ),
          ShellDestination(
            label: 'Estudiantes',
            icon: Icons.school_outlined,
            selectedIcon: Icons.school_rounded,
          ),
          ShellDestination(
            label: 'Matrícula',
            icon: Icons.assignment_ind_outlined,
            selectedIcon: Icons.assignment_ind_rounded,
          ),
          ShellDestination(
            label: 'Anuncios',
            icon: Icons.campaign_outlined,
            selectedIcon: Icons.campaign_rounded,
          ),
          ShellDestination(
            label: 'Cuentas',
            icon: Icons.manage_accounts_outlined,
            selectedIcon: Icons.manage_accounts_rounded,
          ),
        ],
        branches: {
          '/admin': (_) => const AdminDashboardPage(),
          '/admin/students': (_) => const DirectoryPage(),
          '/admin/enrollment': (_) => const EnrollmentsPage(),
          '/admin/announcements': (_) => const AnnouncementsPage(),
          '/admin/accounts': (_) => const AccountsPage(),
        },
      ),
      _shell(
        destinations: const [
          ShellDestination(
            label: 'Resumen',
            icon: Icons.insights_outlined,
            selectedIcon: Icons.insights_rounded,
          ),
          ShellDestination(
            label: 'Cobrar',
            icon: Icons.point_of_sale_outlined,
            selectedIcon: Icons.point_of_sale_rounded,
          ),
          ShellDestination(
            label: 'Validar',
            icon: Icons.receipt_long_outlined,
            selectedIcon: Icons.receipt_long_rounded,
          ),
          ShellDestination(
            label: 'Movimientos',
            icon: Icons.swap_vert_rounded,
            selectedIcon: Icons.swap_vert_circle_rounded,
          ),
          ShellDestination(
            label: 'Avisos',
            icon: Icons.notifications_none_rounded,
            selectedIcon: Icons.notifications_rounded,
          ),
        ],
        branches: {
          '/finance': (_) => const FinanceOverviewPage(),
          '/finance/collect': (_) => const CashierSearchPage(),
          '/finance/evidences': (_) => const EvidencesReviewPage(),
          '/finance/payments': (_) => const PaymentsPage(),
          '/finance/notices': (_) => const NoticesPage(),
        },
      ),
    ],
  );

  ref.onDispose(() {
    router.dispose();
    listenable.dispose();
  });
  return router;
});

StatefulShellRoute _shell({
  required List<ShellDestination> destinations,
  required Map<String, Widget Function(BuildContext context)> branches,
}) {
  return StatefulShellRoute.indexedStack(
    builder: (context, state, navigationShell) =>
        RoleShell(navigationShell: navigationShell, destinations: destinations),
    branches: [
      for (final entry in branches.entries)
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: entry.key,
              pageBuilder: (context, state) =>
                  NoTransitionPage(key: state.pageKey, child: entry.value(context)),
            ),
          ],
        ),
    ],
  );
}
