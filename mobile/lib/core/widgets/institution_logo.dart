import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// Logo institucional desde la configuración. Si el archivo falta muestra un
/// marcador neutro en lugar de un logo inventado.
class InstitutionLogo extends StatelessWidget {
  const InstitutionLogo({required this.path, this.size = 44, super.key});

  final String path;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      image: true,
      label: 'Logo institucional',
      child: Image.asset(
        path,
        width: size,
        height: size,
        fit: BoxFit.contain,
        cacheWidth: (size * MediaQuery.devicePixelRatioOf(context)).round(),
        errorBuilder: (context, error, stackTrace) {
          final pair = AppColors.of(context).tone(AppTone.brand);
          return Container(
            width: size,
            height: size,
            decoration: BoxDecoration(color: pair.background, shape: BoxShape.circle),
            child: Icon(Icons.school_rounded, color: pair.foreground, size: size * 0.55),
          );
        },
      ),
    );
  }
}
