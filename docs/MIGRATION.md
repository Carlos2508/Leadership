# Migración futura y fuentes

Referencia PHP: `C:/xampp/htdocs/sys/sys-escolar`. Documento original: `C:/xampp/htdocs/sys/ANALISIS_Y_PLAN_MAESTRO_PLATAFORMA_ESCOLAR.md`, copiado a `docs/reference/PLAN_MAESTRO_ORIGINAL.md`.

Esta implementación no arrancó PHP/MariaDB, no extrajo registros personales y no modificó sus archivos ni su base. La base nueva `SchoolPlatform_Development` pertenece exclusivamente al proyecto .NET.

Alcance futuro: personas de estudiantes, encargados ligados y relaciones verificables. No importar automáticamente notas, deudas, pagos, tareas, documentos, fotos, sesiones, hashes ni roles numéricos. Las cuentas nuevas se activan por procedimiento propio. Padre inexistente/vacío genera revisión, no relación inventada. No unir personas por nombre/apellido solamente.

El importador aún no existe. Debe tener inspect/plan/dry-run/apply/reconcile, manifiesto de destino/alcance, staging restringido, mapas de identidad e idempotencia por ID de origen. No ejecutar `apply` antes de contar con padrón revisado, respaldo y conciliación. Los conteos del documento son evidencia de aquella copia, no números que deban sembrarse en destino.

No incluir exportaciones ni datos reales en Git, pruebas, logs o capturas. La migración de cada colegio se ejecuta contra su propia copia del proyecto y base de datos, sin directorio compartido entre instituciones.
