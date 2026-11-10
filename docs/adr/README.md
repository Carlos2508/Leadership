# Registro de decisiones

ADR-019 registra alta familiar atómica, atajos de carnets y pendientes por curso, conservando autorizaciones y consultas acotadas.

ADR-020 registra horarios semanales por grado/sección: borrador y publicación con vigencia, conflictos de grupo, docente y aula, reintentos idempotentes y consulta de docentes y familias autorizadas.

ADR-021 registra el carnet para todo el personal (no encargados), su tabla propia, la impresión con ciclo explícito y el rediseño CR80.

ADR-022 registra la calificación por actividades con punteo (suma del período, tareas con valor, plantillas), las páginas de tareas, calificaciones y asistencia del estudiante, el horario en PDF como el tablero y los adjuntos del chat con límite configurable.

ADR-011 registra el dashboard operativo por ciclo, permisos y recurso. ADR-012 registra roles de acceso configurables en SQL, independientes de los perfiles escolares.

ADR-010 registra el rediseño web y la navegación por Estudiantes, Padres y Docentes, conservando el modelo de personas y los permisos existentes. Complementa `docs/DESIGN.md`.

ADR-001 recoge monolito, Angular, SQL y proyecto/base independientes por colegio. ADR-002 recoge identidad inicial y configuración única de marca. La aclaración directa más reciente del usuario prevalece sobre propuestas anteriores de crecimiento/membresías del documento fuente.

ADR-008 registra el prototipo Flutter en `mobile/` con datos ficticios: valida experiencia y diseño, no integra la API ni sustituye la fase móvil.

ADR-007 registra la libreta por período, publicación ligada a un corte de solvencia y la consulta familiar condicionada en cada lectura.

ADR-006 registra obligaciones con clave de negocio, emisión con vista previa, pagos con aplicación exacta, recibos con snapshot, reversión y solvencia por corte.

ADR-005 registra ciclos con estados solo hacia adelante, períodos, grados, secciones opcionales y matrícula canónica con ubicaciones de vigencia.

ADR-004 registra el modelo de personas, perfiles, vínculos familiares con facultades explícitas, enlace cuenta↔persona y `SchoolDbContext` con auditoría en el mismo guardado.

ADR-003 registra permisos, activación/recuperación asistida, canje de un uso, transacciones, auditoría y revocación inmediata implementados en el segundo incremento. Complementa los pendientes de ADR-002 sobre la base inicial.

Antes de activar funciones siguientes registrar decisiones concretas sobre: hosting/licencias y operación de SQL; invitaciones/recuperación/MFA; modelo de persona/matrícula efectiva; redondeo y recuperación; solvencia y cortes; archivos/retención; outbox/worker; migración y archivo. No se consideran aprobadas porque aparezcan en una tabla de propuestas del plan maestro.
