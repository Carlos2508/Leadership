# Plan de ejecución

Los IDs conservan trazabilidad con §20 del plan maestro. La aclaración más reciente del propietario fija instalaciones independientes por colegio.

| Orden | Trabajo | Estado / criterio de cierre |
| --- | --- | --- |
| BASE-001 | Solución .NET/Angular, configuración institucional y muestra del kit | Implementado en este incremento; verificar builds y pruebas documentadas |
| IAM-001a | Ingreso/salida con Identity, cuenta activa, SQL y CSRF | Implementado; no incluye invitaciones/recuperación |
| IAM-001b/003 | Invitaciones y activación/recuperación asistida | Implementado con código de un uso, vencimiento y entrega manual; el operador registra la verificación del titular |
| IAM-002/004 | Permisos por recurso, revocación completa y auditoría durable | Implementado para gestión de cuentas/auditoría y sus recursos; cerrar sesiones y recuperar contraseña invalida cookies previas. Extender autorización por recurso a cada módulo escolar nuevo |
| IAM-005 | MFA y revisión de privilegios | MFA implementado (TOTP, respaldo, desafío, restablecimiento, exigencia configurable por permiso). Pendiente: revisión periódica de privilegios |
| OPS-001a | CI con SQL y artefacto reproducible | Workflow escrito (`.github/workflows/ci.yml`), sin ejecutar aún en GitHub; readiness y respaldo con restauración verificada implementados (`OPERATIONS.md`) |
| PEO-001/002 | Personas, perfiles, encargados y facultades | Implementado (tercer incremento): persona estable, perfiles, vínculos con facultades explícitas e historial, cuenta↔persona y autoservicio por recurso. Pendiente: identificadores oficiales, fusión de duplicados, política de mayoría de edad/custodia |
| ACA-001 / ENR-001 | Ciclo, períodos, grupos y matrícula efectiva | Implementado (cuarto incremento): ciclos con estados, períodos, grados, secciones opcionales con cupo, inscripción canónica y ubicaciones fechadas. Pendiente: cursos/materias/carga docente y membresía por curso (ACA-002) |
| ACA-002 | Materias, cursos, personal docente y listas | Implementado (sexto incremento): perfil de personal, materias, cursos por grado o sección, asignaciones fechadas y lista derivada por fecha. Pendiente: planes de estudio versionados, electivas con membresía explícita, horarios |
| MIG-001a | Lectura/staging/dry-run acotado de personas | Importador plan/apply desde staging revisado implementado (`MIGRATION.md`). Pendiente: extractor de solo lectura del legado y conciliación |
| FIN-001…006 | Planes, cargos masivos, pagos, recibos y solvencia | Implementado parcialmente (quinto incremento): conceptos, calendario, emisión con vista previa e idempotencia, ajustes, anulación, pagos exactos, recibo con snapshot, reversión, solvencia por corte y estado de cuenta del encargado. Pendiente: planes versionados, comprobantes subidos, saldo a favor, cierre/conciliación, pasarelas, FEL |
| LRN-001…004 | Tareas, audiencia efectiva, archivos privados y entregas | LRN-001/002 implementados: actividades con audiencia derivada, entregas con adjuntos privados, tardías y retroalimentación. Pendiente: antivirus, rúbricas, vínculo con libreta |
| ASM-001…005 | Libreta, publicación, recuperación, boletas y promoción | Implementado parcialmente (séptimo incremento): libreta por período con revisiones, publicación por período con corte de solvencia y consulta familiar/estudiantil condicionada. Pendiente: promedio anual/boleta con política aprobada, recuperaciones, componentes, promoción |
| ATT / COM | Asistencia, avisos y notificaciones durables | ATT-001 y COM-001 implementados (asistencia por curso; avisos internos con audiencia, facultad de comunicaciones y lecturas). Pendiente: notificaciones externas |
| DOC / GATE | PDFs de período/recibo, carnets y portería | Implementado en servidor y web; lector Flutter con API de dispositivo. ADR-009 / GATE.md. Pendiente: licencia confirmada, impresión y pruebas Android/iOS físicas; sin promedios anuales |
| PERF | Compresión, caché de bundles y navegación | Implementado y medido localmente; sin Redis. No equivale a carga/latencia de producción |
| Piloto | Restauración, conciliación, capacitación y corte | Pendiente; entregar evidencias antes de activar operación real |
| MOB-000 | Prototipo Flutter navegable con datos ficticios | Implementado en `mobile/` (ADR-008); no integra API ni cuenta como MOB |
| MOB / EXT | Flutter y módulos adicionales | Lector de portería conectado mediante token de dispositivo. Los otros módulos permanecen como prototipo; falta integración de identidad/datos y pruebas físicas |

La fase operativa sigue parcial por CI remoto, despliegue/endurecimiento, plan de respaldos de producción, licencia, equipos y piloto. IAM, personas, matrícula, cursos, finanzas/solvencia, notas por período, tareas/archivos, asistencia y avisos tienen flujos persistentes y pruebas; no son todas las funcionalidades de cada fase. Para notas anuales/recuperaciones/promoción se necesita política aprobada. La experiencia general Flutter sigue siendo prototipo, salvo el lector real de portería.
