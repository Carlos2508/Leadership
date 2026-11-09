# ADR-020: Horarios semanales por grado y sección

Fecha: 8 de noviembre de 2026. Estado: aceptado (rama `codex/course-timetables`, completada e integrada en `main`).

## Contexto

El colegio necesita preparar el horario de clases de cada grupo, publicarlo con fechas de vigencia y que docentes, estudiantes y familias autorizadas lo consulten y lo impriman. El horario depende de cursos y asignaciones docentes que ya existen; no los reemplaza.

## Decisión

- **Modelo** (`Timetables/*`, `SchoolDbContext.Timetables.cs`, migración `CourseTimetables`):
  - Plan por ciclo, grado y sección; si no tiene sección, aplica a todo el grado.
  - Revisiones `Draft`/`Published` con vigencia `[StartsOn, UntilExclusive)` y la estructura (días y períodos) en JSON. Las celdas son clase o descanso.
  - Al publicar se guardan copias de materia, docente y aula, para que el horario histórico no cambie si después cambian los nombres.
  - Catálogos de aulas y de estructuras reutilizables.
- **Borrador y publicación:**
  - Un borrador por plan, con versión optimista. Al guardar, las celdas se reemplazan en la misma transacción serializable, con bloqueo `SchoolPlatform.Timetables`.
  - Publicar recalcula las celdas, exige al menos una clase y rechaza conflictos (409 `timetable-conflicts`): docente sin asignación vigente durante toda la vigencia, o cruce de grupo, docente o aula con otro horario publicado que se superpone en fechas y en día.
  - Una versión nueva recorta la vigencia de la anterior; no la borra.
- **Reintentos:** cada escritura lleva un UUID de intento. El recibo (`TimetableOperations`) guarda el actor y la huella del comando; repetir devuelve el mismo resultado y reutilizar el UUID para otra operación da 409.
- **Asignaciones:** `CourseService.EndAssignmentAsync` usa el mismo bloqueo y no permite terminar una asignación que tiene clases publicadas después de esa fecha (409 `teacher-has-timetable`).
- **Permisos:** preparar y publicar exigen `academic.manage` (y MFA si la instalación la configura); consultar la administración exige `academic.read`. En autoservicio (`/api/v1/me/timetables`):
  - El docente ve el horario de su persona vinculada.
  - El estudiante ve el suyo; la familia lo ve con la facultad `AcademicRecords`, según la ubicación vigente en esa fecha.
  - Sin condición de solvencia, porque el horario no es un dato financiero ni una nota.
  - El ciclo y la fecha siempre van explícitos.
- **Documentos:** PDF carta o A4 (`TimetablePdf.cs`), con fines de semana si la estructura los incluye y textos institucionales solo desde la configuración.
- **Web y app:**
  - `/workspace/timetables`: consulta por grupo o docente, impresión y lista de horarios preparados.
  - `/workspace/timetables/edit`: editor con vigencia, estructura, aulas, copiar un día, copiar desde otro horario y avisos de conflicto antes de publicar.
  - En el teléfono el tablero se muestra por día.
  - Flutter: `timetable_page.dart`, accesible desde Colegio, Docencia y Estudiantes, con impresión mediante `printing`.
  - Los estilos se cargan solo con las pantallas de horarios (`_timetable*.scss` como estilos de componente sin encapsulación), para no aumentar el paquete inicial.

## Consecuencias

- La migración aditiva `CourseTimetables` se aplica con la actualización normal del esquema; no se aplicó a la base escolar.
- No hay generación automática de horarios ni asistencia por bloque; quedan fuera de este incremento.
