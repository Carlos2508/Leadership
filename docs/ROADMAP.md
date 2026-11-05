# Plan de ejecución

Los IDs conservan trazabilidad con §20 del plan maestro. La aclaración más reciente del propietario fija instalaciones independientes por colegio.

| Orden | Trabajo | Estado / criterio de cierre |
| --- | --- | --- |
| BASE-001 | Solución .NET/Angular, configuración institucional y muestra del kit | Implementado en este incremento; verificar builds y pruebas documentadas |
| IAM-001a | Ingreso/salida con Identity, cuenta activa, SQL y CSRF | Implementado; no incluye invitaciones/recuperación |
| IAM-001b/003 | Invitaciones y activación/recuperación asistida | Implementado con código de un uso, vencimiento y entrega manual; el operador registra la verificación del titular |
| IAM-002/004 | Permisos por recurso, revocación completa y auditoría durable | Implementado para gestión de cuentas/auditoría y sus recursos; cerrar sesiones y recuperar contraseña invalida cookies previas. Extender autorización por recurso a cada módulo escolar nuevo |
| IAM-005 | MFA y revisión de privilegios | Pendiente antes de producción para operadores sensibles |
| OPS-001a | CI con SQL y artefacto reproducible | Pendiente; scripts/checks locales ya disponibles |
| PEO-001/002 | Personas, perfiles, encargados y facultades | Implementado (tercer incremento): persona estable, perfiles, vínculos con facultades explícitas e historial, cuenta↔persona y autoservicio por recurso. Pendiente: identificadores oficiales, fusión de duplicados, política de mayoría de edad/custodia |
| ACA-001 / ENR-001 | Ciclo, períodos, grupos y matrícula efectiva | Implementado (cuarto incremento): ciclos con estados, períodos, grados, secciones opcionales con cupo, inscripción canónica y ubicaciones fechadas. Pendiente: cursos/materias/carga docente y membresía por curso (ACA-002) |
| ACA-002 | Materias, cursos, personal docente y listas | Implementado (sexto incremento): perfil de personal, materias, cursos por grado o sección, asignaciones fechadas y lista derivada por fecha. Pendiente: planes de estudio versionados, electivas con membresía explícita, horarios |
| MIG-001a | Lectura/staging/dry-run acotado de personas | Después de personas; sin apply al origen ni importación implícita |
| FIN-001…006 | Planes, cargos masivos, pagos, recibos y solvencia | Implementado parcialmente (quinto incremento): conceptos, calendario, emisión con vista previa e idempotencia, ajustes, anulación, pagos exactos, recibo con snapshot, reversión, solvencia por corte y estado de cuenta del encargado. Pendiente: planes versionados, comprobantes subidos, saldo a favor, cierre/conciliación, pasarelas, FEL |
| LRN-001…004 | Tareas, audiencia efectiva, archivos privados y entregas | LRN-001 implementado (octavo incremento): actividades con audiencia derivada, entregas de texto, tardías y retroalimentación. Pendiente: archivos privados, rúbricas, vínculo con libreta, notificaciones |
| ASM-001…005 | Libreta, publicación, recuperación, boletas y promoción | Implementado parcialmente (séptimo incremento): libreta por período con revisiones, publicación por período con corte de solvencia y consulta familiar/estudiantil condicionada. Pendiente: promedio anual/boleta con política aprobada, recuperaciones, componentes, promoción |
| ATT / COM | Asistencia, avisos y notificaciones durables | ATT-001 implementado (noveno incremento): asistencia por curso y fecha con resumen familiar. COM pendiente |
| Piloto | Restauración, conciliación, capacitación y corte | Pendiente; entregar evidencias antes de activar operación real |
| MOB / EXT | Flutter y módulos adicionales | Posteriores a API/operación estables |

La base de identidad ya tiene flujos persistentes; la fase operativa sigue parcial por CI, MFA, despliegue, backups/restauración y endurecimiento de producción. PEO-001/002, ACA-001/ENR-001, ACA-002 y el núcleo FIN quedaron implementados. ASM-001/002 y LRN-001 quedaron implementados. Siguiente: pulido transversal; luego ATT (asistencia), COM (avisos), MFA/CI y boletas cuando se apruebe la política de redondeo. No sustituir esos módulos con el directorio de muestra. Cada incremento entrega un flujo con persistencia, autorización y pruebas reales.
