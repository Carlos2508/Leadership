# Ciclos, grados, secciones e inscripciones

Implementado en el cuarto incremento (ACA-001/ENR-001). Decisión en `adr/ADR-005-CICLOS-Y-MATRICULA.md`.

## Flujo recomendado

1. **Ciclos y grados** (`/workspace/academic`, `academic.manage`): registrar grados del colegio; crear el ciclo (queda en borrador y el primero se vuelve predeterminado); agregar períodos; crear secciones si el colegio las usa; «Abrir inscripciones».
2. **Inscripciones** (`/workspace/enrollment?year=…`, `enrollment.manage`): buscar estudiante, grado, sección opcional y fecha de ingreso efectiva.
3. Cambios durante el ciclo: «Cambiar ubicación» (grado o sección con fecha efectiva y motivo), «Retiro» y «Reingreso». La ficha de la persona muestra la línea de tiempo por ciclo.
4. «Iniciar ciclo» congela fechas y períodos; «Cerrar ciclo» impide nuevas inscripciones y cambios.

## API

| Ruta | Requisito |
| --- | --- |
| GET `/api/v1/academic/years`, `/years/{id}`, `/grades` | `academic.read` |
| POST `/api/v1/academic/years`, `/years/{id}`, `/years/{id}/status`, `/years/{id}/default` | `academic.manage` + CSRF |
| POST `/api/v1/academic/years/{id}/periods`, `/periods/{id}`, `/years/{id}/periods/remove-last` | `academic.manage` |
| POST `/api/v1/academic/years/{id}/periods/generate` (`version`, `count` 1–12; solo sin períodos) y `/years/{id}/sections/copy` (`sourceYearId`; no duplica) | `academic.manage` |
| POST `/api/v1/academic/grades`, `/grades/{id}` | `academic.manage` |
| POST `/api/v1/academic/years/{id}/sections`, `/years/{y}/sections/{id}`, `/…/remove` | `academic.manage` |
| GET `/api/v1/years/{yearId}/enrollments?gradeId&sectionId&status&search&page` | `academic.read` |
| GET `/api/v1/people/{id}/enrollments` | `academic.read` |
| POST `/api/v1/enrollments`, `/enrollments/{id}/placement`, `/withdraw`, `/reactivate` | `enrollment.manage` + CSRF |

Errores esperados: `year-overlap`, `year-locked`, `periods-required`, `invalid-transition`, `already-enrolled`, `year-not-open`, `section-full`, `resource-changed` (409); validaciones 400. Un cuerpo JSON mal formado (por ejemplo una fecha vacía) devuelve 400 `invalid-request`, no 500.

## Auditoría

`AcademicYearCreated/Updated/StatusChanged/DefaultChanged`, `AcademicPeriodAdded/Updated/Removed`, `GradeCreated/Updated`, `SectionCreated/Updated/Removed` (tipo `AcademicYear`/`Grade`) y `StudentEnrolled`, `EnrollmentPlacementChanged`, `StudentWithdrawn`, `StudentReenrolled` (tipo `Enrollment`), en el mismo guardado que el cambio.

## Pruebas

`AcademicSqlTests` (7): ciclo con transiciones solo hacia adelante y períodos válidos; inscripción canónica y sección opcional; historial fechado con retiro y reingreso; cupo y cambios concurrentes; visibilidad de la ubicación por facultad; separación de permisos; cuerpo mal formado como 400. `e2e/academic.spec.ts` prepara ciclo, período, grado, sección, inscripción y movimiento a «Sin sección» en escritorio y móvil con Axe.
