# Progreso

Actualizado: 9 de noviembre de 2026, America/Guatemala.

## Revisión visual y de experiencia — incremento vigente

- Revisión solicitada por el propietario sobre `main` `1d45066`, en `codex/qa-visual-experiencia`. Corregidos ocho grupos de hallazgos: nombre del hijo en páginas académicas, datos/respuestas cruzados entre hijos, errores y respuestas tardías de adjuntos, pérdida del texto de una entrega, consultas fallidas sin reintento, acciones fuera de pantalla en Inventario/Horarios, atajo del rol propio fuera de la primera página y campos de punteo ocultos por títulos largos en teléfono.
- 166 vistas privadas antes/después por ocho perfiles, 42 diálogos en tres tamaños, 9 vistas públicas y 23 páginas de PDF evaluadas mediante capturas. Axe sin infracciones detectadas y sin desbordamiento horizontal de página en las matrices; tablas/pestañas conservan su desplazamiento interno. La libreta poblada se verificó también a 320 px.
- Validación: 183 .NET/SQL, 105 Flutter y análisis limpio, 115 pruebas en la suite web completa más 2 nuevas de libreta y 2 de migración aislada; las 10 pruebas de notas/tareas relacionadas se repitieron después del ajuste final de CSS. Angular/Release correctos. Informe `QA_REVISION_VISUAL_2026-11-09.md`, evidencia bajo `.local/qa/evidencia/review-visual-2026-11-09/`. Sin cambios de esquema propios ni de la base escolar; conserva los pendientes físicos y del app en ADR-022.

## Experiencia escolar: actividades con punteo, páginas del estudiante y adjuntos — rama `feat/experiencia-escolar`

- Las notas del período se arman con actividades con punteo, como en el sistema anterior: la nota es la suma de los puntos (sin promedios ni redondeos). Las tareas pueden valer puntos y se califican desde la entrega. Hay plantillas institucionales de actividades y una libreta en PDF con una columna por actividad. Sin actividades, se mantiene la nota directa 0–100. ADR-022.
- Estudiantes y familias tienen páginas propias de Tareas (pendientes, historial y calendario), Calificaciones (tarjetas por materia con el detalle de actividades, solo de períodos publicados y con solvencia) y Asistencia (calendario mensual con ✓, ✗, tarde o justificada). Se quitó «Académico» del menú.
- Encabezado solo con nombre y perfil; «Mi cuenta» y «Cerrar sesión» quedan en el menú. Asistencia de solo consulta y tareas sin destinatarios explican el motivo. Horario en PDF con la misma cuadrícula del tablero y el escudo visible. Chat con adjuntos (PDF, Office, imágenes, ZIP revisado) y límite configurable por administración (5 MB por defecto).

## Horarios semanales por grado y sección — incremento vigente

- Preparar el horario de cada grupo con la estructura de la semana (días, períodos, recreos), aulas y docentes asignados; guardar el borrador; publicar con vigencia por fechas y versiones que conservan el historial. Al publicar se rechazan cruces de grupo, docente o aula y los docentes sin asignación vigente. Hay reintentos seguros, estructuras reutilizables, copiar un día o copiar desde otro horario, e impresión en carta o A4. ADR-020.
- Consulta: administración con `academic.read`; docentes ven su horario y estudiantes o familias con `AcademicRecords` ven el del grupo, en web y app. Ciclo y fecha siempre explícitos.

## Simplificación operativa sin cambiar políticas — incremento vigente

- El propietario autorizó mejorar el flujo conservando el modelo nuevo y el rendimiento. Nueva familia reúne encargados, hijos, vínculos e inscripción por ciclo/fecha en un recorrido web adaptable a teléfono; reutiliza fichas existentes, sin otorgar facultades por parentesco ni crear cuentas/cargos. ADR-019 y guía `SIMPLIFICAR_TRABAJO_ESCOLAR.md`.
- Guardado atómico con bloqueo de matrícula/cupos compartido y comprobante por intento; una respuesta perdida se reintenta sin duplicar, un fallo en un hijo revierte el conjunto. Conserva los cuatro permisos requeridos, MFA por operación, CSRF y auditoría. Nueva migración aditiva `20261109043528_FamilyRegistrationReceipts`, aplicada solo a fixtures propios.
- Carnets accesibles directamente desde Inscripciones; pendientes de notas por curso/docente con enlace a libreta y período, consultados bajo demanda y con la misma pertenencia fechada de publicación. Listas de cursos pasan de una consulta de conteo por curso a una agrupada; prueba con interceptor confirma consultas constantes de uno a nueve cursos.
- SQL Server: 160 pruebas correctas, diez nuevas. Flutter: 101 correctas, análisis limpio. Angular/Release y snapshot correctos. 95 E2E web + 2 E2E de migración MariaDB/SQL correctos; dos omisiones ordinarias resueltas en fixture aparte, rendimiento móvil medido en escritorio. QA y evidencia en `QA_SIMPLIFICACION_FLUJOS_2026-11-08.md`. Incremento integrado en main por autorización vigente, conservando las otras ramas. No se incorpora asistencia diaria por grupo, recibos conjuntos ni reglas de cobro pendientes. No se aplicó el esquema ni se importaron personas a la base escolar.

## Carnet para todo el personal y rediseño — integrado en main

- El propietario pidió carnet para todas las personas del colegio, excepto encargados, y un diseño más profesional basado en su propuesta. Ahora reciben carnet los estudiantes y el personal (docentes y demás puestos): tabla `people.StaffCards`, reemplazo con motivo y auditoría, impresión individual y de todo el personal con ciclo explícito. El carnet de personal no marca asistencia en portería. ADR-021.
- Carnet CR80 rediseñado (frente y reverso) para ambos tipos; el personal se distingue por el pie de color de acento. Textos institucionales solo desde `config/institution.json` (`StaffCardNotice` opcional y vacío).
- Integrado en `main` junto con los horarios por solicitud del propietario. Pendiente: imprimir y probar con lectores reales, y que el colegio redacte el aviso del carnet de personal si lo desea.

## Inventario y bodega del colegio — incremento vigente

- El propietario pidió terminar `feat/school-store-inventory`, aclaró uso de bodega escolar y autorizó merge/push a main. Catálogo general de artículos, categoría/ubicación, stock mínimo, carga inicial, entradas, salidas, conteos físicos, historial y exportación CSV en web; operación conectada en el app. Guía `INVENTARIO_BODEGA.md`, ADR-018.
- Transacciones SQL con saldo/movimiento/auditoría atómicos, versión optimista, secuencia por artículo y UUID de intento para no duplicar existencias al reintentar. Stock negativo rechazado; archivo solo con saldo cero; historial protegido en EF y SQL. Permisos configurables `inventory.read`, `inventory.manage`, `inventory.move`, con CSRF/ActiveAccount y MFA por operación. No se amplían roles existentes automáticamente.
- Migración aditiva `20261107004645_SchoolStoreInventory`, aplicada solo a fixtures propios; el nombre corresponde a la preparación inicial y el alcance final es bodega general. Existencias independientes del ciclo académico. No se modificaron el legado, datos escolares ni la instalación.
- Se integró `origin/main` `5fdc9fb` en la rama de trabajo, conservando las correcciones de carnets, libreta, chat y app docente. Regresión combinada: 150 SQL, 101 Flutter, 89 E2E web + 2 de migración MariaDB/SQL correctas, análisis limpio, builds Angular/Release/APK y snapshot correctos. Dos omisiones ordinarias se resolvieron en el fixture aparte; rendimiento móvil se mide en escritorio. La revisión detectó y corrigió la pérdida del botón de reintento del chat tras un poll, en web/app, con cobertura expresa. Informe `QA_INVENTARIO_BODEGA_2026-11-08.md`. Incremento integrado en main por autorización del propietario; sin despliegue ni actualización de la base escolar.

