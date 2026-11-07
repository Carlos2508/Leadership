# ADR-016: chat privado entre docentes y estudiantes/encargados

Fecha: 6 de noviembre de 2026. Decisión solicitada por el propietario; alcance confirmado: **solo docentes de los cursos del estudiante**.

## Decisión

Una conversación corresponde a un ciclo explícito, estudiante de referencia, docente y persona interlocutora (el propio estudiante o un encargado). Es privada y de dos participantes. No hay grupos, estudiante↔estudiante, estudiante↔encargado, ni directorio abierto de personas. Tener un rol administrativo o un cargo llamado «Docente» no concede acceso.

Cada lista, búsqueda, lectura, envío, marca de lectura y resumen vuelve a calcular el alcance en SQL: personas activas, cuentas activas con acceso preparado, vínculos cuenta/persona vigentes, matrícula/ubicación del estudiante y asignación del docente al curso de ese grado/sección. Para encargados se requiere además vínculo familiar vigente y facultad **Communications** sin revocar. Sin alcance se responde 404 y se retiran los mensajes de la interfaz tras la comprobación. No existe permiso de lectura general para Dirección.

Se usa la fecha del colegio configurada en `InstitutionOptions`, limitada al intervalo del ciclo para preparar conversaciones antes de clases y consultar el cierre. Se permite escribir en `EnrollmentOpen` y `Active`; un ciclo cerrado conserva la consulta mientras siga existiendo el alcance permitido. Retiro según su fecha efectiva, matrícula cancelada, facultad revocada y fin de asignación docente dejan de autorizar. Un retiro programado para más adelante conserva el acceso hasta hacerse efectivo, igual que la lista del curso; la fecha final docente es inclusiva y la ubicación anual usa fin exclusivo. Conversaciones de encargado y estudiante sobre la misma persona son independientes; una cuenta no ve la otra.

Tablas `chat.Conversations` y `chat.Messages`, migración aditiva `20261106225356_PrivateChat`. Índice único del par/contexto, secuencia por conversación e índice único de intento de envío por remitente. Transacciones serializables y bloqueo SQL por par/conversación: crear/reintentar no duplica; cambiar el contenido de un intento confirmado produce 409. El cliente conserva la clave en un fallo incierto y consulta desde la última secuencia mostrada, incluso tras enviar, para no saltarse mensajes intercalados.

Texto plano de hasta 4000 caracteres, sin HTML ejecutable. Historial de 50 mensajes por página, conversaciones/contactos de 40 por página y búsqueda autorizada en el servidor; vista previa de 120 caracteres. Lectura mediante cursor monotónico enviado por la interfaz al estar visible al final del historial, sin marcar como leídos mensajes nuevos que aún no llegaron. Auditoría de inicio/envío con identificadores y secuencia, sin contenido, nombres ni contactos. No se borra automáticamente el historial sin una política de conservación aprobada.

Todas las rutas usan `ActiveAccount`, autorización por recurso y `ApiWriteFilter`/CSRF en escrituras. Límite de 60 envíos por minuto y cuenta. No se reutilizan `GateDevice`, cuentas demo ni autorizaciones por nombre de rol. Ciclo en URL/comando; el resumen de mensajes sin leer puede abarcar los ciclos autorizados, igual que la campana de avisos.

Web `/workspace/messages` y app conectado: icono con contador, aviso de mensajes sin leer al ingresar, búsquedas, selección de contacto/contexto, burbujas, historial, reintento, protección de borradores al salir y lectura confirmada. Actualización automática por consultas periódicas (3 s en conversación; 10 s en contador), con una solicitud en curso, pausa en segundo plano/bloqueo y revalidación del servidor.

## Ampliación: entrega, hora de lectura y actividad

El propietario solicitó expresamente doble check, hora de visto y estado en línea/activo. Migración aditiva `20261107000825_ChatReceiptsAndPresence`: cursores de entrega por participante, `DeliveredAt`/`ReadAt` por mensaje y tabla `chat.Presence`.

- **✓ Enviado:** persistido en el servidor. **✓✓ Entregado:** una sesión del destinatario confirmó la recepción del aviso en su plataforma visible/desbloqueada, o confirmó lectura. No se presupone que abrió el chat. El resumen incluye solo identificadores/ciclo/secuencia pendientes; el cliente devuelve esos límites mediante `/api/v1/chat/activity`. Obtener un GET por sí solo no confirma entrega ni lectura.
- **✓✓ Visto a las…**, azul: el destinatario confirmó lectura al estar al final de la conversación visible. Primera hora de entrega y primera lectura fijadas por el servidor; reintentos, otra sesión y lecturas posteriores no las cambian. Lectura implica entrega. Los cursores son monotónicos y las restricciones SQL conservan orden y límites. El poll incremental devuelve también cambios de recibos para los mensajes ya cargados.
- **En línea:** al menos una sesión del interlocutor mantiene actividad de primer plano. Pulso cada 10 s, caducidad de 45 s; al ocultar, cerrar o bloquear se intenta cerrar inmediatamente su propio registro. Corte de red/cierre abrupto se resuelve por caducidad. La interfaz hace caducar el indicador aunque no llegue otra respuesta y corrige el reloj del dispositivo con la hora del servidor. **Últ. vez** corresponde a la última actividad confirmada; no afirma que alguien esté mirando esta conversación ni que esté escribiendo.

Cada pestaña/proceso tiene un identificador aleatorio y versión creciente: peticiones tardías no reabren actividad cerrada, y cerrar una sesión no oculta las demás. Máximo 32 registros por cuenta, reutilizando primero registros caducados; el sello de seguridad vigente invalida actividad tras revocar sesiones. No se guardan IP, modelo de equipo o geolocalización. Solo se proyecta la actividad del interlocutor dentro de una conversación que ya supera el alcance SQL, sin directorio de presencia ni consulta abierta por persona. El endpoint de actividad verifica la cuenta propia activa y sello; cada confirmación de entrega vuelve a autorizar su conversación. CSRF y límite de 120 peticiones/minuto/cuenta.

Los mensajes antiguos conservan sus cursores de lectura: la actualización inicializa la entrega desde ellos, pero deja las fechas desconocidas en NULL. Se muestra «Visto» sin inventar una hora histórica. No se consideran leídos mensajes solo por entrar al inicio, recibir el aviso o permanecer en línea.

## Límites de este incremento

Chat de texto y notificaciones **dentro de la plataforma**; no se envían mensajes externos, correo, WhatsApp ni push de sistema con el app cerrado. No incluye llamadas, grupos, archivos, audio, edición/borrado de mensajes ni lectura institucional de todos los chats. Estas capacidades requieren diseño y autorización propios; no se anuncian como implementadas.

La migración debe aplicarse mediante el procedimiento normal de actualización de la instalación antes de servir este código. El asistente de importación no aplica silenciosamente la migración de chat. Se mantiene separado el ensayo sintético del corte real.
