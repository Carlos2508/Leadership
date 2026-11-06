# QA de chat privado y desbloqueo biométrico

Fecha: 6 de noviembre de 2026. Base: `ce563a7`, ya publicada en `origin/main` al comenzar por solicitud del propietario. Desarrollo en `feat/private-chat-biometric-unlock` y posterior integración en `main`.

| Comprobación | Resultado |
| --- | --- |
| Suite .NET/SQL Server completa | **136 aprobadas, 0 fallos, 0 omitidas** |
| Suite Playwright completa, escritorio/teléfono | **85 aprobadas, 0 fallos, 3 omisiones previstas** |
| Recorrido adicional de migración MariaDB/SQL aislados | **2 aprobadas**; ejecutan las dos omisiones de migración de la suite normal |
| Chat después de los ajustes de móvil y respuesta perdida | **2 aprobadas**; recorrido específico de escritorio/teléfono repetido con Axe, reintentos, lectura, borrador siguiente y acceso revocado |
| Flutter | **91 pruebas aprobadas**, incluyendo 10 de sesión biométrica y 7 de interfaz conectada; análisis sin observaciones |
| Compilación Android | APK debug correcto en `mobile/build/app/outputs/flutter-apk/app-debug.apk` |
| Angular y publicación Release del host | Correctas; permanece el aviso conocido de optimización CommonJS de SweetAlert2 |
| Modelo School frente al snapshot | Sin cambios pendientes |
| Extractor del legado | **7 escenarios + 4 de configuración PHP correctos**; también se repitió el fixture MariaDB real independiente |

La única omisión web que no corresponde a migración es rendimiento en teléfono: se mide una vez en escritorio. El recorrido web completo conserva accesos/MFA/roles, personas/familias, estructura académica, matrícula, cursos, asistencia, notas, tareas/entregas, finanzas, avisos y portería, además del chat nuevo. El rendimiento ordinario del panel/API siguió dentro de sus presupuestos locales.

## Alcance del chat comprobado

- Solo docente asignado ↔ estudiante matriculado/ubicado en su curso, o ↔ encargado con vínculo vigente y facultad Communications. Un gestor administrativo sin ese contexto no puede leer conversaciones. Identificadores manipulados, otra sección y participantes prohibidos se rechazan; no hay lectura de familia sobre el chat del estudiante.
- Cada consulta vuelve a resolver el alcance. Retirar la facultad elimina contactos, historial y mensajes sin leer; la interfaz retira el contenido al comprobar la pérdida de acceso. Se comprueban también retiro efectivo y retiro programado para otra fecha, preservando el alcance hasta esa fecha, como la lista del curso.
- Creación simultánea y reintentos producen una conversación y un mensaje por intento. La secuencia conserva el orden, búsqueda/paginación se autorizan en SQL, el historial anterior no se pierde, un cursor de lectura viejo no retrocede y uno futuro se rechaza. No se aceptan mensajes vacíos, controles inválidos o más de 4000 caracteres.
- Recorrido navegador entre estudiante/docente/encargado: aviso al ingresar, icono/contador, contacto autorizado, error de red con texto conservado, reintento sin duplicado, respuesta automática, estado Leído, chat familiar independiente y revocación de la facultad por API auditada.
- Respuesta incierta: se guarda el mensaje, una consulta automática lo confirma y se pierde/retrasa su respuesta HTTP. Se libera el compositor y la respuesta antigua no bloquea ni borra el borrador siguiente. Comprobado en Flutter y navegador.
- Capturas revisadas de escritorio/teléfono y Axe sin infracciones en el recorrido aprobado. Se corrigieron contrastes de fechas/vistas previas y se dejó el compositor completamente dentro de la ventana del teléfono al abrir una conversación; el ciclo sigue visible en su encabezado. Flutter cubre 320 px, texto ampliado y modo oscuro.

## Alcance de biometría comprobado

Opt-in explícito; solo cookie Identity protegida, sin contraseña ni cookies antiforgery; reapertura sin login de contraseña después de lectura protegida y comprobación del servidor; cancelación, falta de red con reintento, datos inválidos, dispositivo sin biometría, sesión revocada, cierre de sesión, desactivación y MFA incompleto. La prueba de interfaz cubre contenido oculto en segundo plano y desbloqueo únicamente tras restauración validada. SQL/HTTP comprueba CSRF, ticket persistente/ordinario y revocación durable del sello.

La compilación Android verifica integración de FragmentActivity, permiso biométrico, tema AppCompat y plugins. Se resolvió el fallo de cachés incrementales Kotlin al combinar fuentes Pub en C: y repositorio en D: mediante compilación en proceso sin esas cachés. El APK es de prueba, no una publicación de tienda. **No se acreditó lectura de huella/Face ID en dispositivos físicos ni compilación iOS desde Windows.** Debe comprobarse en dispositivos compatibles antes de distribuirlo.

## Aislamiento y reproducción

Todo usa datos inventados. No se modificó PHP/MariaDB escolar, no se importaron personas reales ni se aplicó el esquema del chat a la base escolar. La migración aditiva incluida se aplica por el procedimiento normal de actualización de la instalación; el asistente de importación sigue rechazando actualizaciones ajenas silenciosas.

Una primera corrida E2E pasó sus dos casos, pero la limpieza intentó reconstruir binarios en uso. Se retiró exclusivamente su base verificada por UUID y fichas sintéticas coincidentes con las capturas. La limpieza ahora usa el harness ya construido; cada ejecución tiene además `ArtifactsPath` propio para binarios **y obj**, y un manifiesto sin secretos de propiedad del fixture. Se dejó intacta otra base E2E anterior de origen ajeno. Los manifiestos y datos de las corridas aprobadas se retiran al cerrar.

Comprobación SQL final: sin bases temporales propias nuevas, cero respaldos de migración E2E restantes y cero manifiestos activos propios; la base anterior ajena se conservó. No se detuvo ningún host ajeno.

Evidencia ignorada: `.local/qa/test-results/chat-full-final.trx`, `.local/qa/test-results/chat-security.trx`, `.local/qa/evidencia/chat-biometria-2026-11-06/` (regresión completa y chat final), `.local/qa/evidencia/migration-assistant/`. El APK está ignorado en `mobile/build/`. Contraseñas y códigos sintéticos se generan en memoria; no figuran en este informe ni en las capturas.

Reproducción: `dotnet test SchoolPlatform.slnx` con SQL local de pruebas; `./scripts/Invoke-Web.ps1 run build`; `./scripts/Invoke-Web.ps1 test`; `./tests/migration/Test-LegacyExtractor.ps1 -RunAssistantE2E`; en `mobile`, `flutter analyze`, `flutter test` y `flutter build apk --debug` con el SDK fijado del repositorio. No es una certificación de ausencia absoluta de defectos ni un despliegue.