## Revisión completa de flujos (integrada en main)

- Sin errores de lógica en 42 casos límite (dinero, notas, inscripción, permisos, portería y chat). Se corrigieron cuatro fallas visuales del chat, el orden de los carnets por grado y el aviso de nota inválida (ahora nombra al estudiante); la libreta del app tiene etiquetas accesibles.

## Doble check, hora de visto y actividad del chat — incremento vigente

- Solicitud del propietario: entrega/lectura tipo WhatsApp y estado en línea/activo. Rama `feat/chat-presence-receipts`, sobre `main` `f84af4d`, conservando el incremento de uso real. Web y app muestran ✓ Enviado, ✓✓ Entregado y ✓✓ azul Visto a las…, con fechas completas en ayuda contextual y primera lectura inmutable.
- Entrega por confirmación explícita del destinatario tras recibir su resumen, lectura solo al llegar al final de la conversación visible. Actividad real de primer plano, pulso cada 10 s y caducidad 45 s; cierre/bloqueo de cada sesión, protección contra peticiones fuera de orden y varias sesiones simultáneas. Últ. vez y reloj corregido por servidor; presencia expuesta solo dentro de conversaciones autorizadas.
- Migración aditiva `20261107000825_ChatReceiptsAndPresence`: recibos durables y presencia acotada por cuenta/sello. Conserva lecturas antiguas sin inventar fechas. Aplicada solo en fixtures, pendiente actualización normal de la instalación. ADR-016 y guía `CHAT_Y_HUELLA.md` actualizados; evidencia y resultados en `QA_CHAT_ESTADOS_2026-11-06.md`.
- Validación confirmada: 141 .NET/SQL, 94 Flutter, 85 E2E web + 2 de migración MariaDB/SQL aislados (resuelven dos omisiones ordinarias; rendimiento móvil se mide en escritorio) y dos recorridos específicos adicionales de chat, análisis limpio, Angular/APK/Release correctos y modelo School sin diferencias. Integrado en `main` por solicitud vigente de merge/push. Sin datos reales, despliegue o cambios del legado.

## Prueba de uso real (integrada en main)

- Colegio ficticio de tamaño real usado por seis roles. Se corrigieron lentitud con volumen, homónimos en búsquedas, cierre de caja, exportación de solvencia, vista de notas y saldo total para familias y menú del docente con muchos cursos. Ocho mejoras quedan recomendadas en `QA_USO_REAL_2026-11-06.md`.

## Chat privado por cursos y desbloqueo biométrico — estado vigente

- Solicitud del propietario: publicar el merge en `main` y después añadir huella al app y chat tipo conversación privada. `ce563a7` se subió al iniciar; el alcance del chat se confirmó expresamente: solo docentes de los cursos del estudiante. Implementación en `feat/private-chat-biometric-unlock`, conservando la instalación independiente por colegio.
- Chat real en SQL, web `/workspace/messages` y app conectado: conversaciones de dos personas por ciclo/estudiante; estudiante o encargado autorizado ↔ docente asignado, sin chat entre estudiantes, entre estudiante/encargado o lectura general por un rol administrativo. Revalida cuentas/personas activas, vínculos, Communications y matrícula/ubicación/asignación efectiva en cada petición. Ciclo explícito, consulta de cierre e historial conservado. ADR-016.
- Icono/contador y aviso al ingresar, búsqueda/paginación, historial, burbujas, lectura confirmada, actualización automática y reintento idempotente. Conserva el borrador al cancelar salida o perder conexión; una respuesta perdida/tardía no bloquea el compositor ni borra el siguiente borrador. Móvil deja el compositor en pantalla; Axe y contraste revisados.
- Huella opcional desde el perfil Android/iOS: ticket Identity cifrado/protegido por biometría, sin contraseña, 14 días absolutos, misma validación de sello/MFA/permisos; bloqueo al perder primer plano y validación del servidor al restaurar. Cerrar sesión, cambiar cuenta o desactivar elimina el recordatorio; desactivar devuelve la sesión ordinaria. No crea tokens o permisos propios. ADR-017.
- Validación: **136 .NET/SQL**, **91 Flutter**, **85 E2E de suite completa + 2 de migración** aprobados; recorrido de chat repetido tras los ajustes finales y pruebas de respuesta perdida. Análisis Flutter limpio, builds Angular/Release y **APK debug** correctos, snapshot School sin diferencias, 7 + 4 escenarios PHP. Informe `QA_CHAT_BIOMETRIA_2026-11-06.md`; guía `CHAT_Y_HUELLA.md`.
- Nueva migración aditiva `20261106225356_PrivateChat`, aplicada solo a fixtures propios; pendiente actualización normal de la instalación antes de servir esta versión. Huella/Face ID físicos e iOS nativo pendientes de comprobación; no se declara distribución, push de sistema con app cerrado ni fase MOB completa. Sin datos personales reales, cambios del legado o despliegue.

## Integración en main y regresión — incremento previo

- Por solicitud del propietario se integra la rama de migración sobre el `main` que ya incluye primeros pasos, gestión académica simplificada y revisión visual web/app. Conflictos limitados a continuidad documental, conservando ambos incrementos. El asistente se abre desde `Migrar estudiantes.cmd` en la raíz de `main`; publicación local actualizada.
- Regresión del conjunto: **127 .NET/SQL**, **83 E2E web + 2 E2E de migración**, **74 Flutter**, todos aprobados; análisis Flutter sin observaciones, builds Angular/Flutter y Release correctos, snapshot School sin cambios pendientes, **7 + 4 escenarios PHP** correctos. De las tres omisiones de la suite web normal, dos se ejecutaron correctamente con MariaDB aislado; la restante es rendimiento medido una sola vez en escritorio.
- Colegio nuevo por seis perfiles: **49/49 pasos de escritorio y 49/49 de teléfono**, incluida app conectada; matrícula, familias, accesos, ciclo/períodos/cursos, caja, asistencia, tareas, notas, avisos y portería. Capturas/Axe y estados de error revisados; sin fallos funcionales ni problemas visuales detectados en el alcance ensayado. Informe `QA_MERGE_MAIN_2026-11-06.md`.
- Todo con datos sintéticos, sin cortar datos escolares, modificar el legado o desplegar. Recursos SQL propios retirados: 0 bases temporales nuevas/respaldos E2E restantes. La carga real y la comprobación de equipos físicos siguen siendo operaciones pendientes.

## Migración fácil: asistente local de tres pasos — incremento previo

