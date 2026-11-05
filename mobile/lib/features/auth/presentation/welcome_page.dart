import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/providers.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/institution_logo.dart';
import '../../../shared/back_navigation.dart';

class WelcomePage extends ConsumerWidget {
  const WelcomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final institution = ref.watch(institutionProvider);
    final colors = AppColors.of(context);
    final theme = Theme.of(context);
    final headline = institution.tagline.isEmpty ? institution.displayName : institution.tagline;

    return ExitOnDoubleBack(
      child: Scaffold(
        body: DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [colors.heroStart, colors.heroEnd],
            ),
          ),
          child: SafeArea(
            child: LayoutBuilder(
              builder: (context, constraints) => SingleChildScrollView(
                padding: const EdgeInsets.symmetric(horizontal: 28, vertical: 24),
                child: ConstrainedBox(
                  constraints: BoxConstraints(minHeight: constraints.maxHeight - 48),
                  child: Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 440),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Column(
                            children: [
                              const SizedBox(height: Spacing.lg),
                              InstitutionLogo(path: institution.mainLogo, size: 112),
                              const SizedBox(height: Spacing.xxl),
                              const _Illustration(),
                            ],
                          ),
                          Column(
                            children: [
                              const SizedBox(height: Spacing.xxl),
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                                decoration: BoxDecoration(
                                  color: colors.accent.withValues(alpha: 0.18),
                                  borderRadius: BorderRadius.circular(999),
                                ),
                                child: Text(
                                  'TU CAMPUS, MÁS CERCA',
                                  style: theme.textTheme.labelSmall?.copyWith(
                                    color: Colors.white,
                                    letterSpacing: 1.2,
                                  ),
                                ),
                              ),
                              const SizedBox(height: Spacing.lg),
                              Semantics(
                                header: true,
                                child: Text(
                                  headline,
                                  textAlign: TextAlign.center,
                                  style: theme.textTheme.headlineLarge?.copyWith(
                                    color: Colors.white,
                                    fontSize: 32,
                                  ),
                                ),
                              ),
                              if (institution.welcomeMessage.isNotEmpty) ...[
                                const SizedBox(height: Spacing.md),
                                Text(
                                  institution.welcomeMessage,
                                  textAlign: TextAlign.center,
                                  style: theme.textTheme.bodyLarge?.copyWith(
                                    color: Colors.white.withValues(alpha: 0.86),
                                    height: 1.45,
                                  ),
                                ),
                              ],
                              const SizedBox(height: Spacing.xxl),
                              SizedBox(
                                width: double.infinity,
                                child: FilledButton.icon(
                                  style: FilledButton.styleFrom(
                                    backgroundColor: colors.accent,
                                    foregroundColor: colors.onAccent,
                                    minimumSize: const Size.fromHeight(56),
                                  ),
                                  onPressed: () async {
                                    await ref.read(welcomeSeenProvider.notifier).markSeen();
                                    if (context.mounted) context.go('/login');
                                  },
                                  iconAlignment: IconAlignment.end,
                                  icon: const Icon(Icons.arrow_forward_rounded),
                                  label: const Text('Comenzar'),
                                ),
                              ),
                              const SizedBox(height: Spacing.lg),
                              Text(
                                '${institution.displayName} · Prototipo con datos ficticios',
                                textAlign: TextAlign.center,
                                style: theme.textTheme.bodySmall?.copyWith(
                                  color: Colors.white.withValues(alpha: 0.7),
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _Illustration extends StatelessWidget {
  const _Illustration();

  @override
  Widget build(BuildContext context) {
    final colors = AppColors.of(context);
    return ExcludeSemantics(
      child: SizedBox(
        height: 170,
        width: 260,
        child: Stack(
          alignment: Alignment.center,
          children: [
            Container(
              width: 170,
              height: 170,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: Colors.white.withValues(alpha: 0.12),
              ),
            ),
            Container(
              width: 112,
              height: 112,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: Colors.white.withValues(alpha: 0.16),
              ),
              child: const Icon(Icons.auto_stories_rounded, size: 58, color: Colors.white),
            ),
            Positioned(
              left: 18,
              top: 22,
              child: Icon(Icons.star_rounded, color: colors.accent, size: 34),
            ),
            Positioned(
              right: 22,
              top: 8,
              child: Container(
                width: 22,
                height: 22,
                decoration: BoxDecoration(color: colors.accent, shape: BoxShape.circle),
              ),
            ),
            Positioned(
              right: 30,
              bottom: 18,
              child: Icon(
                Icons.auto_awesome_rounded,
                color: Colors.white.withValues(alpha: 0.8),
                size: 26,
              ),
            ),
            Positioned(
              left: 36,
              bottom: 14,
              child: Icon(
                Icons.edit_rounded,
                color: colors.accent.withValues(alpha: 0.9),
                size: 24,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
