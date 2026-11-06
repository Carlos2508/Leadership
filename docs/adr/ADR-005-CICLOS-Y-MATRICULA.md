# ADR-005: ciclos, estructura académica y matrícula efectiva

Estado: implementado en el cuarto incremento. Fecha: 5 de noviembre de 2026, America/Guatemala.

## Contexto

El legado guarda el ciclo global en configuración y otro en sesión; escrituras y lecturas pueden ir a ciclos distintos, la sección vale `0` cuando falta y los cambios crean copias de personas. El plan (§9.2, §10) propone ciclo explícito por URL/comando, matrícula canónica anual y ubicaciones con vigencia.

## Decisión

- `academic.Years`: etiqueta única, fechas, estado `Draft → EnrollmentOpen → Active → Closed` (solo avanza) y un único predeterminado (índice filtrado). No se superponen. Activar exige períodos. Reabrir un ciclo cerrado requiere un flujo de rectificación aún no implementado.
- `academic.Periods`: secuencia 1..12, nombres por defecto con el término institucional (`Terms.Period`), dentro del ciclo, sin superposición y en orden. Solo se editan en `Draft`/`EnrollmentOpen`; solo se quita el último en `Draft`. **No se fija la cantidad de períodos**; las reglas de cálculo de notas siguen en `ACADEMIC_RULES.md`.
- `academic.Grades`: catálogo de grados (código, nombre, nivel, orden, activo), no versionado por ciclo en este incremento.
- `academic.Sections`: opcionales, por ciclo y grado, con jornada y cupo opcionales. Una sección con historial no se elimina ni cambia de grado.
- `academic.Enrollments`: **una por estudiante y ciclo** (índice único), estado `Active/Withdrawn/Cancelled/Completed`, fecha de ingreso efectiva separada de la fecha de captura.
- `academic.EnrollmentPlacements`: grado + sección opcional con intervalo `[StartsOn, EndsOn)` (fin exclusivo); a lo sumo una abierta por inscripción (índice filtrado). Cambiar grado/sección cierra la vigente y abre otra con fecha efectiva; el retiro cierra la ubicación; el reingreso en el mismo ciclo abre una nueva desde una fecha posterior o igual al retiro. `EndsOn = StartsOn` conserva una corrección del mismo día como historial vacío.
- Toda escritura usa rowversion de la inscripción; dos cambios concurrentes producen un éxito y un 409. El cambio de ubicación usa una transacción con dos guardados (cerrar y luego abrir) por el índice filtrado.
- Cupo: se rechaza inscribir/mover a una sección llena (`section-full`). El cupo es un dato del colegio.
- Permisos: `academic.read` (consultar estructura, listas e historial), `academic.manage` (ciclos, períodos, grados, secciones) y `enrollment.manage` (inscribir, mover, retirar, reingresar). Perfiles: Secretaría incluye inscripciones; nueva Coordinación académica; Administración institucional incluye todo.
- Autoservicio: el estudiante ve su ubicación vigente; un encargado solo la ve con la facultad `AcademicRecords`. Sin la facultad, solo ve nombre y código del estudiante vinculado.
- Las URL llevan el ciclo (`/workspace/enrollment?year=…`, `/api/v1/years/{yearId}/enrollments`); el predeterminado solo se usa para la navegación inicial, nunca decide una escritura.
- El listado de inscripción conserva también grado/sección/estado/búsqueda/página en la URL y restaura ese contexto con el historial. Una carga fallida retira filas y acciones anteriores y permite reintentar; un ciclo solicitado desconocido no se sustituye por el predeterminado. Ver `QA_EXPERIENCIA_2026-11-05.md`.

## Pendientes conscientes

- Cursos, materias por plan, asignación docente y membresía por curso (`CourseEnrollment`): siguiente dependencia para tareas/notas.
- Ingreso tardío: se registra la fecha efectiva; el tratamiento de períodos previos (NotEnrolled/TransferredCredit) depende de la política de notas pendiente.
- Promoción y cierre con `Completed`: se definirán con boletas/promoción.
- Anulación de una inscripción errónea (`Cancelled`) sin retiro: no expuesta todavía.