- El propietario pidió eliminar la operación técnica manual. Nuevo acceso `Migrar estudiantes.cmd` → navegador local HTTPS con ingreso habitual → origen/ciclo → revisión por grupos/excepciones → importar. Guía corta `MIGRAR_EN_3_PASOS.md`. El modo de consola anterior se conserva como opción avanzada.
- Detecta configuración PHP literal sin ejecutarla/mostrar secretos; propone nombres/familias/códigos, muestra conteos por ciclo, guarda revisiones privadas por operador y permite grado/sección por grupo. Destaca nombres ambiguos y encargados repetidos para consolidación explícita. No decide promoción ni continuidad por notas. Borradores, claves e información escolar no se guardan en Git ni en almacenamiento del navegador.
- Prepara solo el esquema aditivo del padrón/rol SQL vacío cuando faltan, sin crear roles por nombre para autorizar. Modo `--migration-assistant` separado del host normal, solo loopback, `ActiveAccount` + tres permisos SQL + CSRF/MFA. Misma autenticación Identity/SignInFlow, cookie local aislada. Respaldo COPY_ONLY verificado automático antes de apply y conciliación después; fallo de respaldo impide la carga. Bloqueo privado de archivo y SQL para dos ventanas/procesos.
- Validación: **125 .NET/SQL aprobadas**, ocho específicas nuevas del asistente; **7 escenarios de extractor + 4 de detección PHP** correctos; recorrido real con MariaDB sintético y SQL E2E en escritorio/móvil, con Axe, capturas y cuentas pendientes. Guía/evidencia en `QA_MIGRATION_2027.md`; artefacto listo en `.local/migration-tool/`.
- Sigue la rama aislada `feat/legacy-migration-2027`; sin tocar el trabajo del otro agente, despliegue, lectura de personas reales del legado o carga escolar real. Solo se prepara la herramienta para que el propietario revise su padrón y confirme la carga desde la pantalla.

## Importador inicial 2027 listo en rama aislada — estado vigente

- El propietario pidió implementar y probar lo que antes era estudio. `--import-roster` v2 y wrappers PowerShell/PHP: extracción acotada de lectura, borrador con decisiones pendientes, plan sin escrituras, apply atómico y reconcile independiente. Guía `MIGRATION_2027.md`; ADR-015 y `QA_MIGRATION_2027.md`. El estudio original se conserva como `MIGRATION_2027_STUDY.md`.
- Personas/estudiantes/encargados, vínculos sin facultades, matrícula del GUID/ciclo/grado/sección/fecha revisados y cuentas pendientes (`IsActive=false`, sin contraseña, código o permisos administrativos). Una persona con dos perfiles comparte cuenta. Familias requieren decisión expresa; no se infiere el titular ni promoción. Activación individual por IAM existente; reintentos no duplican ni revierten activaciones posteriores.
- Identity y School comparten conexión/transacción serializable, bloqueo IAM y bloqueo de matrícula compartido con las escrituras web; cuida cupos concurrentes y rollback de cuentas/personas. Actor con contraseña/permisos/TOTP cuando corresponde; consola exige confirmar catálogo destino. Paquete estricto, huella de archivo/estado, resultados técnicos durables y alias con discriminador para separar adultos de una ficha familiar.
- Migración School `20261106202522_InitialRosterImport`, snapshot y delta SQL preparados; aplicada solo en bases propias del fixture, sin cambios de esquema/datos en la instalación escolar/desarrollo. Artefacto Release local `.local/migration-tool/`. Rama `feat/legacy-migration-2027`, worktree `.local/worktrees/legacy-migration-2027`; no se incorporaron cambios del otro agente en main.
- Validación: **117 .NET/SQL correctas, 0 omitidas**, incluyendo 16 nuevas (caso con 26 variantes inválidas, rollback, concurrencia, MFA real, CLI completa y 150 alumnos); **17 específicas posteriores correctas**, **7 escenarios PHP de contrato** y recorrido de extracción/borrador con MariaDB real aislado correctos. Publicación Release, sintaxis de scripts, enlaces y `git diff --check` correctos. Evidencia `.local/qa/test-results/`; ver QA para límites.
- No se conectó al MariaDB del legado ni se leyeron/importaron personas reales. Sin notas, pagos, deudas, tareas o contraseñas migradas; sin despliegue. Quedan configuración/backups, extracción/revisión del padrón real y su corte/conciliación a cargo del propietario. No se certifica un corte real ni ausencia absoluta de bugs.

## Estudio de migración inicial 2027 — rama aislada

- Solicitud del propietario: estudiar la mejor forma de migrar estudiantes y padres/encargados para empezar en 2027, con ciclo/grado/sección explícitos y acceso inactivo para activarlo individualmente; operación a cargo del propietario, sin notas ni historial.
- Rama `feat/legacy-migration-2027`, worktree `.local/worktrees/legacy-migration-2027`, base `12c9c0d`; no se trasladaron los cambios ajenos del working tree principal. Documentos: `MIGRATION_2027.md` y ADR-015 (estrategia propuesta, no implementada).
- Comparadas seis alternativas; recomendada consola con extracción de lectura, padrón revisado, plan, aplicación atómica y conciliación. Personas activas/matrículas 2027 separadas de cuentas pendientes (`IsActive=false`, sin contraseña/código). Vínculos sin facultades hasta verificación individual.
- Lectura estática del PHP/modelo actual: ficha antigua `parents` puede contener dos adultos; `state` es candidato de código institucional; copias por ciclo y grado destino requieren revisión. Importador actual no crea matrículas/cuentas y necesita contrato estricto, tratamiento de alias/familias, bloqueos y conciliación antes del corte.
- Preparados `migration-2027/extract-readonly.sql` (borrador, origen NULL por defecto) y `review.example.json` (solo sintético, contrato futuro no aceptado por el importador actual). Validado JSON: 5 personas, 3 estudiantes, 2 encargados, 3 vínculos sin facultades y 3 matrículas del mismo ciclo; acceso pendiente sin códigos. Revisión estática de 14 sentencias SQL de lectura/control transaccional y `git diff --check` correctos. Sin conexiones a MariaDB, exportaciones personales, aplicación SQL ni cambios de datos/PHP. No se declara terminada MIG-001 ni listo un corte.

## Prueba desde cero y primeros pasos (estado vigente)

- Un colegio vacío recorrió un año escolar completo por la interfaz (seis roles, web escritorio/teléfono y app): todo correcto. Inicio guía a un colegio nuevo con «Primeros pasos» hasta tener inscripciones y cargos.

## Gestión sencilla para Dirección y docentes (estado vigente)

- Dirección: «Ciclos y grados» muestra una guía «Preparar el ciclo» (períodos, secciones, cursos y docentes, inscripciones, inicio) con el atajo de cada paso. Los períodos se generan del mismo largo en una sola operación; secciones y cursos (con sus docentes vigentes) se copian del ciclo anterior. «Nuevo ciclo» propone nombre y fechas un año después y deja el ciclo listo de una vez. «Crear cursos» admite varios grados, un curso por sección y «Todas» las materias; filtro «Solo sin docente». Motivos sugeridos al abrir inscripciones, iniciar el ciclo y asignar docentes (editables y auditados).
- Docentes: barra común Lista · Asistencia · Actividades · Libreta en cada curso y selector «Cambiar de curso» que conserva la herramienta. En el teléfono, los cuatro estados de asistencia caben en una fila.
- App conectado: «Ciclo escolar» en la pestaña Colegio (misma guía y atajos, nuevo ciclo, avanzar estado); tarjetas de curso con «Asistencia de hoy pendiente», entregas por revisar y botones directos a Asistencia y Libreta, también en Inicio; cambio de curso sin salir.

## Campana de notificaciones (estado vigente)

- La campana del encabezado muestra avisos sin leer y pendientes por rol con las mismas reglas del panel; abre cada destino y marca avisos como leídos. También en el app conectado. Recorrido de 8 cuentas y 74 pantallas sin errores ni desbordes.

## App Flutter con diseño de la web y cuenta real (estado vigente)

