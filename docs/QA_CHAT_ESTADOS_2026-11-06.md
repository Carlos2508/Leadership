# QA de entrega, visto y actividad del chat

Fecha: 6 de noviembre de 2026, America/Guatemala. Rama `feat/chat-presence-receipts`, sobre `main` `f84af4d` (incluye las mejoras de uso real y el chat/huella anterior). Solicitud expresa del propietario: doble check, hora de visto y estado en línea/activo. Decisión en ADR-016.

## Resultado

| Comprobación | Resultado |
| --- | --- |
| .NET/SQL Server, suite completa | **141 aprobadas**, 0 fallos, 0 omitidas; 13 específicas del chat |
| Flutter, suite completa | **94 aprobadas**; análisis sin observaciones |
| Playwright, chat escritorio/teléfono | **2 aprobadas**, con Axe, capturas y ciclo de actividad real de la pestaña |
| Playwright, suite completa | **85 aprobadas**, 0 fallos y 3 omisiones previstas |
| Asistente de migración, MariaDB/SQL reales aislados | **2 E2E aprobadas**, escritorio/teléfono; extracción sintética correcta |
| Builds | Angular, APK Android debug y host Release correctos |
| EF School | Sin cambios pendientes respecto al snapshot |

Las dos omisiones ordinarias de migración se ejecutaron correctamente aparte con el fixture MariaDB, después de la regresión general. La restante es rendimiento en teléfono, medido una sola vez en escritorio. La validación final de entradas inválidas se acompañó de otra corrida completa de las 141 pruebas SQL, también aprobada.

## Casos comprobados

- Enviado inicialmente; recibir el resumen y confirmarlo actualiza a Entregado sin marcar lectura. Abrir y llegar al final produce Visto con primera hora del servidor. GET sin confirmación no entrega/lee; reintentos y lecturas posteriores conservan fechas. Confirmar una secuencia anterior no entrega mensajes futuros. El poll incremental actualiza mensajes que ya estaban en pantalla.
- Otra cuenta sin alcance no modifica recibos de la conversación. Actividad HTTP exige sesión y antiforgery, usa la cuenta del principal y verifica sello; las conversaciones siguen autorizándose por recursos. Lectura familiar/estudiantil separada y revocación de Communications siguen comprobadas en SQL y navegador.
- Las entradas de actividad con identificador vacío, versión inválida, más de 100 confirmaciones o un elemento NULL se rechazan con 400, sin error interno.
- En línea con pulso confirmado; ocultar pestaña muestra Últ. vez y volver al primer plano recupera En línea. Varias sesiones no se desconectan entre sí. Peticiones tardías fuera de orden no revierten un cierre, incluso si el cierre llegó antes del primer pulso.
- Caducidad a los 45 s sin red; expiración local del indicador con reloj del dispositivo desfasado; bloqueo nativo impide anunciar actividad. Cambio de sello revoca presencia y rechaza pulsos con el sello anterior.
- Actualización de una base sintética que ya contenía lectura en el esquema anterior: conserva cursores, inicializa entrega y deja fechas históricas desconocidas en NULL. No inventa horas al volver a confirmar el mismo cursor.
- Escritorio y teléfono: checks y hora legibles, indicador bajo el nombre y compositor dentro de la pantalla; Axe sin infracciones en las pantallas recorridas. Flutter cubre 320 px, texto 1,5 y tema oscuro, reintentos, respuesta perdida y conservación del borrador.

## Evidencia y operación

Evidencia local ignorada: `.local/qa/test-results/presence-core.trx`, `presence-full.trx`, `presence-flutter.log`, `presence-android.log`, `presence-release.log`, `presence-migration.log`; capturas revisadas en `.local/qa/evidencia/chat-presence-2026-11-06/` y su carpeta `regresion-completa/`. APK en `mobile/build/app/outputs/flutter-apk/app-debug.apk`; publicación local aislada en `.local/publish/chat-presence/`. No son despliegues ni una publicación en tienda.

La migración aditiva `20261107000825_ChatReceiptsAndPresence` se aplicó únicamente en fixtures SQL propios. Requiere el procedimiento normal de actualización antes de servir esta versión; no se aplica silenciosamente desde el asistente de migración. No se modificó el PHP/MariaDB, la base escolar ni se usaron personas reales. El timestamp de la migración usa UTC (7 de noviembre); esta verificación ocurrió todavía el día 6 en Guatemala.

En línea describe primer plano confirmado, no escritura ni atención a un chat particular. Un cierre abrupto puede seguir figurando activo hasta 45 s; las actualizaciones ordinarias del interlocutor llegan mediante el poll de conversación. Los avisos siguen dentro de la plataforma, sin push de sistema con el app cerrado. La prueba física de biometría y el build iOS siguen pendientes del incremento anterior. Las pruebas no certifican ausencia absoluta de defectos.
