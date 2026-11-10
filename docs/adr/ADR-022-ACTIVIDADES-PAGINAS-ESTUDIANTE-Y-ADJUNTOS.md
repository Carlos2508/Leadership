# ADR-022: Calificación por actividades, páginas del estudiante y adjuntos del chat

Fecha: 9 de noviembre de 2026. Estado: aceptado por solicitud del propietario (rama `feat/experiencia-escolar`).

## Contexto

Revisión del propietario sobre la instancia QA:

- Las notas solo admitían un número de 0 a 100 por período. Las tareas no tenían punteo.
- En el sistema anterior cada curso definía sus «actividades a calificar» (por ejemplo, ocho por período) con su punteo, y la nota era la suma de los puntos.
- Tareas, Calificaciones y Asistencia del estudiante apuntaban a la misma página, e Inicio y «Académico» llevaban al mismo lugar.
- El encabezado repetía «Mi cuenta» y ofrecía «Ir al sitio público».
- El PDF del horario no se parecía al tablero y el logo casi no se veía.
- El chat no permitía adjuntos.
- La asistencia de una fecha futura y una tarea sin destinatarios fallaban sin explicar por qué.

## Decisión

### Calificación por actividades

- **Modelo** (`Grades/ActivityModel.cs`, `ActivityService.cs`, migración `GradeActivities`, esquema `assessment`):
  - `Activities`: curso, período, nombre, punteo y una tarea opcional.
  - `ActivityScores`: puntos, «no entregó» o «exonerado».
  - `ActivityScoreRevisions` y plantillas institucionales (`ActivityTemplates`).
- **Totales:** las actividades de un curso y período suman como máximo 100 puntos.
  - El resultado del período es la suma de los puntos obtenidos: «no entregó» vale 0, «exonerado» no suma y, si todas las actividades están exoneradas, el resultado es Exonerado.
  - El total se escribe en `PeriodScores` con su revisión. Así la publicación, los boletines y la solvencia (`ClearanceService`) no cambian.
  - Con actividades, la nota directa (0–100) se rechaza (409 `activity-graded`). Sin actividades sigue disponible, para los cursos que no las usen.
  - No hay promedios anuales ni redondeos: es una suma exacta de hasta dos decimales.
- **Permisos y concurrencia:**
  - Antes de publicar el período editan el docente asignado o coordinación; después, solo coordinación y con motivo.
  - Las escrituras usan transacción serializable con el bloqueo `SchoolPlatform.Gradebook.{curso}` y versión por celda.
  - Quitar una actividad con punteos exige motivo. La actividad queda marcada como quitada, sus puntos y revisiones se conservan y el total se recalcula.
- **Tareas con punteo:**
  - Al crear la tarea se eligen el período y su valor, y la tarea se vuelve una actividad de la libreta.
  - El docente califica desde la entrega (puntos y comentario opcional) o registra «no entregó» sin entrega.
  - No se puede quitar el punteo de una tarea ya calificada.
  - La fecha de entrega debe estar dentro del ciclo del curso. Esa era la causa de las tareas «sin destinatarios».
- **Lectura de familias y estudiantes:** el desglose por actividad (`MyActivity`) se entrega únicamente por `GradeService.MyGradesAsync`, es decir, en períodos publicados y con la solvencia evaluada en cada lectura. En las tareas se muestra cuánto vale cada una; los puntos obtenidos aparecen solo cuando el período ya está publicado y disponible.
- **Plantillas:** las crea coordinación (`academic.manage`, con motivo) en `/workspace/grades/templates`. El docente las aplica en la libreta con un clic y luego puede ajustarlas. No se crea ninguna plantilla por defecto: es política del colegio.
- **Libreta PDF:** con actividades, sale apaisada con una columna por actividad (puntos, NE, EX) y el total.

### Páginas del estudiante y de la familia

- `/workspace/my/students/{id}` (resumen), `/tasks`, `/grades` y `/attendance`, con pestañas comunes. El menú del estudiante enlaza a cada página; se quitó «Académico».
  - **Tareas:** por entregar, vencidas, entregadas, revisadas, filtro por curso, calendario de entregas y ventana de entrega con archivos.
  - **Calificaciones:** períodos publicados, una tarjeta por materia con anillo y barras por actividad, y la evolución entre períodos (sin promedios).
  - **Asistencia:** calendario mensual con ✓, ✗, tarde o justificada por día, el detalle por curso y las marcas de portería.
- Nuevo `GET /api/v1/me/students/{id}/attendance/calendar?from&to` (máximo 62 días), con la facultad `AcademicRecords` o siendo el propio estudiante.

### Interfaz

- **Encabezado:** solo el nombre y el perfil escolar. «Mi cuenta» y «Cerrar sesión» quedan solo en el pie del menú y se quitó «Ir al sitio público». El menú usa una columna del ancho de la barra y los nombres largos terminan en elipsis.
- **Asistencia de solo consulta:** explica el motivo (`ReadOnlyReason`): fecha futura o curso no asignado.
- **Guardado automático:** la libreta ya no cambia el botón a «Guardando…» al guardar sola; el estado se muestra junto al botón.

### Horario en PDF

Se dibuja como el tablero: cuadrícula con celdas separadas, bloques de clase en acento y de descanso en gris, horas y nombre del período a la izquierda. Usa el escudo (`CardLogo`/`MainLogo`) en la banda. La banda ya no repite la sede cuando forma parte del nombre.

### Adjuntos del chat

- **Envío** (`Chat/ChatAttachments.cs`, migración `ChatAttachmentsAndSettings`):
  - Un archivo por mensaje, con las mismas reglas de acceso, bloqueo y reintento que el texto.
  - Si no hay texto, el mensaje nombra el archivo para que los clientes anteriores lo muestren.
- **Archivos permitidos:**
  - Se aceptan PDF, Word, Excel, PowerPoint, imágenes PNG/JPG, TXT/CSV y ZIP.
  - Se verifica la firma real del contenido. Los formatos con macros nunca se aceptan.
  - Un ZIP se abre y se rechaza si contiene programas o scripts (.exe, .bat, .js, .ps1…), si está cifrado o si es demasiado grande al descomprimirse.
- **Almacenamiento y descarga:** los archivos se guardan fuera del sitio público. La descarga exige ser parte de la conversación.
- **Límite:** 5 MB por defecto, configurable por el administrador (`accounts.manage`, con motivo y auditoría) en «Administración → Ajustes». Se guarda en la tabla `platform.Settings`, entre 1 y 25 MB. No es un editor de marca.

## Consecuencias

- Dos migraciones aditivas (`GradeActivities`, `ChatAttachmentsAndSettings`); no se aplicaron a la base escolar.
- El app Flutter muestra los adjuntos como texto «📎 archivo» y todavía no los descarga. Sus pantallas de notas no muestran el desglose por actividad.
- Pendiente de decisión del colegio: la plantilla de actividades por período y si las tareas sin punteo deben contar.
