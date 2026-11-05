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
| PEO-001/002 | Personas, perfiles, encargados y facultades | Pendiente; cuenta separada de expediente/ciclo |
| ACA-001 / ENR-001 | Ciclo, períodos, grupos y matrícula efectiva | Pendiente; sección opcional, sin contexto global en sesión |
| MIG-001a | Lectura/staging/dry-run acotado de personas | Después de personas; sin apply al origen ni importación implícita |
| FIN-001…006 | Planes, cargos masivos, pagos, recibos y solvencia | Pendiente; obligatorio antes de consultar notas |
| LRN-001…004 | Tareas, audiencia efectiva, archivos privados y entregas | Pendiente |
| ASM-001…005 | Libreta, publicación, recuperación, boletas y promoción | Solo núcleo de cálculo en memoria; resto pendiente |
| ATT / COM | Asistencia, avisos y notificaciones durables | Pendiente según dependencias |
| Piloto | Restauración, conciliación, capacitación y corte | Pendiente; entregar evidencias antes de activar operación real |
| MOB-000 | Prototipo Flutter navegable con datos ficticios | Implementado en `mobile/` (ADR-004); no integra API ni cuenta como MOB |
| MOB / EXT | Flutter y módulos adicionales | Posteriores a API/operación estables |

La base de identidad ya tiene flujos persistentes; la fase operativa sigue parcial por CI, MFA, despliegue, backups/restauración y endurecimiento de producción. El siguiente bloque funcional es PEO-001/002: personas y facultades familiares, seguido de ciclos/matrícula. No sustituir esos módulos con el directorio de muestra. Cada incremento entrega un flujo con persistencia, autorización y pruebas reales.
