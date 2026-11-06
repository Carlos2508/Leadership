# Materias, cursos, personal docente y listas

Implementado en el sexto incremento (ACA-002). Complementa ADR-005.

## Decisiones

- **Personal** (`people.Staff`): perfil con cargo/puesto, agregado desde la ficha de la persona. Los permisos siguen viniendo de la cuenta; el alcance docente, de las asignaciones.
- **Materias** (`academic.Subjects`): catálogo con código y nombre únicos. Planes de estudio versionados quedan pendientes.
- **Cursos** (`academic.Courses`): materia del ciclo para un grado completo o para una sección. Índices únicos filtrados impiden duplicados; el servicio rechaza mezclar «todo el grado» y «por sección» para la misma materia (`course-scope-conflict`).
- **Asignaciones docentes** (`academic.TeachingAssignments`): titular o auxiliar, con inicio, fin y motivo. Una asignación abierta por persona y curso. El acceso docente dura **mientras la asignación no haya finalizado** (puede preparar un curso antes de su inicio); al finalizarla deja de ver la lista ese mismo día.
- **Lista del curso derivada**: no se copia una membresía por curso. Los estudiantes de un curso en una fecha son las inscripciones del ciclo cuya ubicación vigente en esa fecha coincide con el grado (y la sección, si el curso es por sección). Un cambio de sección mueve al estudiante de curso desde la fecha efectiva. Cuando existan electivas se agregará membresía explícita por curso.
- **Autorización por recurso**: `academic.read` ve todos los cursos; un docente solo los suyos (`/api/v1/me/courses`) y su lista (`/api/v1/courses/{id}/roster`); cualquier otro recibe 404.

## API

| Ruta | Requisito |
| --- | --- |
| GET `/api/v1/academic/subjects`, `/api/v1/years/{yearId}/courses?gradeId`, `/api/v1/academic/staff?search` | `academic.read` |
| POST `/api/v1/academic/subjects`, `/subjects/{id}`, `/api/v1/years/{yearId}/courses` | `academic.manage` + CSRF |
| POST `/api/v1/courses/{id}/teachers`, `/courses/{id}/teachers/{assignmentId}/end` | `academic.manage`, con motivo |
| POST `/api/v1/years/{yearId}/courses/copy` (`sourceYearId`, `teachers`): copia cursos por grado/sección (nombre) y, si se pide, docentes vigentes al cierre del origen con motivo auditado | `academic.manage` |
| GET `/api/v1/courses/{id}/roster?on=` | `academic.read` o docente con asignación no finalizada; si no, 404 |
| GET `/api/v1/me/courses` | Cuenta activa vinculada a personal con asignaciones |
| POST `/api/v1/people/{id}/roles` con `role: "staff"` y `position` | `people.manage` |

## Interfaz

`/workspace/courses?year=…` (materias, crear cursos por grado o sección, asignar/finalizar docentes) y `/workspace/courses/{id}` (lista a una fecha). En `/workspace`, el personal con asignaciones ve «Mis cursos». La ficha de la persona permite agregar el perfil de personal y el directorio filtra por «Personal».

## Pruebas

`CourseSqlTests` (2): unicidad y conflicto de alcance; lista por fecha ante cambio de sección; docente que solo ve su curso, otro docente sin acceso y revocación al finalizar. `e2e/courses.spec.ts`: materia, cursos por sección, asignación docente y vista «Mis cursos» del docente en escritorio y móvil con Axe.
