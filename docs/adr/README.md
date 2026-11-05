# Registro de decisiones

ADR-001 recoge monolito, Angular, SQL y proyecto/base independientes por colegio. ADR-002 recoge identidad inicial y configuración única de marca. La aclaración directa más reciente del usuario prevalece sobre propuestas anteriores de crecimiento/membresías del documento fuente.

ADR-008 registra el prototipo Flutter en `mobile/` con datos ficticios: valida experiencia y diseño, no integra la API ni sustituye la fase móvil.

ADR-007 registra la libreta por período, publicación ligada a un corte de solvencia y la consulta familiar condicionada en cada lectura.

ADR-006 registra obligaciones con clave de negocio, emisión con vista previa, pagos con aplicación exacta, recibos con snapshot, reversión y solvencia por corte.

ADR-005 registra ciclos con estados solo hacia adelante, períodos, grados, secciones opcionales y matrícula canónica con ubicaciones de vigencia.

ADR-004 registra el modelo de personas, perfiles, vínculos familiares con facultades explícitas, enlace cuenta↔persona y `SchoolDbContext` con auditoría en el mismo guardado.

ADR-003 registra permisos, activación/recuperación asistida, canje de un uso, transacciones, auditoría y revocación inmediata implementados en el segundo incremento. Complementa los pendientes de ADR-002 sobre la base inicial.

Antes de activar funciones siguientes registrar decisiones concretas sobre: hosting/licencias y operación de SQL; invitaciones/recuperación/MFA; modelo de persona/matrícula efectiva; redondeo y recuperación; solvencia y cortes; archivos/retención; outbox/worker; migración y archivo. No se consideran aprobadas porque aparezcan en una tabla de propuestas del plan maestro.