- Mismo diseño visual que la web y nuevo modo conectado (ADR-014): ingreso con la cuenta del colegio y MFA, panel por permisos, avisos, ficha de estudiante/familia (notas con solvencia, tareas con entrega, asistencia, portería, estado de cuenta), libreta y asistencia docente, directorio y caja con registro de pago. El servidor autoriza todo; el modo demo queda separado.
- 101 pruebas .NET/SQL y 72 Flutter aprobadas; humo real con 5 cuentas ficticias correcto. Falta Playwright web (host ajeno bloqueaba `bin`), notificaciones, archivos y pruebas en dispositivos físicos. No se declara terminada la fase MOB.

## Roles, módulos y flujos completos (estado vigente)

- El propietario pidió quitar el aspecto de página enmarcada, quitar el logo grande de la activación y recorrer todos los flujos por perfil escolar (estudiante, docente, familia) y por roles creados (Dirección con 15 permisos, Subdirección con 10, Secretaría y Caja), sin cambiar la paleta, documentando cada prueba en HTML. Informe: `QA_ROLES_FLUJOS_2026-11-06.md`, incidencias RF-001 a RF-006.
- Pantalla completa en sitio público, ingreso/activación y espacio privado; logo retirado de Activate/LoginWith2fa; menú personal Docencia/Mis estudiantes; saludo sin cortes y selector de ciclo sin recorte; aviso de cupo que se limpia al cambiar sección; migración School `ReceiptNumbersNoCache` (secuencia de recibos sin caché, aplicada en desarrollo).
- Recorrido Playwright de la instalación de desarrollo con datos ficticios: 41 pasos, 32 correctos, 9 informativos (datos de corridas previas), 0 errores. Matrices rol × pantalla y rol × API, comunicados, captura de nota, portería con lectura repetida, caja con recibo PDF y móvil 390 px. Reporte HTML autocontenido en `.local/qa/reporte/` (ignorado).
- Validación: **98 .NET/SQL aprobadas**, 0 omitidas; build Angular correcto; suite Playwright completa **81 E2E aprobados + 1 omisión prevista** (82 casos) tras ajustar `finance.spec.ts` y `grades.spec.ts` para buscar el enlace del estudiante dentro de `#content` (el menú Mis estudiantes ahora repite ese enlace). `git diff --check` correcto. Quedan 7 bases `SchoolPlatform_Test_*` del 5/11 23:35 de una corrida interrumpida; no se borraron por no poder confirmar su origen.

## Rediseño según las capturas del propietario — estado vigente

- Se sustituyó la dirección visual anterior por las capturas pública/estudiantil y el paquete HTML/CSS aportado: carbón/dorado, marfil, Inter normal/cursiva autoalojada, imágenes originales, portada curva, menú/cabecera oscuros, cápsula seleccionada y tarjetas. Mismo sistema en ingreso Razor, directorios y módulos. Ver ADR-013 y `QA_REFERENCIA_2026-11-05.md`.
- Página pública ampliada con misión/visión, vida escolar, admisión, comunidad, preguntas y contacto; cada entrada del menú tiene destino real, con desplazamiento bajo la cabecera y cierre móvil. Todos los contenidos institucionales, recursos y colores permanecen en `institution.json`/`InstitutionOptions`; no hay editor de marca. La redacción pública nueva fue autorizada por el propietario y no se trata como una política oficial investigada.
- Panel con composición de referencia y datos reales: cursos propios, tareas, calendario por fecha, comunicados y acciones funcionales. Conserva los resúmenes de administración/docencia/familia, ciclo explícito y permisos por recurso. No copia promedio, horario ni Recursos ficticios. Curso propio/familiar se obtiene solo con acceso académico; revocar la facultad retira cursos/tareas. La sesión añade contexto visual del perfil, sin conceder permisos por nombre.
- **89 .NET/SQL aprobadas**, 0 omitidas; build Angular correcto. **81 E2E aprobados y 1 omisión prevista**, 0 fallos, corrida completa de 82 casos; **18 adicionales aprobados** tras corregir texto sobre la curva en menú/ingreso. Axe, capturas y ausencia de desbordamiento revisados en escritorio/móvil y 320/390/900/1440/1920 px; recorridos operativos también 768/1024 px. Advertencia conocida SweetAlert2 CommonJS.
- Evidencia preservada en `.local/qa/evidencia/reference-redesign-2026-11-05/`: 159 PNG de suite completa y 58 finales, más accesos directos de portada/panel. Originales del kit y copias coinciden por SHA-256. SQL final: **0 bases temporales**. `git diff --check` correcto. Host final `http://localhost:5082`, raíz/liveness/readiness 200, metadatos `.local/qa/reference-design-host.json`, lanzador 3372.
- Incremento en working tree sobre `434a824`, **sin commit/push ni despliegue**. Sin migraciones, importación, cambios de datos escolares/PHP/Numix ni Flutter. Los pendientes anteriores de operación, políticas, hardware y licencias se conservan.

## Consolidación de QA y experiencia en Git

El propietario solicitó commit y push de todo lo pendiente. Este commit reúne las dos revisiones de flujos y sus mejoras de experiencia, pruebas y documentación. Las referencias posteriores a cambios sin commit describen el cierre de esas revisiones. Se conserva la validación documentada (89 .NET/SQL, 68 Flutter, 75 E2E y 6 adicionales); se comprobó `git diff --check` sin repetir suites. Evidencias y credenciales locales permanecen ignoradas. No incluye despliegue.

## Revisión completa y experiencia de uso (estado vigente)

- El propietario pidió nuevamente probar todos los flujos y mejorar la experiencia. Informe y matriz de cobertura: `QA_EXPERIENCIA_2026-11-05.md`. Las muestras y pendientes físicos/móviles no se consideran módulos terminados.
- Asistencia: protección de marcas/observaciones al cambiar día/curso o salir; espera el guardado en curso; cancelación restaura la fecha visible. Fecha explícita en URL, historial, reintento y rechazo de respuestas anteriores. Estados Sin registrar/Registro parcial/Registrada/Cambios sin guardar; totales del borrador, errores junto al formulario y guardado solo cuando hace falta. Los botones avanzan un día calendario sin inventar calendario lectivo.
- Inscripciones: ciclo y filtros completos en URL/historial; un error retira matrícula y acciones anteriores, permite reintentar y no sustituye ciclos desconocidos. Al vencer una sesión, la vuelta desde ingreso conserva ciclo/filtros/fragmento. Caja explica búsquedas vacías y descarta resultados cuyo término cambió; matrícula orienta a limpiar filtros.
- **89 pruebas .NET aprobadas con SQL real**, 0 omisiones; **68 Flutter aprobadas**, análisis estático sin observaciones con SDK fijado; build Angular correcto. **75 E2E aprobados y 1 omisión prevista de rendimiento móvil**, 0 fallos, corrida completa de 76 casos (12 nuevos). **6 adicionales aprobados** tras el último ajuste visual de asistencia, con Axe y capturas revisadas en escritorio/móvil y 320/768/1920 px. Advertencia conocida SweetAlert2 CommonJS.
- Evidencias: `.local/qa/evidencia/experience-2026-11-05/` (133 PNG, secretos MFA enmascarados). SQL final: **0 bases temporales Test/E2E**; `git diff --check` correcto. Sin cambios backend/migraciones, datos de desarrollo o legado. Incremento y QA previo en el working tree sobre `ed6022f`, sin despliegue. Host actualizado `http://localhost:5082`, proceso/logs en `.local/qa/experience-host.json` y `experience-5082*.log`.

## QA continuada de flujos (estado vigente)

