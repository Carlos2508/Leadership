# Migración limitada de personas (MIG-001a)

## Preparación solicitada para 2027 — 6 de noviembre de 2026

El propietario operará la migración y pidió dejarla implementada/probada para estudiantes/familias con matrícula en el ciclo indicado y todos los accesos inicialmente inactivos. **El flujo v2 ya está implementado:** extracción de lectura, revisión, `--import-roster` con plan/apply/reconcile, matrícula y cuentas pendientes sin contraseña/códigos; ver [guía de uso](MIGRATION_2027.md), [QA](QA_MIGRATION_2027.md) y [ADR-015](adr/ADR-015-MIGRACION-INICIAL-2027.md). Probado solo con datos sintéticos. El comando v1 siguiente conserva su alcance anterior y no admite el paquete v2; usar la guía nueva para 2027.

Referencia PHP: `C:/xampp/htdocs/sys/sys-escolar` (sin modificar). Documento original copiado en `reference/PLAN_MAESTRO_ORIGINAL.md` (§16).

## Implementado

Herramienta de consola del host (no expuesta por HTTP) que importa **estudiantes, encargados ligados y relaciones** desde un archivo de *staging* normalizado y revisado:

```powershell
# 1. Plan (dry-run, sin escrituras): imprime checksum, conteos e incidencias
dotnet SchoolPlatform.Host.dll --import-people --Import:File=<staging.json>
# 2. Aplicar exactamente el archivo revisado, con una cuenta responsable con people.manage
dotnet SchoolPlatform.Host.dll --import-people --Import:File=<staging.json> --Import:Mode=apply --Import:Sha256=<checksum del plan> --Import:Actor=<usuario>
```

Formato del staging: `manifest` (`source`, `schoolId` de **esta** instalación, `scope`, `exportedAt`), `students[]` (`legacyId`, `givenNames`, `familyNames`, `birthDate` yyyy-MM-dd opcional, `studentCode`, `parentLegacyId`, `phone`, `email`) y `guardians[]` (`legacyId`, nombres, contactos, `relationship` opcional). Los nombres llegan **ya separados por revisión humana**; el importador nunca divide nombres automáticamente.

Reglas aplicadas:
- Rechaza otro `SchoolId`, un checksum distinto al revisado, una base con migraciones pendientes y un actor sin `people.manage`.
- Incidencias (no se importan): IDs vacíos/repetidos, nombres sin separar, fecha inválida (no se inventa), código ausente o repetido (no se unen personas por código), encargado sin estudiante seleccionado. Referencia a encargado inexistente: el estudiante se importa sin vínculo.
- Relación con parentesco del staging o «Otro vínculo» y **sin facultades**: nadie obtiene acceso al portal hasta que el colegio verifique cada vínculo. No se crean cuentas ni se migran contraseñas, notas, pagos, deudas ni tareas.
- Idempotente mediante `people.LegacyIdentities` (fuente, entidad, ID de origen → persona); repetir `apply` no duplica. Auditoría `LegacyPeopleImported` (tipo «Lote de importación») con conteos y prefijo del checksum, en la misma transacción.

Pruebas: `LegacyImportSqlTests` con staging **sintético**. Probado el modo plan contra `SchoolPlatform_Development` con un archivo sintético; no se aplicó ninguna importación.

## Pendiente

- Extractor de solo lectura desde MariaDB a staging (inspect/normalize/candidatos de duplicado) y su revisión por secretaría: requiere una exportación autorizada; **no** se conectó a la base del legado.
- Conciliación formal (reconcile), deltas entre ensayo y corte, archivo del legado y criterios de corte de §16.7.
- Nunca incluir exportaciones ni datos reales en Git, pruebas, logs o capturas. Cada colegio migra en su propia copia del proyecto y base.
