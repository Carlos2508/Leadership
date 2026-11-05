# Tareas y entregas

Implementado en el octavo incremento (LRN-001, sin archivos).

## Reglas

- `learning.Assignments`: actividad de un curso con título, instrucciones, entrega (`DateTimeOffset`), «acepta tardías» y estado `Draft → Published → Closed` (solo avanza). Crear/editar/publicar/cerrar: docente con asignación no finalizada o `academic.manage`.
- **Audiencia derivada**: estudiantes del curso en algún momento entre la fecha de publicación y la de entrega (ubicaciones fechadas, zona horaria institucional). Quien ingresó antes de la entrega la recibe; quien salió antes de publicarse, no.
- `learning.Submissions`: una entrega vigente por estudiante y actividad, con hora del servidor, intentos y rowversion. Solo la cuenta del propio estudiante entrega; encargados y personal no pueden entregar por él. Se puede actualizar mientras esté publicada, dentro de plazo (o con tardías permitidas) y no revisada. Las tardías se marcan.
- Retroalimentación docente marca la entrega como revisada. Aún no se vincula a la libreta (las notas de período se capturan aparte).
- Visibilidad: el estudiante ve instrucciones, su texto y la retroalimentación; un encargado con `AcademicRecords` ve estado y retroalimentación, no el texto. **Las tareas no se bloquean por solvencia.**
- Fechas: el formulario usa la hora local del navegador y se guarda como instante; se muestra en la zona horaria institucional.

## API

| Ruta | Requisito |
| --- | --- |
| GET/POST `/api/v1/courses/{courseId}/assignments`, POST `/assignments/{id}`, `/publish`, `/close` | Docente asignado o `academic.manage` (lectura también `academic.read`) |
| GET `/api/v1/courses/{courseId}/assignments/{id}/submissions` | Igual; 404 sin acceso |
| POST `/api/v1/courses/{courseId}/submissions/{id}/feedback` | Docente asignado o `academic.manage` |
| GET `/api/v1/me/students/{id}/assignments` | El estudiante o encargado con `AcademicRecords`; si no, 404 |
| POST `/api/v1/me/assignments/{id}/submission` | Solo la cuenta del estudiante destinatario |

## Interfaz

`/workspace/courses/{id}/assignments` para docentes; sección «Tareas» en `/workspace/my/students/{id}` con diálogo «Entregar». Enlaces desde la lista del curso y «Mis cursos».

## Pruebas

`LearningSqlTests` (2): borrador invisible, entrega y reenvío con versión, facultad del encargado sin texto, entrega solo propia, revisión que bloquea reenvío; plazo estricto, tardías marcadas, cierre y audiencia sin estudiantes retirados. `e2e/learning.spec.ts`: actividad publicada, entrega del estudiante y retroalimentación en escritorio y móvil.

## Archivos (LRN-002, implementado)

Adjuntos privados de la entrega: PDF, PNG, JPG, DOCX, XLSX, PPTX y TXT validados por firma, máx. 10 MB y 5 por entrega, guardados fuera de la web con clave aleatoria y SHA-256. Solo después de guardar el texto; se pueden quitar (borrado lógico) mientras la entrega sea editable. Descarga (`GET /api/v1/files/{id}`) para el propio estudiante o personal que pueda ver el curso; el encargado no descarga trabajos. Ver `OPERATIONS.md`.

## Pendiente

Antivirus y retención de archivos, rúbricas y vínculo con componentes de la libreta, plazos individuales, notificaciones.