- El propietario pidió seguir probando y buscar bugs, y reiteró continuar durante la revisión. Se reprodujeron y corrigieron ocho incidencias documentadas en `QA_FLUJOS_2026-11-05.md`: historial/errores/respuestas tardías de Caja, error del resumen de cobros, pérdida de notas al navegar tras fallar el autoguardado, contexto y errores de ciclos financieros, y color de confirmaciones destructivas.
- Caja limpia el estado del estudiante anterior, sigue la URL y ofrece reintentos; el fallo de recibos no se presenta como ausencia de pagos. La libreta espera guardados pendientes y cambios escritos durante ellos; los cambios que no se guardaron requieren confirmar su descarte. El historial y la recuperación no mezclan períodos.
- Configuración financiera y Solvencia siguen el ciclo y corte explícitos, ignoran respuestas anteriores y retiran datos ante errores. Un ciclo inexistente o corte de otro ciclo no se sustituye por otro. Los filtros de solvencia se conservan en URL/historial. La creación de una cuota tras Atrás se comprobó contra el ciclo del comando y SQL.
- Recorrido financiero ampliado: reversión → deuda/solvencia familiar actualizadas → rechazo de ajuste excesivo → descuento válido → pago del remanente; se conserva el recibo revertido. Roles espera que termine la actualización visual de la selección; el resumen de recibos no supone una base vacía después de otras pruebas.
- MFA: trazas del recorrido desactivadas, QR/clave/códigos enmascarados en capturas. Evidencias locales en `.local/qa/evidencia/flows-2026-11-05/`; revisión de escritorio/móvil y Axe. Sin migración ni modificación de datos escolares/PHP/Numix.
- Validación de cierre: **63 E2E aprobados, 1 omisión prevista de rendimiento móvil y 0 fallos**, en corrida completa de 64 casos; **4 adicionales aprobados** tras corregir el color destructivo. Se añadieron 24 casos de regresión. Build Angular correcto, advertencia conocida SweetAlert2 CommonJS; Axe y capturas de escritorio/móvil revisadas. SQL final: **0 bases temporales Test/E2E**. `git diff --check` correcto. Cambios QA en el working tree sobre `ed6022f`, sin migraciones. Host actualizado en `http://localhost:5082`, datos del proceso en `.local/qa/flows-host.json`.

## Dashboard operativo y roles configurables (estado vigente)

- Rediseño anterior versionado y enviado a `origin/main` como `2f70635` por solicitud del propietario. Este incremento sustituye «Tus módulos» por datos y acciones operativas; ver ADR-011.
- Dashboard con ciclo explícito en URL, actualización/hora de lectura, cobrado hoy por aplicaciones, saldos por vencimiento, siete días de cobros y recibos, matrícula por grado, cursos sin docente, registros/publicación por período y portería de hoy con alcance indicado. Los errores se muestran con reintento, sin ceros ficticios. No convierte falta de entrada en ausencia ni saldo vacío en solvencia.
- Inicio docente con entregas por revisar, vencimientos próximos y registros de asistencia de sus cursos. Inicio familiar/estudiantil con tareas y seguimiento por recurso; el saldo solo para encargados con facultad financiera. Sin valores de notas en el resumen ni nuevas políticas académicas.
- **Roles de acceso configurables en SQL**, con módulo `/workspace/roles`, creación/edición, permisos por área, selección completa y asignación desde Cuentas. Director y Secretaría pueden tener todos los permisos; el nombre no fija el acceso. Los perfiles escolares de estudiante, docente y padre/encargado se conservan separados, según aclaración del propietario. Ver ADR-012.
- Eliminado `AccessProfiles` como catálogo en tiempo de ejecución. El catálogo de acciones protegidas permanece en el servidor; roles, composición y asignación se guardan en SQL. La proyección efectiva, auditoría y cierre de sesiones se actualizan transaccionalmente; protección del último gestor, rowversion, MFA y desactivación solo tras reasignar cuentas.
- Migración Identity `20261106003523_ConfigurableAccessRoles` aplicada a `SchoolPlatform_Development`. **Hash SHA-256 de los permisos efectivos antes/después idéntico**, y comparación entre roles/asignaciones y proyección con **0 diferencias**. Los accesos no coincidentes se preservan en roles propios, sin ampliación. No se importaron datos ni se tocó PHP/Numix.
- Validación final: **89 pruebas .NET aprobadas, 0 omitidas** (SQL Server real); build Angular correcto; **39 E2E aprobados y 1 omitido previsto** (rendimiento móvil), con Axe y revisión de escritorio/móvil. Dashboard comprobado en 320/768/1024/1920 px, incluyendo error y reintento, cambio/navegación de ciclo y datos reales preparados por el harness. Roles comprobados con todos los permisos y edición de un rol asignado.
- Capturas revisadas y conservadas en `.local/qa/evidencia/dashboard-roles-2026-11-05/`. Sin bases temporales de pruebas residuales; `git diff --check` correcto. Advertencia conocida del build: SweetAlert2 CommonJS.
- Revisión visual adicional: corregida la distribución del botón de pendientes en 320 px y agregados importes diarios desplegables para evitar cifras diminutas. **4 E2E adicionales aprobados** tras ese ajuste, con Axe y verificación de lectura en móvil.
- Host local en `http://localhost:5082`, lanzador/puerto en `.local/qa/dashboard-roles-host.json`, logs `.local/qa/dashboard-roles-5082*.log`. Los hosts anteriores del proyecto se detuvieron para compilar. No hubo despliegue. Se versiona y envía este incremento por la solicitud vigente; licencias, políticas, hardware y Flutter mantienen sus pendientes anteriores.

## Rediseño web y navegación escolar (estado vigente)

El propietario solicitó commit y push de este rediseño antes del siguiente ajuste del dashboard. Las menciones a «sin commit» de esta sección describen el cierre previo.

- Por solicitud del propietario se revisaron Numix (sitio público, dashboard y productos) y su código local como referencia de claridad visual. No se modificó aquel proyecto ni el PHP.
- Nueva interfaz compartida: Figtree, menú sólido compacto, encabezado blanco, paneles con borde fino, acciones y filtros legibles y menos decoración. Una marca por cabecera/menú; sin escudo gigante, logo duplicado en login ni instrucciones técnicas en la página pública. Ver `DESIGN.md` y ADR-010.
- Módulos independientes en el menú: **Estudiantes, Padres y Docentes**, con directorios propios filtrados por servidor, perfiles preseleccionados al registrar y pestaña Todos para reutilizar fichas. Docentes incluye al personal y lo indica; asignaciones de cursos y permisos siguen separados.
- Registro de personal en un paso: cargo opcional en el contrato de alta, validación y persistencia de persona/perfil/auditoría en un mismo guardado. Sin migración de esquema, nuevas cuentas, vínculos o matrícula implícitos. Formularios muestran primero nombres, apellidos y código/cargo; contacto y otros perfiles se despliegan si se necesitan.
- Portería y Administración se pliegan fuera de su ruta. Menú móvil conserva foco, Escape y fondo inerte. Página pública con presentación configurada, información del portal, servicios reales, contacto y mapa voluntario. Flutter no cambió.
- Validación: **81 pruebas .NET aprobadas, 0 omitidas**, con SQL Server real; build Angular correcto; **33 E2E aprobados y 1 omitido previsto** (rendimiento móvil), más **14 E2E adicionales aprobados** tras simplificar el formulario y ampliar la revisión pública a 320/768/1920 px. Los directorios/formularios también se comprobaron en 320/768/1024/1920 px, además de escritorio y móvil habituales, con Axe y sin desbordamiento horizontal.
- QA detectó y corrigió contraste del pie público, transición de cierre de alertas y una fecha UTC usada por la prueba de notas para pagos: las pruebas usan ahora la fecha civil configurada del colegio. Capturas revisadas y conservadas en `.local/qa/evidencia/redesign-2026-11-05/`; `git diff --check` correcto. Advertencia conocida del build: SweetAlert2 CommonJS.
- Rediseño abierto y verificado en `http://localhost:5082` (proceso lanzador 9816; comprobar disponibilidad en otra sesión). El puerto 5080 ya estaba ocupado por otro host del proyecto y se conservó. Ingreso revisado nuevamente en el navegador tras ajustar la separación de su encabezado. No se desplegó ni se creó commit de este rediseño. Siguen pendientes las políticas, licencias, validaciones físicas y operación indicadas en los incrementos anteriores.

