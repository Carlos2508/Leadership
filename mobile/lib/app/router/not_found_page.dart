import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../core/widgets/states.dart';

class NotFoundPage extends StatelessWidget {
  const NotFoundPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(),
      body: EmptyState(
        icon: Icons.explore_off_rounded,
        title: 'No encontramos esta página',
        message: 'Es posible que el enlace haya cambiado o que no tengas acceso.',
        action: FilledButton(onPressed: () => context.go('/'), child: const Text('Ir al inicio')),
      ),
    );
  }
}
