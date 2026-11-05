import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/institution_logo.dart';

class ShellDestination {
  const ShellDestination({required this.label, required this.icon, required this.selectedIcon});

  final String label;
  final IconData icon;
  final IconData selectedIcon;
}

/// Contenedor de pestañas por rol. Usa barra inferior en teléfonos y riel
/// lateral en tabletas/escritorio.
class RoleShell extends ConsumerWidget {
  const RoleShell({required this.navigationShell, required this.destinations, super.key});

  final StatefulNavigationShell navigationShell;
  final List<ShellDestination> destinations;

  static const _railBreakpoint = 720.0;
  static const _extendedBreakpoint = 1180.0;

  void _onSelect(int index) {
    navigationShell.goBranch(index, initialLocation: index == navigationShell.currentIndex);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final width = MediaQuery.sizeOf(context).width;
    if (width < _railBreakpoint) {
      return Scaffold(
        body: navigationShell,
        bottomNavigationBar: DecoratedBox(
          decoration: BoxDecoration(
            border: Border(top: BorderSide(color: AppColors.of(context).border)),
          ),
          child: NavigationBar(
            selectedIndex: navigationShell.currentIndex,
            onDestinationSelected: _onSelect,
            labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
            destinations: [
              for (final d in destinations)
                NavigationDestination(
                  icon: Icon(d.icon),
                  selectedIcon: Icon(d.selectedIcon),
                  label: d.label,
                  tooltip: '',
                ),
            ],
          ),
        ),
      );
    }

    final extended = width >= _extendedBreakpoint;
    final institution = ref.watch(institutionProvider);
    return Scaffold(
      body: Row(
        children: [
          SafeArea(
            right: false,
            child: NavigationRail(
              extended: extended,
              minExtendedWidth: 220,
              selectedIndex: navigationShell.currentIndex,
              onDestinationSelected: _onSelect,
              labelType: extended ? NavigationRailLabelType.none : NavigationRailLabelType.all,
              leading: Padding(
                padding: const EdgeInsets.only(top: 12, bottom: 20),
                child: InstitutionLogo(path: institution.mainLogo, size: extended ? 56 : 44),
              ),
              destinations: [
                for (final d in destinations)
                  NavigationRailDestination(
                    icon: Icon(d.icon),
                    selectedIcon: Icon(d.selectedIcon),
                    label: Text(d.label),
                  ),
              ],
            ),
          ),
          VerticalDivider(width: 1, color: AppColors.of(context).border),
          Expanded(child: navigationShell),
        ],
      ),
    );
  }
}
