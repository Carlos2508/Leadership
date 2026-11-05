import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/providers.dart';
import '../core/theme/app_colors.dart';
import '../core/theme/app_theme.dart';
import '../core/widgets/cards.dart';
import '../core/widgets/institution_logo.dart';
import '../features/auth/application/auth_controller.dart';
import 'back_navigation.dart';

/// Cuerpo desplazable con ancho máximo legible y pull-to-refresh opcional.
class PageBody extends StatelessWidget {
  const PageBody({
    this.children = const [],
    this.slivers = const [],
    this.onRefresh,
    this.bottomPadding = 32,
    this.controller,
    super.key,
  });

  final List<Widget> children;

  /// Slivers adicionales después de [children] (listas largas perezosas).
  final List<Widget> slivers;
  final Future<void> Function()? onRefresh;
  final double bottomPadding;
  final ScrollController? controller;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final horizontal = math.max(Spacing.page, (constraints.maxWidth - kMaxContentWidth) / 2);
        final scroll = CustomScrollView(
          controller: controller,
          physics: const AlwaysScrollableScrollPhysics(),
          slivers: [
            if (children.isNotEmpty)
              SliverPadding(
                padding: EdgeInsets.fromLTRB(horizontal, Spacing.sm, horizontal, 0),
                sliver: SliverList.list(children: children),
              ),
            for (final sliver in slivers)
              SliverPadding(
                padding: EdgeInsets.symmetric(horizontal: horizontal),
                sliver: sliver,
              ),
            SliverToBoxAdapter(child: SizedBox(height: bottomPadding)),
          ],
        );
        if (onRefresh == null) return scroll;
        return RefreshIndicator(onRefresh: onRefresh!, child: scroll);
      },
    );
  }
}

/// Página raíz de una pestaña: encabezado institucional + contenido.
/// Al volver a tocar su pestaña se desplaza al inicio.
class TabPage extends ConsumerStatefulWidget {
  const TabPage({
    required this.subtitle,
    this.children = const [],
    this.slivers = const [],
    this.onRefresh,
    this.floatingActionButton,
    super.key,
  });

  final String subtitle;
  final List<Widget> children;
  final List<Widget> slivers;
  final Future<void> Function()? onRefresh;
  final Widget? floatingActionButton;

  @override
  ConsumerState<TabPage> createState() => _TabPageState();
}

class _TabPageState extends ConsumerState<TabPage> {
  final _scroll = ScrollController();

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(tabReselectProvider, (_, _) {
      // Solo la pestaña visible (las demás tienen los tickers desactivados).
      if (!TickerMode.valuesOf(context).enabled || !_scroll.hasClients) return;
      unawaited(
        _scroll.animateTo(
          0,
          duration: const Duration(milliseconds: 350),
          curve: Curves.easeOutCubic,
        ),
      );
    });
    return Scaffold(
      floatingActionButton: widget.floatingActionButton,
      body: SafeArea(
        bottom: false,
        child: PageBody(
          controller: _scroll,
          onRefresh: widget.onRefresh,
          bottomPadding: widget.floatingActionButton == null ? 32 : 96,
          slivers: widget.slivers,
          children: [
            InstitutionHeader(subtitle: widget.subtitle),
            ...widget.children,
          ],
        ),
      ),
    );
  }
}

/// Logo, nombre corto de la institución y acceso al perfil.
class InstitutionHeader extends ConsumerWidget {
  const InstitutionHeader({required this.subtitle, super.key});

  final String subtitle;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final institution = ref.watch(institutionProvider);
    final session = ref.watch(authControllerProvider);
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Padding(
      padding: const EdgeInsets.only(top: Spacing.md, bottom: Spacing.lg),
      child: Row(
        children: [
          InstitutionLogo(path: institution.mainLogo),
          const SizedBox(width: Spacing.md),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  institution.shortName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.titleMedium,
                ),
                Text(
                  subtitle,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.bodySmall?.copyWith(color: colors.muted),
                ),
              ],
            ),
          ),
          if (session != null)
            Tooltip(
              message: 'Mi perfil',
              child: Semantics(
                button: true,
                label: 'Abrir perfil de ${session.displayName}',
                child: InkWell(
                  borderRadius: BorderRadius.circular(16),
                  onTap: () => context.push('/profile'),
                  child: Padding(
                    padding: const EdgeInsets.all(2),
                    child: ExcludeSemantics(child: InitialsAvatar(initials: session.initials)),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// Introducción de página: fecha/contexto, título grande y subtítulo.
class PageIntro extends StatelessWidget {
  const PageIntro({required this.title, this.eyebrow, this.subtitle, super.key});

  final String? eyebrow;
  final String title;
  final String? subtitle;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = AppColors.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: Spacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (eyebrow != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 6),
              child: Text(
                eyebrow!.toUpperCase(),
                style: theme.textTheme.labelSmall?.copyWith(
                  color: colors.muted,
                  letterSpacing: 1.1,
                ),
              ),
            ),
          Semantics(
            header: true,
            child: Text(title, style: theme.textTheme.headlineMedium?.copyWith(fontSize: 28)),
          ),
          if (subtitle != null)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                subtitle!,
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.muted),
              ),
            ),
        ],
      ),
    );
  }
}

/// Página de detalle con barra superior y botón de regreso.
class DetailPage extends StatelessWidget {
  const DetailPage({
    required this.title,
    this.children = const [],
    this.slivers = const [],
    this.body,
    this.actions,
    this.onRefresh,
    this.bottomBar,
    this.floatingActionButton,
    super.key,
  });

  final String title;
  final List<Widget> children;
  final List<Widget> slivers;

  /// Cuerpo propio (p. ej. pestañas); reemplaza a [children]/[slivers].
  final Widget? body;
  final List<Widget>? actions;
  final Future<void> Function()? onRefresh;
  final Widget? bottomBar;
  final Widget? floatingActionButton;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title), actions: actions),
      floatingActionButton: floatingActionButton,
      bottomNavigationBar: bottomBar == null
          ? null
          : SafeArea(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(
                  Spacing.page,
                  Spacing.sm,
                  Spacing.page,
                  Spacing.md,
                ),
                child: Center(
                  heightFactor: 1,
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: kMaxContentWidth),
                    child: SizedBox(width: double.infinity, child: bottomBar),
                  ),
                ),
              ),
            ),
      body:
          body ??
          PageBody(
            onRefresh: onRefresh,
            slivers: slivers,
            bottomPadding: floatingActionButton == null ? 32 : 96,
            children: children,
          ),
    );
  }
}
