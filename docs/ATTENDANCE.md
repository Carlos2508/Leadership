# Asistencia

Implementado en el noveno incremento (ATT-001).

- `attendance.Sessions`: una sesión por curso y fecha escolar; `attendance.Records`: un registro por estudiante con `Present/Absent/Late/Excused`, observación (200) y rowversion. Sin columnas por día del mes.
- Registra el docente con asignación no finalizada o `academic.manage`, solo para estudiantes del curso en esa fecha (ubicaciones fechadas), dentro del ciclo y nunca en fechas futuras. Se puede consultar una fecha futura pero no editarla.
- Todos inician como «Presente» en la pantalla hasta que el docente cambie el estado; se guarda toda la lista con versiones (409 ante cambios concurrentes).
- Portal: el estudiante o un encargado con `AcademicRecords` ve el resumen del ciclo vigente y las últimas incidencias. **No depende de la solvencia.**
- Auditoría `AttendanceRecorded` (fecha y cantidad de cambios) en el mismo guardado.

API: GET/POST `/api/v1/courses/{id}/attendance?date=` (docente/personal académico; 404 sin acceso) y GET `/api/v1/me/students/{id}/attendance`.

UI: `/workspace/courses/{id}/attendance` (enlace desde la lista del curso y «Mis cursos») y sección «Asistencia» en el portal.

La fecha consultada queda explícita en `?date=YYYY-MM-DD`; el historial restaura la lista correspondiente. Las marcas modificadas se protegen antes de cambiar fecha/curso o salir; la salida espera un guardado en curso. Los indicadores distinguen registro completo/parcial de cambios sin guardar, y los errores conservan el formulario o presentan reintento sin mostrar filas de otro día. Los botones avanzan un día calendario, sin presumir días lectivos. Ver `QA_EXPERIENCIA_2026-11-05.md`.

Pruebas: `AttendanceSqlTests` (2) y pasos de asistencia en `e2e/learning.spec.ts`.

Pendiente: asistencia por jornada (no por curso), justificaciones con evidencia, alertas a familias y reportes.