## Consolidación en Git (5 de noviembre de 2026)

Por solicitud explícita del propietario, este commit reúne todos los cambios pendientes, incluidos los incrementos décimo y undécimo y las correcciones del recorrido QA. Se solicitó también su push a `origin/main`. Las menciones históricas a «sin commit» describen el cierre de aquellas sesiones. Los archivos locales, credenciales, respaldos y evidencias ignoradas permanecen fuera de Git. En esta consolidación se comprobó `git diff --check`; no se repitieron las suites y se conservan sus resultados previos documentados. Los pendientes físicos, de licencia, políticas y producción siguen vigentes.

## Alcance vigente

**Un proyecto y una base independientes por colegio.** El propietario aclaró expresamente que copiará este proyecto para otro colegio y cambiará su nombre/configuración. No construir SaaS, selección de clientes, tenants ni membresías entre instituciones. La marca está en un único `config/institution.json`, editable por el desarrollador, sin panel administrativo.

## Implementado

### Undécimo incremento: libreta ágil, rendimiento, PDFs, carnets y portería (estado vigente)

- Libreta con navegación Enter/↓, pegado desde Excel (E/P), autoguardado con rowversion y correcciones publicadas con motivo.
- Bundles Brotli/Gzip e inmutables, API comprimida, sesión breve en guards y precarga. Medición local final: páginas 101–145 ms; API más lenta observada 11 ms. Sin Redis, solvencia y permisos se verifican en servidor.
- PDFs reales: recibo interno/familiar (sin usuario emisor en familia), notas familiares con solvencia, libreta docente e informes de período publicado por grado/sección con retención. QuestPDF Community: elegibilidad del colegio pendiente de confirmación; ver ADR-009.
- Carnets CR80 con QR/Code 128, impresión individual y por grado/sección (tarjeta/carta), foto privada opcional, reemplazo transaccional y revocación del carnet anterior. **Imprimir conserva el carnet activo; reemplazar lo invalida.**
- Portería persistente y UI Angular: lector USB/teclado, modos, duplicados concurrentes, registro manual, reporte diario/historial, anulaciones con motivo, registro/revocación de teléfonos y consulta familiar por facultad académica. `attendance.gate/read/manage` en SQL, más perfiles Portería y Control de asistencia.
- Flutter: lector `/gate-reader` conectado a la API con credencial de dispositivo (QR, HTTPS, cámara/entrada manual, almacenamiento seguro nativo y revocación). Los demás módulos siguen siendo prototipo ficticio. No se declara terminada la integración móvil.
- `StudentCardsAndGate` aplicada y verificada en `SchoolPlatform_Development`; datos sintéticos previos conservados. Ver ADR-009 y `GATE.md`.

Validación del servidor: **79 pruebas .NET correctas, 0 omitidas** (SQL Server real). Build Angular correcto. **31 E2E correctos y 1 omitido previsto** (rendimiento móvil, medido en escritorio); recorridos nuevos con Axe. Flutter: **68 pruebas correctas**, análisis sin observaciones y build web correctos, incluida navegación/emparejamiento/revocación del lector con dobles HTTP. Revisión de PDF renderizado y capturas de escritorio/móvil; pendiente la impresión y cámara físicas. Sin commit, PR, publicación ni datos del legado.

### Décimo incremento: avisos, MFA, archivos privados, operación y pulido (estado más reciente)

- **Avisos internos** con audiencia por colegio/grado/curso, facultad `Communications` y lecturas (`COMMUNICATION.md`).
- **MFA TOTP** con QR, códigos de respaldo, desafío en el ingreso, restablecimiento por gestor y exigencia configurable por permiso. Se corrigió un caso en que una cuenta marcada con MFA sin autenticador entraba solo con contraseña.
- **Adjuntos privados** en entregas, validados por firma, fuera de la web, con descarga autorizada por recurso.
- **Operación**: `/health/ready`, `Backup-Database.ps1 -VerifyRestore` (probado contra la base de desarrollo) y workflow de CI (sin ejecutar aún). Ver `OPERATIONS.md`.
- **Importador MIG-001a** (plan/apply desde staging revisado, idempotente, sin facultades ni cuentas). Ver `MIGRATION.md`.
- **UX**: auditoría con etiquetas legibles y filtro por área, plurales correctos, nombre de ciclo sin duplicar «Ciclo», cierre de sesión en la barra lateral, avatar hacia «Mi seguridad», resumen de validación vacío oculto en las páginas de acceso.

Validación: **71 pruebas .NET (57 SQL) y 28 E2E** correctas en escritorio y móvil con Axe, sin errores del servidor. Migraciones `CommunicationAnnouncements` y `SubmissionFiles` aplicadas a `SchoolPlatform_Development`.


### Noveno incremento: asistencia (estado más reciente)

- Esquema `attendance`: sesión por curso y fecha y registro por estudiante; UI docente y resumen en el portal. Ver `ATTENDANCE.md`.
- Validación: **62 pruebas .NET (49 SQL) y 24 E2E** correctas, sin errores del servidor; migración `AttendanceSessions` aplicada a `SchoolPlatform_Development`.


### Pulido transversal (estado más reciente)

- Inicio con saludo por nombre de la persona vinculada, accesos rápidos según permisos y mensaje claro para cuentas sin servicios; navegación lateral agrupada (Comunidad y académico, Finanzas, Administración) con desplazamiento; avatar con la inicial del usuario.
- Revisión visual de escritorio y móvil de todos los flujos; recibo en móvil, tabla de directorio apilada, foco tras diálogos y enlace de salto corregidos.
- Validación final: **60 pruebas .NET (47 SQL) y 24 E2E** correctas, `dotnet restore --locked-mode` y `git diff --check` sin errores; ninguna base `SchoolPlatform_Test_*`/`E2E_*` residual.


### Octavo incremento: tareas y entregas (estado más reciente)

- Esquema `learning`: actividades por curso con audiencia derivada de las ubicaciones fechadas y entregas de texto del propio estudiante, tardías marcadas y retroalimentación. Ver `LEARNING.md`.
- UI docente `/workspace/courses/{id}/assignments` y sección «Tareas» en el portal del estudiante/encargado.

Validación: **60 pruebas .NET (47 SQL) y 24 E2E** correctas, sin errores del servidor. Migración `LearningAssignments` aplicada a `SchoolPlatform_Development`.


### Séptimo incremento: libreta, publicación y consulta de notas (estado más reciente)

- Esquema `assessment`: notas de período por curso con estados y revisiones, y publicaciones por período ligadas a un corte de solvencia. Ver ADR-007.
- Libreta docente `/workspace/courses/{id}/grades`, publicación `/workspace/grades?year=…` y sección «Calificaciones» en `/workspace/my/students/{id}` con aviso de bloqueo sin datos de notas.
- `SqlTestBase` ahora falla si el servidor registró una excepción no controlada durante la prueba; así se detectó una consulta EF no traducible y que los 400 por JSON inválido se registraban como error.

