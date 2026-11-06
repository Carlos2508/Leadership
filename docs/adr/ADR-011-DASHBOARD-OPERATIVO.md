# ADR-011: inicio orientado al trabajo y al estado del colegio

Fecha: 5 de noviembre de 2026, America/Guatemala. Estado: implementado.

El propietario pidió retirar «Tus módulos» del dashboard: duplicaba el menú y no informaba sobre el colegio. El inicio debe responder qué ocurre, qué requiere atención y dónde resolverlo.

- `/api/v1/dashboard` exige `ActiveAccount` y evalúa permisos efectivos mediante la autorización del servidor, incluido MFA configurado. Las secciones no autorizadas son nulas. El endpoint no devuelve valores de calificaciones.
- El ciclo seleccionado queda en `?year=<id>` y en cada enlace de acciones escolares. La primera lectura resuelve un ciclo disponible y lo hace explícito en la URL. Un ciclo solicitado sin acceso devuelve 404; no se cambia silenciosamente por otro. Los ciclos disponibles de una cuenta sin permisos generales proceden de sus cursos o de estudiantes con facultad académica/financiera.
- Finanzas del ciclo: cobro de hoy, saldo vencido/vence hoy/por vencer, siete días de cobros y recibos recientes. Solo aplicaciones explícitas de pagos no revertidos. Un recibo que cubre varios ciclos aporta a cada uno solo sus aplicaciones; los ajustes reducen el cargo. Sin cargos no significa solvencia.
- Académico: matrícula activa por grado y avance de registros/publicaciones de cada período. Pendientes/exonerados son registros, no notas numéricas ni promedios. Los períodos futuros se muestran como próximos; solo un período terminado con audiencia y sin publicación genera seguimiento. No se publica automáticamente.
- Portería muestra la fecha institucional de hoy y todos los ciclos vigentes, indicado expresamente. La falta de entrada registrada no se declara ausencia. No se inventa calendario lectivo u horario de clases.
- Docentes: únicamente cursos con asignación autorizada, entregas por revisar, próximas fechas de entrega y cantidad de registros de asistencia de hoy. Las acciones llevan al curso concreto.
- Familias/estudiantes: tareas pendientes y próximos vencimientos mediante `LearningService.StudentAssignmentsAsync`, con facultad `AcademicRecords` o acceso propio. El saldo se consulta solo para un encargado con facultad `Finance`, después de `PeopleService.StudentAccessAsync`. El estudiante no recibe acceso financiero por defecto. Las calificaciones siguen en su portal, exclusivamente por `GradeService.MyGradesAsync`, con solvencia por publicación en cada lectura.
- Actualización manual, hora de lectura, estados de carga y errores recuperables visibles. Un fallo no produce ceros, datos sintéticos o información anterior de otro ciclo. Los avisos conservan su audiencia y sus errores tienen reintento propio.

No requiere migración de esquema. Diseño y capturas se validan en escritorio, móvil y tamaños intermedios. Ver `PROGRESS.md` para resultados finales.
