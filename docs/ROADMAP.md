# Plan de ejecución

Los IDs conservan trazabilidad con §20 del plan maestro. La aclaración más reciente del propietario fija instalaciones independientes por colegio.

| Orden | Trabajo | Estado / criterio de cierre |
| --- | --- | --- |
| BASE-001 | Solución .NET/Angular, configuración institucional y muestra del kit | Implementado en este incremento; verificar builds y pruebas documentadas |
| IAM-001a | Ingreso/salida con Identity, cuenta activa, SQL y CSRF | Implementado; no incluye invitaciones/recuperación |
| IAM-001b/003 | Invitaciones y activación/recuperación asistida | Siguiente: token de un uso, vencimiento, verificación de titular, sin exigir correo |
| IAM-002/004 | Permisos por recurso, revocación completa y auditoría durable | Siguiente antes de abrir directorio real |
| OPS-001a | CI con SQL y artefacto reproducible | Pendiente; scripts/checks locales ya disponibles |
| PEO-001/002 | Personas, perfiles, encargados y facultades | Pendiente; cuenta separada de expediente/ciclo |
| ACA-001 / ENR-001 | Ciclo, períodos, grupos y matrícula efectiva | Pendiente; sección opcional, sin contexto global en sesión |
| MIG-001a | Lectura/staging/dry-run acotado de personas | Después de personas; sin apply al origen ni importación implícita |
| FIN-001…006 | Planes, cargos masivos, pagos, recibos y solvencia | Pendiente; obligatorio antes de consultar notas |
| LRN-001…004 | Tareas, audiencia efectiva, archivos privados y entregas | Pendiente |
| ASM-001…005 | Libreta, publicación, recuperación, boletas y promoción | Solo núcleo de cálculo en memoria; resto pendiente |
| ATT / COM | Asistencia, avisos y notificaciones durables | Pendiente según dependencias |
| Piloto | Restauración, conciliación, capacitación y corte | Pendiente; entregar evidencias antes de activar operación real |
| MOB / EXT | Flutter y módulos adicionales | Posteriores a API/operación estables |

No declarar cerrada la fase 1 de la fuente: aún faltan permisos, invitaciones, auditoría, recuperación y CI. No sustituir esa fase con un dashboard de muestra. Cada incremento nuevo debe entregar un flujo pequeño con persistencia, autorización y pruebas reales.