Validación: **58 pruebas .NET (45 SQL) y 22 E2E** correctas, sin errores del servidor. Migración `GradesAndPublications` aplicada a `SchoolPlatform_Development`.


### Sexto incremento: materias, cursos y docentes (estado más reciente)

- Perfil de personal (`people.Staff`), materias, cursos por grado o sección, asignaciones docentes fechadas y lista del curso derivada de las ubicaciones vigentes en una fecha. Ver `COURSES.md`.
- Autorización por recurso para docentes: solo cursos con asignación no finalizada; 404 para otros.
- UI `/workspace/courses`, `/workspace/courses/{id}` y «Mis cursos» en `/workspace`.

Validación: **55 pruebas .NET (42 SQL) y 20 E2E** correctas, sin errores del servidor durante E2E. Migración `StaffCoursesAndTeaching` aplicada a `SchoolPlatform_Development`.


### Quinto incremento: finanzas y solvencia (estado más reciente)

- Esquema `finance`: conceptos, calendario de cuotas por ciclo, cortes de solvencia, cargos con clave de obligación única, ajustes (beca/descuento/exoneración), pagos con aplicaciones exactas, recibos numerados por secuencia con snapshot JSON y copia del logo por SHA-256, reversiones. Ver `BILLING_AND_CLEARANCE.md` y ADR-006.
- Emisión masiva con vista previa en servidor, exclusiones y confirmación por hash. Idempotencia de pagos por clave de formulario. Bloqueo SQL de aplicación para dinero.
- `ClearanceService`: `Clear/Blocked/PendingFinancialSetup/NotEnrolled` desde datos confirmados; reportes por corte.
- UI: caja/estado de cuenta, configuración y emisión, solvencia, recibo imprimible y vista del encargado con facultad `Finance` (`/workspace/my/students/{id}`).
- Robustez: peticiones abortadas por el cliente ya no se registran como error del servidor; ruido de advertencias antiforgery silenciado.

Validación: **53 pruebas .NET (40 con SQL Server) y 18 E2E** correctas; revisión de capturas de escritorio/móvil (se corrigió el recibo en móvil). Migración `FinanceAndClearance` aplicada a `SchoolPlatform_Development`.


### Cuarto incremento: ciclos, grados, secciones e inscripciones (estado más reciente)

- Esquema `academic` en `SchoolDbContext`: ciclos con estados solo hacia adelante y predeterminado único, períodos con nombre del término institucional, catálogo de grados, secciones opcionales con cupo/jornada, inscripción canónica por estudiante y ciclo, y ubicaciones con vigencia `[inicio, fin)`. Ver `ENROLLMENT.md` y ADR-005.
- Permisos `academic.read`, `academic.manage`, `enrollment.manage`; perfiles Secretaría (con inscripciones), Coordinación académica y Administración institucional.
- UI `/workspace/academic` y `/workspace/enrollment?year=…` (ciclo en URL), línea de tiempo de ubicaciones en la ficha y ubicación vigente en `/workspace` para el estudiante o el encargado con facultad académica.
- `BadRequestExceptionHandler`: un cuerpo JSON inválido devolvía 500; ahora 400 `invalid-request` sin detalles internos. Validación previa de fechas en los diálogos.
- Playwright muestra la salida del servidor (`stdout: 'pipe'`) para diagnosticar 500; así se encontró el fallo anterior.

Validación: **48 pruebas .NET correctas (35 con SQL Server), 0 omitidas; 16 E2E correctas** y además **32/32 con `--repeat-each 2`** (escritorio/móvil, Axe). Migración nueva `AcademicStructureAndEnrollment` (School), aplicada a `SchoolPlatform_Development` con `dotnet ef database update -c SchoolDbContext`.


### Tercer incremento: personas, estudiantes, encargados y facultades (estado más reciente)

- `SchoolDbContext` con esquema `people`: persona estable, contactos, perfiles de estudiante (código único) y encargado, vínculos con parentesco/inicio/finalización, facultades con historial y enlace cuenta↔persona. Ver `PEOPLE_AND_FAMILIES.md` y ADR-004.
- Permisos `people.read` y `people.manage` y perfiles «Consulta de personas», «Secretaría» y «Administración institucional». Vincular cuenta exige además `accounts.manage`. Perfil no coincidente se muestra como personalizado.
- Auditoría en el mismo `SaveChanges` que cada cambio; nueva columna `audit.Events.TargetType` y etiquetas en `/workspace/audit`. Sin nombres de personas en auditoría.
- Autoservicio `/api/v1/me` y `/api/v1/me/students/{id}`: acceso calculado en SQL por cuenta vinculada y vínculo vigente; 404 sin acceso; revocación inmediata.
- UI: `/workspace/people` (filtros, alta con detección de duplicados, filas apiladas en móvil), `/workspace/people/{id}` (datos, contactos, perfiles, vínculos activos/finalizados, facultades, cuenta, archivo) y ficha/estudiantes a cargo en `/workspace`.
- Corrección de raíz: la conexión EF se resolvía antes de que los fixtures la sustituyeran. Ahora se resuelve desde la configuración final y `MigrateOwnedDatabaseAsync` verifica el catálogo aleatorio antes de migrar.

Validación: **41 pruebas .NET correctas (28 con SQL Server), 0 omitidas; 14 E2E correctas** (escritorio/móvil, Axe sin infracciones en directorio, diálogos, ficha y vista del encargado). Build Angular de producción correcto. Capturas revisadas: se corrigieron tabla móvil cortada y pérdida de foco al cerrar diálogos.

Migraciones nuevas: `AuditTargetType` (Identity) y `InitialPeople` (School). **Ambas quedaron aplicadas en `SchoolPlatform_Development`** (ver incidente).

**Incidente a resolver por el propietario:** antes de la corrección anterior, una corrida de `PeopleSqlTests` usó `SchoolPlatform_Development` (la guarda de borrado impidió eliminarla). Quedaron datos **sintéticos**: cuentas `synthetic-owner` (gestor activo), `synthetic-reader`, `synthetic-registrar`, `synthetic-target`, cuya contraseña de prueba está en el código de pruebas; dos personas sintéticas y 12 eventos de auditoría (append-only). Se intentó una limpieza dirigida por ID y la herramienta la denegó; no se forzó. Antes de usar esa base: eliminar/suspender esas cuentas o recrear la base de desarrollo según decidas. Mientras exista `synthetic-owner`, `-CreateOperator` rechazará crear el primer gestor. No había datos reales ni cuentas previas (0 cuentas antes).


### Segundo incremento: acceso persistente (estado más reciente)

- Permisos SQL `accounts.manage` y `audit.read`, perfiles de cuenta y autorización por acción/recurso de gestión; comprobación repetida dentro de la transacción.
- Pantallas `/workspace/access` y `/workspace/audit` con datos reales de la base de esta instalación, búsqueda, paginación, estados, errores y control de versiones.
- Invitaciones, activación y recuperación sin correo obligatorio. Códigos de un uso con hash, vencimiento, revocación, entrega manual verificada y formulario POST; ningún código en URLs/listados/logs.
- Cambio de perfil, suspensión/habilitación y cierre de sesiones. La cookie compara el sello de seguridad directamente por petición. Recuperar contraseña invalida cookies previas y limpia el bloqueo por intentos.
- Protección del último gestor activo, también ante cambios simultáneos. Canje concurrente con un solo éxito, usando transacción y bloqueo de aplicación SQL.
- Auditoría durable, confirmada junto con el cambio; protegida contra UPDATE/DELETE en EF y mediante trigger SQL. Se auditan también ingreso/salida y fallos de acceso, sin atribuir un fallo al titular como actor.
- Preparación del primer gestor por consola; sin cuenta o contraseña predeterminada. `--provision-owner` exige base ya migrada y no inicia el host.
- Formularios de acceso con límite por IP/tamaño, errores sin reinsertar secretos y prevención de omitir MFA en cuentas marcadas con segundo factor.
- Harness de navegador con base temporal propia, propietario sintético y limpieza al finalizar. No utiliza la base escolar.

