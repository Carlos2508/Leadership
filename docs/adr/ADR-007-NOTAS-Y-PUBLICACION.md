# ADR-007: libreta por período, publicación con corte de solvencia y consulta familiar

Estado: implementado en el séptimo incremento. Fecha: 5 de noviembre de 2026, America/Guatemala.

## Contexto

Reglas aceptadas: cero es una nota; pendiente no es cero; las notas familiares/estudiantiles requieren solvencia verificada en backend al corte de cada publicación; conservar ordinarias y revisiones. Pendientes de política: redondeo (59.5/59.999), promedio anual publicado, recuperación inferior e intentos.

## Decisión

- `assessment.PeriodScores`: una nota vigente por curso, período y estudiante; `decimal(7,3)` con hasta dos decimales capturados, 0–100; estados `Scored`, `Missing` (pendiente de entregar) y `Excused` (exonerado). CHECK en SQL: solo `Scored` lleva valor.
- `assessment.ScoreRevisions`: toda creación o cambio guarda valor/estado anterior y nuevo, actor, hora y motivo.
- Captura: docente con asignación no finalizada en el curso, o `academic.manage`. Solo estudiantes que pertenecieron al curso durante el período (ubicaciones fechadas que se cruzan con el período).
- Rowversion por nota: dos ediciones con la misma versión producen 409.
- `assessment.Publications`: una por período, con el `ClearanceCutoff` elegido por el colegio y nota interna. Exige que cada estudiante esperado tenga estado (registrado, pendiente o exonerado). Tras publicar, el docente ya no edita; `academic.manage` corrige con motivo obligatorio (`PublishedScoresCorrected` en auditoría).
- **Solo se publican notas de período tal como se capturaron.** No se publican promedios anuales, aprobado/reprobado ni redondeos: el cálculo de `SchoolPlatform.Assessment` sigue interno hasta aprobar la política.
- Consulta (`/api/v1/me/students/{id}/grades`): requiere ser el estudiante o encargado vigente con la facultad `AcademicRecords`; cada período publicado se evalúa en cada lectura con `ClearanceService` contra su corte. Sin solvencia, la respuesta no contiene notas: estado `Blocked` o `PendingFinancialSetup`; el detalle pendiente solo se muestra a un encargado con facultad `Finance`. Tareas y entregas (cuando existan) no se bloquean.
- Lista de cortes visible a personal académico (`/api/v1/years/{id}/clearance-cutoffs`) sin requerir permisos financieros.

## Pendientes

Promedio anual y boleta con política aprobada y snapshot; recuperaciones con resolución; notas por componentes/actividades vinculadas a tareas; reapertura excepcional de un período; notificaciones de publicación.