Validación del segundo incremento: **35 pruebas .NET correctas (22 con SQL Server), 0 omitidas; 12 E2E correctas en escritorio y móvil**, con Axe en las pantallas revisadas. Build Angular de producción y restore con lock correctos. La revisión móvil detectó y corrigió el desbordamiento por etiquetas ocultas dentro de una tabla y la posición del diálogo.

En `SchoolPlatform_Development` se verificaron **3 migraciones aplicadas y 0 cuentas**. Al finalizar los E2E no quedaban bases `SchoolPlatform_E2E_*`. No se creó un gestor local automáticamente: el propietario elige su usuario/contraseña mediante el script interactivo.

El servidor local del primer incremento fue detenido para compilar. Al preparar este handoff **no quedó iniciado nuevamente**. Arrancarlo con `./scripts/Start-Development.ps1 -SkipBuild` después de comprobar que 5080 está libre. `.local/publish` contiene el artefacto del primer incremento; no se volvió a publicar el segundo. El código fuente y `wwwroot/app` sí corresponden al segundo build probado.

### Base del primer incremento conservada

- Solución .NET 10 con SDK estable fijado, paquetes lock y advertencias como errores; web Angular 22 con TypeScript/templates estrictos y dependencias fijadas.
- Configuración institucional tipada/validada: nombre, sede, lema, contactos, dirección, cinco recursos gráficos independientes, tema, región, términos y campos documentales privados. Contactos desconocidos vacíos.
- Misma marca en Angular y Razor; proyección pública explícita y archivos fuera del directorio web. Ningún nombre Leadership/Mixco en componentes, plantillas o lógica; solo en la configuración y recursos de esta institución.
- Bienvenida, login/logout real, cookies/CSRF, bloqueo de cinco intentos y política de cuenta activa, incluida desactivación con una cookie existente.
- EF Core/SQL Server, migraciones y comandos separados de preparación local. Base creada `SchoolPlatform_Development`; sin usuarios precargados ni contraseña predeterminada.
- `/workspace` protegido con estado inicial honesto; no simula módulos escolares.
- Muestra visual `/preview` y directorio sintético con búsqueda/filtros; API de muestra únicamente en Development.
- Responsive, contraste corregido, tabla desplazable accesible por teclado y menú móvil con foco, Escape y fondo inerte.
- Cálculo académico puro por materia: cuatro bimestres, pendiente distinto de cero, mínima 60 y recuperación que conserva ordinaria/reemplaza efectiva. Sin publicación ni política pendiente activada.
- Documentación, ADR, scripts de ejecución/verificación, fuentes originales copiadas y handoff.

## Validación del primer incremento (histórica)

| Comprobación | Resultado |
| --- | --- |
| `dotnet test --verbosity quiet` | 18 correctas, 0 omitidas; incluye 5 pruebas con SQL Server real |
| `Invoke-Web.ps1 run build` | Correcto con modo producción de Angular y tipos estrictos |
| `Invoke-Web.ps1 test` | 8 correctas: escritorio 1440 y móvil 390, navegación, filtros, ingreso, fallos y teclado |
| Axe en bienvenida/login/muestra/directorio/error | Sin infracciones detectadas en etiquetas WCAG 2 A/AA y 2.1 AA evaluadas; no equivale a certificación completa |
| Revisión visual PNG | Bienvenida, ingreso, panel y directorio revisados en las capturas; pantallas sin estilos detectadas y corregidas antes del cierre |
| `dotnet restore --locked-mode` | Correcto |
| `dotnet list package --vulnerable --include-transitive` | Sin vulnerabilidades reportadas por las fuentes consultadas |
| Instalación npm | Sin vulnerabilidades reportadas en la instalación verificada |
| `dotnet publish … -c Release -o .local/publish` | Correcto |
| Smoke del artefacto publicado en 5086 | HTML y JavaScript 200; marca correcta y hash del bundle igual al build actual. Proceso de prueba detenido |
| `git diff --check` | Sin errores de whitespace; Git avisa conversión LF/CRLF según configuración local |

Las bases aleatorias de integración se crearon y eliminaron mediante el fixture. En la base de desarrollo se comprobó `AspNetUsers` con cero cuentas y ausencia de `SchoolMemberships`. El historial conserva la migración aplicada inicialmente y su simplificación; el modelo actual no tiene esa tabla.

No se modificó el PHP, no se arrancó su MariaDB y no se importaron registros reales. No se creó commit, PR ni publicación externa.

Al cerrar el primer incremento se había iniciado el host en 5080. Fue detenido durante el segundo incremento; el estado más reciente está indicado arriba.

## Prototipo móvil Flutter (5 de noviembre de 2026)

Prototipo navegable en `mobile/` con **datos ficticios en memoria**; no se conecta a la API ni a SQL. Ver `mobile/README.md` y ADR-008.

- Perfiles: estudiante (inicio, cursos, agenda/tareas con entrega, notas, avisos), familia (dos hijos, notas con bloqueo por solvencia, estado de cuenta, comprobantes), docente (clases del día, asistencia, libreta por bimestre, revisión), administración (indicadores, directorio/expediente, matrícula con incidencias, anuncios, cuentas y códigos de un uso) y caja (resumen, cobro con vista previa e idempotencia, recibos, validación de comprobantes, movimientos).
- Marca desde `config/institution.json` mediante `mobile/tool/sync_institution.dart`; tema claro/oscuro, navegación adaptable (barra inferior / riel lateral) y estados de carga, vacío y error con reintento.

| Comprobación (Linux, Flutter 3.47.9) | Resultado |
| --- | --- |
| `flutter analyze` con reglas estrictas | Sin observaciones |
| `flutter test` | 62 correctas: unitarias de configuración, formatos, solvencia, pagos idempotentes, entregas, libreta y códigos; navegación de los 5 perfiles en 390, 320 y 1024 px y con texto al 130 %; botón atrás del sistema (no cierra la app, vuelve a la primera pestaña, doble pulsación para salir, confirmación con cambios sin guardar); flujos completos |
| `flutter build web --release --no-web-resources-cdn` | Correcto |
| Recorrido Playwright de la build web (390×844 y 1366×860), capturas revisadas | Sin errores de consola tras corregir un desbordamiento de `1 << 32` en web y etiquetas largas de navegación |

No se probó en dispositivos/emuladores Android o iOS ni se compiló APK: el entorno bloquea la descarga del SDK de Android. El botón atrás se verificó con el canal de plataforma simulado en pruebas de widgets y el atrás del navegador en la build web. Las capturas de QA no se versionan.

## Próximo bloque

Pruebas físicas de portería/carnets en Android/iOS e impresora, confirmación de licencia y procedimientos del colegio; luego piloto con respaldos y restauración, conciliación, capacitación y endurecimiento. Persisten pendientes de políticas académicas, recuperaciones/promoción, notificaciones externas, antivirus y extracción/conciliación del legado. Los módulos persistentes descritos arriba existen; el prototipo Flutter general y las muestras de diseño no los reemplazan.
