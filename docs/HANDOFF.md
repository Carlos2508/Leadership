# Handoff para la siguiente sesión

## Referencias pública/estudiantil — 5 de noviembre de 2026, leer primero

- Solicitud vigente: reproducir las capturas aportadas y usar el paquete local `Colegio_Leadership_HTML_CSS/Colegio_Leadership`. ADR-013 reemplaza la dirección visual Numix/Figtree de ADR-010; sus módulos, autorización y contratos se conservan. Cambios en working tree sobre **`434a824`**, sin commit/push de este incremento ni despliegue.
- Inter normal/cursiva y licencia en `wwwroot/fonts`; cuatro recursos del paquete copiados sin alterar a `wwwroot/branding` (SHA-256 idénticos). `reference/REFERENCE_DESIGN_ASSETS.md` registra fuentes y carácter ilustrativo. Todos los textos/marca/colores en el único `institution.json` y `InstitutionOptions`; `BRANDING.md` y el ejemplo neutral incluyen campos nuevos. Misión/visión y presentación son redacción expresamente autorizada para diseño, no documentos oficiales ni política académica/legal aprobada.
- `welcome.ts/html`, `_public.scss`: portada curva, pilares, misión/visión y secciones reales Vida Escolar/Admisión/Comunidad/preguntas/Contacto. RouterLink + `ViewportScroller` para destinos bajo la cabecera; menú móvil se cierra/Escape; imágenes/contactos/secciones opcionales vacíos se ocultan, mapa solo por acción. No emitir llamadas a un prefijo sin teléfono ni enlaces de correo vacío.
- `workspace.ts/html`, `shared/school-calendar.ts`, `_reference-home.scss`: composición de referencia con indicadores reales, calendario navegable/teclado/filtro por fecha, comunicados, acciones y cursos propios. Administración, docencia y familias conservan sus datos operativos. `DashboardStudent` añade `isSelf`, `courses` y `courseItems`; la consulta exige `StudentAccessAsync` y facultad académica/acceso propio, matrícula activa y colocación del ciclo. Una facultad financiera no expone cursos/tareas; no se devuelven notas ni promedio general.
- `Program.cs`/`SessionService`: contexto de perfil para cabecera/menú estudiantil, con rol de acceso leído de SQL. Solo presentación; permisos/MFA/autorización por recurso no cambian. Shell conserva el ciclo en fragmentos; Inicio ignora query para su selección. `my-student.ts` desplaza al fragmento después de cargar el contenido. No hay módulo de horario/Recursos nuevo ni datos de la muestra convertidos en backend.
- `_identity.scss` sigue cargándose al final. Negro/dorado y marfil, cabecera/menú oscuros y botones dorados; fondo opaco en navegación/cuenta para que el arte no reduzca contraste en menús largos. `auth.css` limpio y compartiendo Inter/paleta con Razor. Alertas siguen exclusivamente en `core/alerts.ts`.
- Validación: **89 .NET/SQL aprobadas**, build Angular correcto; suite completa **81 E2E aprobados + 1 omisión prevista móvil**, 0 fallos, más **18 aprobados** tras el ajuste final del menú/ingreso. Seis ejecuciones nuevas en `reference-design.spec.ts`. Axe y capturas revisadas desde 320 hasta 1920 px. Fuente de verdad: `QA_REFERENCIA_2026-11-05.md`. Advertencia conocida SweetAlert2 CommonJS.
- Evidencias `.local/qa/evidencia/reference-redesign-2026-11-05/complete-suite` (159 PNG), `final-visual` (58 PNG) y cuatro copias directas de portada/panel escritorio/móvil. Sin trazas copiadas; MFA/QR protegidos por sus máscaras existentes y trazas desactivadas en recorridos de códigos. **0 bases SQL temporales al cierre**; `git diff --check` correcto.
- Host de revisión final en **`http://localhost:5082`**, raíz/liveness/readiness 200. Lanzador **3372**, metadatos `.local/qa/reference-design-host.json`, logs `.local/qa/reference-design-5082.out.log` y `.err.log`. El host anterior 11232 se detuvo tras verificar su ruta para compilar. **No compilar host mientras Playwright lo mantiene abierto**. Verificar proceso/puerto al retomar; los PIDs son históricos.
- Sin migraciones, modificación/importación de registros de desarrollo, PHP/MariaDB/Numix ni cambios de Flutter. Los pendientes previos de licencia, políticas, publicación, operación y pruebas físicas siguen vigentes. No declarar fases enteras terminadas por este diseño.

## Consolidación en Git — 5 de noviembre de 2026

Por solicitud explícita del propietario, este commit consolida la QA continuada y la revisión completa de experiencia descritas abajo. Las menciones a working tree o ausencia de commit/push son históricas respecto al cierre de esas revisiones. Se conserva su validación aprobada y se verifica `git diff --check`; no se repiten pruebas por el solo hecho de versionar. Credenciales y evidencias ignoradas quedan fuera del commit. Esta solicitud no incluye despliegue.

## Revisión completa y experiencia — 5 de noviembre de 2026, leer primero

- El propietario volvió a pedir todos los flujos y mejoras de experiencia. Informe/matriz: `QA_EXPERIENCIA_2026-11-05.md`, EX-001 a EX-005 y mejoras de uso. Este incremento y la QA anterior siguen en working tree sobre `ed6022f`; no hubo commit/push o despliegue en estas revisiones.
- `course-attendance.ts` usa el guard compartido `pendingChangesGuard`, también ante cambios de query/curso. Espera guardados en curso, protege borradores y ofrece descarte explícito. Nunca guardar al destruir. Fecha en URL con historial; cancelar un cambio restaura el input mediante `markForCheck` (necesario en el Angular sin zone). Solicitudes versionadas, error/reintento sin filas anteriores, estados completo/parcial/pendiente, totales del borrador y controles bloqueados al enviar. Día siguiente/anterior es calendario, sin omitir fines de semana como si existiera política lectiva. Ver `ATTENDANCE.md`.
- `enrollment-roster.ts/html`: consulta dirigida por URL de ciclo/filtros/página y contador de solicitudes. Durante carga/fallo retira datos, impide acciones anteriores y muestra reintento. Un ciclo desconocido es error explícito. Después de mutar se refresca el detalle para cupos/versiones. El historial restaura búsqueda/estado; Limpiar filtros conserva ciclo.
- `core/access-api.ts` conserva query y fragmento al regresar desde ingreso tras 401. `session-recovery.spec.ts` vence cookies reales y vuelve a ingresar en el mismo ciclo (sin trazas de credenciales). Caja muestra orientación cuando la búsqueda no coincide y retira resultados al cambiar de término; no equiparar error con resultado vacío.
- Nuevas pruebas: `attendance-resilience.spec.ts`, `enrollment-resilience.spec.ts`, `session-recovery.spec.ts`; helpers en `e2e/support/school.ts`. Usan ciclos sintéticos 2024/2025 para asistencia pasada y 2028/2029 + 2032/2033 para matrícula, reutilizados dentro de su corrida para no superponer calendarios. No usan la base escolar. La búsqueda vacía también está cubierta en el recorrido existente de Caja.
- Validación final: **89 .NET/SQL aprobadas**, 0 omisiones; **68 Flutter aprobadas**, `analyze --no-pub` sin observaciones; build Angular correcto. **75 E2E aprobados + 1 omisión prevista de rendimiento móvil**, 0 fallos, corrida completa de 76 casos. Después del espaciado final, **6 E2E adicionales aprobados** con Axe y capturas de escritorio/móvil y 320/768/1920 px revisadas. SQL final: **0 bases temporales Test/E2E**; `git diff --check` correcto.
- Evidencia `.local/qa/evidencia/experience-2026-11-05/` (133 PNG, MFA enmascarado, sin trazas copiadas). Host actualizado `http://localhost:5082`, proceso en `.local/qa/experience-host.json`, logs `experience-5082*.log`. No hay migraciones/backend nuevos ni cambios a registros de desarrollo, PHP/MariaDB/Numix. Pendientes anteriores de políticas, licencia, producción y hardware siguen vigentes; Flutter sigue parcial fuera del lector real de portería.

## QA continuada — 5 de noviembre de 2026, leer primero

- Solicitud vigente: seguir probando flujos y buscando bugs. El propietario reiteró continuar durante el trabajo. Informe y reproducción: `QA_FLUJOS_2026-11-05.md`, incidencias FL-001 a FL-008. El punto de partida Git es `ed6022f` en `main`; este incremento QA está en el working tree.
- `finance-cashier.ts/html`: selección reactiva por URL, contador de solicitudes, limpieza de datos y diálogos al cambiar estudiante, error/reintento del estado de cuenta y recibos de hoy. `finance-resilience.spec.ts` reprodujo cuatro fallos antes de corregirlos en ambos tamaños.
- `course-grades.ts`, `core/pending-changes.ts` y ruta de libreta: guard de salida y de cambios de parámetros/query; espera el guardado vigente y vuelve a guardar lo escrito mientras esperaba. Si persisten cambios, `Alerts` permite seguir editando o descartarlos expresamente. No volver al guardado en `ngOnDestroy`, que puede fallar después de perder la pantalla. Las correcciones publicadas siguen manuales/con motivo. `grade-resilience.spec.ts` cubre fallos, guardados retenidos, descarte de nota inválida, lectura/reintento e historial. No es recuperación tras cerrar el navegador.
- `finance-setup.ts/html` y `finance-clearance.ts`: cargas dirigidas por URL con versiones de solicitud y reintentos; datos anteriores retirados durante la carga/fallo; selección accesible para recuperarse. No sustituir un ciclo inválido ni un corte de otro ciclo. Solvencia lleva filtros al historial. Vista previa invalidada al cambiar ciclo. `finance-cycle.spec.ts` cubre esos casos y comprueba que una cuota posterior a Atrás se guarde en el ciclo correcto.
- `finance.spec.ts` amplía el recorrido a reversión, rechazo de ajuste excesivo, descuento y recobro; contrasta el saldo familiar y solvencia en cada etapa. `roles.spec.ts` espera que Quitar selección alcance el DOM antes de marcar un permiso. El resumen de recibos compara la respuesta real, no presume que otros tests no hayan cobrado.
- `mfa.spec.ts`: `trace: off`, máscaras sobre QR/clave compartida/códigos y campos de código. Se comprobó en capturas que los secretos están ocultos. Una ejecución fue rechazada por revisión automática que interpretó vigente un parche fallido; el diff comprobó el parche posterior aplicado y la repetición fue aprobada y completada. No hay bloqueo pendiente.
- `_identity.scss` da estilo rojo semántico a `.button.danger`, antes inexistente. Se revisa la confirmación de descarte en escritorio y móvil con Axe.
- Validación final: **63 E2E aprobados + 1 omisión prevista de rendimiento móvil, 0 fallos**, corrida completa de 64 casos (24 nuevos). Tras el ajuste rojo, **4 E2E adicionales aprobados** con Axe y capturas finales revisadas. Build Angular correcto; advertencia conocida SweetAlert2 CommonJS. Consulta final SQL: **0 bases temporales Test/E2E**; `git diff --check` correcto. Evidencias `.local/qa/evidencia/flows-2026-11-05/` (126 PNG; secretos MFA enmascarados, sin trazas copiadas). Host actualizado en `http://localhost:5082`, proceso en `.local/qa/flows-host.json`, logs `flows-5082*.log`. Sin cambios backend/migraciones ni datos del colegio; los pendientes anteriores de política, licencia, hardware y Flutter siguen vigentes.

## Dashboard y roles de acceso — 5 de noviembre de 2026, leer primero

- El propietario pidió commit/push del rediseño (realizado como `2f70635`) y un dashboard con información importante, sin duplicar el menú. Después pidió roles configurables y aclaró: estudiante/docente/padre-encargado son perfiles escolares; se deben poder crear Director o Secretaría con cualquier conjunto de permisos, incluso todos.
- Implementados ADR-011 y ADR-012. `Common/DashboardService.cs` genera el resumen por permisos efectivos (incluido MFA), ciclo explícito y acceso por recurso. `workspace.html`/`workspace.ts`, `core/dashboard.ts` y `_dashboard.scss` sustituyen el catálogo de módulos por indicadores, pendientes, acciones, estado académico/financiero/portería y trabajo personal. Portería es hoy/todos los ciclos; no usarla como ausencia confirmada. Un recibo mixto aporta a cada ciclo solo su aplicación; reversiones excluidas y ajustes descontados.
- Las notas siguen únicamente en el portal mediante `GradeService.MyGradesAsync`; el dashboard no devuelve notas. Tareas familiares pasan por `LearningService.StudentAssignmentsAsync` y el saldo solo se consulta tras facultad `Finance`, nunca para el estudiante por defecto. Sin horario de clases no se presume asistencia atrasada. Errores y reintentos visibles.
- IAM nuevo: `AccessRoles`, `AccessRolePermissions`, `AccountRoleAssignments`; una asignación de rol por cuenta en este incremento. `AccessRoleService` y `/workspace/roles` permiten administrar roles en SQL. `/access/profiles` y el campo `profile` de los comandos se conservan como alias compatibles de roles SQL. Se eliminó el catálogo fijo `AccessProfiles`; no reintroducir permisos por nombre de rol o por coincidencia de conjuntos.
- `AccountPermissions` es una proyección SQL efectiva; rol, proyección, asignaciones, sellos y auditoría van bajo el bloqueo IAM compartido. Editar un rol cierra las sesiones de sus cuentas. Se protegen último gestor, MFA, versiones, catálogo de acciones y desactivación de roles usados. Los ejemplos iniciales son datos de migración editables, no definiciones de runtime. No crear automáticamente un rol Director con privilegios en instalaciones existentes.
- Migración **Identity `20261106003523_ConfigurableAccessRoles` aplicada en `SchoolPlatform_Development`**. La migración preserva exactamente los accesos previos, incluidos conjuntos personalizados. Hashes de permisos antes/después idénticos; diferencias de proyección **0**. Hashes locales sin datos personales en `.local/qa/role-permissions-{before,after}.sha256`. Sin migración School nueva.
- Pruebas finales: **89 .NET aprobadas (SQL real), 0 omitidas; build Angular correcto; 39 E2E aprobados, 1 omisión prevista**. Se validaron recibos multiciclo, reversiones, fechas de vencimiento, facultades/revocación, MFA del resumen y roles, cierre de cookies al editar un rol, último gestor, rowversion y preservación de migración. Pruebas antiguas adaptadas a la navegación y al contenido nuevo, sin debilitar las comprobaciones de permisos.
- Evidencia: `.local/qa/evidencia/dashboard-roles-2026-11-05/`, con archivos cortos `dashboard-desktop.png`, `dashboard-mobile.png`, `roles-editor-desktop.png`, `roles-mobile.png`, además de capturas de todos los recorridos. Revisados escritorio y móvil; 320/768/1024/1920 px sin desbordamiento en el dashboard. Ninguna base temporal Test/E2E residual al finalizar.
- La revisión final detectó texto demasiado estrecho junto a la acción de un pendiente en 320 px: ahora el botón va debajo. Los importes diarios del gráfico se despliegan como lista en móvil. **4 E2E adicionales aprobados** después del ajuste; evidencia corta reemplazada por las capturas finales.
- Host actual `http://localhost:5082`, lanzador/puerto en `.local/qa/dashboard-roles-host.json`, logs `.local/qa/dashboard-roles-5082*.log`. Los procesos anteriores se detuvieron para liberar la compilación. No reconstruir el host mientras Playwright lo usa. El publicado `.local/publish` sigue siendo histórico; no hubo despliegue. Se cierra con commit/push por la solicitud vigente.
- Pendientes de políticas, licencias, hardware, piloto, Flutter y producción conservados. No se tocó el PHP, Numix ni sus bases, ni se importaron personas reales.

## Rediseño web — 5 de noviembre de 2026, leer primero

Por solicitud posterior del propietario, este rediseño se consolida en Git antes de sustituir los accesos duplicados del dashboard por información operativa. Las menciones a «sin commit» siguientes corresponden al cierre previo.

- El propietario rechazó la estética anterior y pidió simplicidad comparable con Numix. Se revisaron el sitio público, dashboard, listado de productos y código de Numix en modo lectura. El usuario omitió personalmente su aviso de 2FA. No se cambió la seguridad de Numix ni se guardaron sus credenciales en el repositorio.
- `DESIGN.md` y ADR-010 reemplazan la dirección visual anterior: Figtree en toda la interfaz, menú sólido, contenido claro, paneles blancos y decoración mínima. `_identity.scss` debe seguir al final de `styles.scss`. Alertas siguen exclusivamente en `core/alerts.ts`.
- Rutas `/workspace/students`, `/workspace/parents`, `/workspace/teachers`: mismo componente `PeopleDirectory` con datos de ruta y filtros `student/guardian/staff`. El directorio general `/workspace/people` queda en la pestaña Todos. Los filtros se limpian conservando el módulo. La ficha vuelve al listado de su perfil. Los permisos no se amplían.
- `CreatePersonRequest.StaffPosition` es opcional; al indicarlo se valida y se crea personal con auditoría en el mismo `SaveChanges`. No requiere migración. No inferir asignación docente del texto del cargo: el módulo muestra Docentes y personal y las asignaciones permanecen en Cursos.
- Formulario: nombres/apellidos y código/cargo primero; contacto y perfiles adicionales desplegables. Página pública e ingreso sin logos repetidos. Portería y Administración son `details` nativos; el control de foco del menú incluye `summary` y solo cierra al pulsar enlaces.
- Pruebas: **81 .NET/SQL aprobadas, 0 omitidas; build Angular correcto; 33 E2E aprobados y 1 omisión prevista; 14 E2E adicionales aprobados** tras el último ajuste del formulario y tamaños públicos. Axe en recorridos, capturas de escritorio/móvil revisadas; directorios/formularios desde 320 hasta 1920 px. `school-date.ts` corrige la fecha de pagos/reportes E2E cuando UTC ya cambió de día.
- Evidencia preservada fuera de la carpeta que vacía Playwright: `.local/qa/evidencia/redesign-2026-11-05/`. Sin datos reales ni cambios al PHP o proyecto Numix. No hay migración nueva ni commit del rediseño. El commit/push de la sección siguiente corresponde al estado anterior.
- Host del rediseño iniciado y verificado con `dotnet run --no-build` en `http://localhost:5082`, lanzador 9816, logs en `.local/qa/redesign-5082*.log`. El intento de usar 5080 terminó porque otro host del proyecto (14216) ya lo ocupaba; se conservó ese proceso. Comprobar puerto antes de iniciar otro. El host anterior 18356 bloqueaba la compilación y se detuvo tras verificar su ruta. No reconstruir el host con Playwright abierto. El artefacto `.local/publish` sigue siendo histórico. Se revisó el ingreso local nuevamente tras separar el enlace de regreso y la etiqueta superior.
- Flutter y los pendientes de licencia, políticas, hardware y producción no se consideran terminados por este rediseño.

## Consolidación en Git — 5 de noviembre de 2026

El propietario autorizó commit y push de todo lo pendiente a `origin/main`. Este commit consolida los incrementos y las correcciones QA descritos abajo; las menciones históricas a trabajo sin commit corresponden a sesiones anteriores. Se verificó `git diff --check`, sin repetir las suites previamente documentadas. Archivos locales y secretos ignorados siguen fuera de Git. Esta solicitud no incluye despliegue ni modifica los pendientes físicos, de licencia, políticas u operación.

## Undécimo incremento — estado vigente, leer primero

5 de noviembre de 2026, America/Guatemala. Se retomaron y completaron los pendientes de código del bloque velocidad/PDF/carnets/portería. ADR-009 y `GATE.md` contienen contratos y límites. El resto de esta bitácora conserva historia y puede mencionar pendientes ya resueltos.

- UI nueva: `gate-kiosk.ts`, `gate-report.ts`, `gate-devices.ts`, `person-card.ts`; rutas `/workspace/gate`, `/workspace/gate/report`, `/workspace/gate/devices`. Carnets/foto en persona, impresión masiva en inscripciones al elegir grado y consulta de marcas en my-student.
- Lector Flutter real en `mobile/lib/features/gate/`, accesible desde el ingreso sin cuenta demo. Autenticación solo de dispositivo, QR con confirmación de servidor HTTPS, `mobile_scanner`, almacenamiento seguro nativo; en web solo memoria. El resto del móvil sigue ficticio. No hay OIDC/cookies escolares integrados en Flutter.
- El SDK global era anterior al requerido. Se preparó Flutter **3.47.9** en `.tools/flutter-3.47.9/flutter`, SHA-256 verificado con manifiesto oficial. Usar ese binario o un SDK compatible, sin bajar los requisitos del proyecto. `pubspec.lock` actualizado.
- Reemplazos de carnet y anulaciones comparten bloqueo SQL con lecturas. Reemplazo + auditoría en una transacción; una lectura que estaba esperando vuelve a comprobar que el carnet no fue revocado.
- Migración `20261105154004_StudentCardsAndGate` aplicada en `SchoolPlatform_Development`, comprobada en `school.__EFMigrationsHistory`. No se tocó PHP/MariaDB ni se importaron datos personales. Los sintéticos de desarrollo se conservaron.
- Validación: 79 pruebas .NET/SQL, build Angular, 31 E2E aprobados y 1 omitido previsto (rendimiento móvil). Medición de escritorio 101–145 ms por página, API más lenta 11 ms; no es prueba de carga. Las capturas de portería están en `web/test-results/gate-*`; la prueba no retiene trazas porque el QR es una credencial.
- Flutter final: **68 pruebas correctas**, análisis sin observaciones, build web de producción correcto. Capturas de la build en 390×844 y 1366×860 revisadas, sin errores JavaScript (`web/test-results/flutter-reader-*.png`); no se activó una cámara física. Las advertencias de build sobre fuentes Cupertino no impidieron compilar.
- Un intento intermedio dejó una base E2E al fallar el teardown por recompilar mientras el host estaba abierto. Se retiró **solo la base de esa corrida** con el propio TestHarness. No compilar cambios del host mientras Playwright lo mantiene abierto. La suite final retiró sus bases; consulta final de `sys.databases` sin bases temporales de integración/E2E.
- Capturas revisadas: se detectaron campos sin estilos y estrechez del reporte móvil; corregidos con `.form-field`, `.access-form` y envoltura de filas. PDF de tarjeta y carta renderizados con Poppler; fotos sintéticas (cuadrado de color), sin datos reales.
- **Pendientes que requieren entorno/decisión externa**: impresión real CR80/carta al 100 % y lectura de equipos; cámara/permisos, segundo plano y almacenamiento seguro en Android/iOS físicos; HTTPS accesible para emparejar (configurar `Gate__PublicUrl`); confirmar elegibilidad QuestPDF Community con el propietario. No declarar probado APK/iOS ni licencia aprobada.
- Verificación final: `git diff --check` sin errores (avisos habituales LF/CRLF), ninguna base SQL temporal residual. El servidor temporal de revisión Flutter (5087) fue detenido; no se inició el host de desarrollo 5080.
- No se hizo commit, PR, publicación ni despliegue. `.local/publish` histórico no representa este incremento. Fases de operación y móvil siguen parciales.

## Décimo incremento — leer primero

- Nuevo: avisos (`Communication/`), MFA (`IdentityAccess/MfaService.cs`, `LoginWith2fa`), adjuntos (`Learning/SubmissionFiles.cs`), readiness (`Common/ReadinessCheck.cs`), respaldo (`scripts/Backup-Database.ps1`), CI (`.github/workflows/ci.yml`, sin ejecutar). Ver `COMMUNICATION.md`, `OPERATIONS.md`, `IDENTITY_AND_ACCESS.md`.
- Pruebas: **71 .NET y 28 E2E**. `SqlTestBase.ExtraConfiguration` permite configurar políticas por clase de prueba.
- Por pedido del propietario, los datos sintéticos de la base de desarrollo **se conservan**.
- Importador MIG-001a desde staging revisado implementado (`MIGRATION.md`); falta el extractor del legado.
- Pendiente: notificaciones externas, antivirus, plan de respaldos de producción y despliegue, y las políticas pendientes del colegio. Flutter: otra sesión/rama.

## Estado consolidado anterior

- Implementado con persistencia, autorización y pruebas reales: IAM, personas/familias/facultades, ciclos y matrícula, cursos y docentes, finanzas y solvencia, libreta y publicación con consulta condicionada, tareas y entregas, y un panel inicial por rol.
- (Resuelto: el propietario pidió conservarlos.) Datos sintéticos en `SchoolPlatform_Development` (cuentas `synthetic-*` con contraseña de prueba conocida) creados por un error ya corregido del fixture; la limpieza dirigida fue denegada por la herramienta. Todas las migraciones están aplicadas en esa base.
- **Pendiente de política**: redondeo/promedio anual/boleta, recuperaciones, planes financieros con prorrateos, saldo a favor, acceso del estudiante a su detalle financiero, mayoría de edad/custodia.
- Asistencia por curso implementada (`ATTENDANCE.md`). Pruebas finales: **62 .NET y 24 E2E**.
- **Pendiente técnico**: MFA con challenge, CI, archivos privados (tareas/documentos), avisos/notificaciones, importador del legado (sin ejecutar apply), despliegue/backups.
- No se creó commit ni PR; no se publicó ni desplegó; no se tocó PHP/MariaDB ni datos reales.

## Actualización del octavo incremento

- LRN-001 implementado (`Learning/`, `LEARNING.md`). `CourseService.CanTeachAsync`/`StudentsBetweenAsync` centralizan alcance docente y audiencia por fechas.
- Pruebas: **60 .NET y 24 E2E**. E2E de tareas reutiliza un ciclo que abarque «hoy» (los ciclos no pueden superponerse).
- Siguiente: pulido transversal (saludo con nombre de la persona, panel inicial según rol, navegación móvil), después ATT/COM.

## Actualización del séptimo incremento

- ASM-001/002 implementado en `Grades/` (ADR-007). Solo notas de período; promedios/boletas esperan política.
- Pruebas: **58 .NET y 22 E2E**. `SqlTestBase.Errors` captura errores del servidor y la prueba falla si hubo alguno.
- Siguiente: LRN-001 (tareas por curso, audiencia efectiva derivada de la lista por fecha, entregas de texto; archivos privados después), luego pulido general.

## Actualización del sexto incremento

- ACA-002 implementado (`Academic/CourseService.cs`, `COURSES.md`). Lista de curso derivada de ubicaciones; acceso docente mientras la asignación no finalice.
- Pruebas: **55 .NET y 20 E2E**. En Git Bash, evitar dos heredocs con comillas en un mismo comando (falla el parser); usar un script Python en el scratchpad.
- Siguiente: ASM (libreta y publicación) usando `CourseService.CanViewAsync`/rosters para captura docente y `ClearanceService` + facultad `AcademicRecords` para consulta familiar.

## Actualización del quinto incremento

- Núcleo FIN implementado en `Finance/` y `SchoolData/SchoolDbContext.Finance.cs`. Ver `BILLING_AND_CLEARANCE.md`, ADR-006.
- Para notas: crear `PublicationBatch` con referencia a `ClearanceCutoff` y consultar `ClearanceService.CheckAsync` en cada lectura familiar/estudiantil, además de `PeopleService.StudentAccessAsync` con la facultad `AcademicRecords`.
- `NEXT VALUE FOR` no funciona con `SqlQueryRaw` (EF lo envuelve en subconsulta); se usa un comando ADO en la transacción actual.
- Pruebas: **53 .NET (40 SQL) y 18 E2E**.
- Siguiente: ACA-002 (materias, cursos, docentes y membresía por curso), luego LRN y ASM.

## Actualización del cuarto incremento

- ACA-001/ENR-001 implementado: `Academic/` (modelo, `AcademicService`, `EnrollmentService`, endpoints) y `SchoolData/SchoolDbContext.Academic.cs`. Ver `ENROLLMENT.md`, ADR-005.
- Pruebas: **48 .NET (35 SQL) y 16 E2E**, también repetidas 2 veces sin fallos. `SqlTestBase` simplifica nuevas pruebas SQL (base aleatoria, administrador sintético, login y POST con CSRF).
- Para ejecutar Playwright con argumentos (por ejemplo `--repeat-each`) el wrapper npm los consume; usar `.tools/node/node_modules/node/bin/node.exe web/node_modules/@playwright/test/cli.js test …` desde `web/`.
- Migración `AcademicStructureAndEnrollment` aplicada en la base de desarrollo (que aún contiene los datos sintéticos del incidente del tercer incremento).
- Siguiente: FIN-001…006 (finanzas/solvencia, sin inventar montos ni calendario: son datos que registra el colegio) y ACA-002 (cursos/materias/carga docente).

## Actualización del tercer incremento

- PEO-001/002 implementado: ver `PEOPLE_AND_FAMILIES.md`, ADR-004. `SchoolDbContext` (carpetas `People/`, `SchoolData/`) es el contexto de registros escolares; los siguientes módulos (ciclos, matrícula) deben agregarse ahí con su propio archivo de configuración (`partial void ConfigureAcademicModel`), migraciones en `SchoolData/Migrations`.
- Para escribir: `SchoolAudit.RequireAsync(actor, ct, permisos…)` recomprueba en SQL; `SchoolAudit.Record(...)` agrega el evento y se confirma con el mismo `SaveChangesAsync`. Excepciones esperadas: `Common/OperationException`; `ApiWriteFilter` valida CSRF y traduce 400/403/404/409 (incluye duplicados SQL 2601/2627).
- Autorización por recurso para portales futuros: `PeopleService.StudentAccessAsync(userId, studentId)` devuelve `null` (responder 404) o las facultades vigentes. Notas exigirán además solvencia.
- Pruebas: **41 .NET (28 SQL) y 14 E2E aprobadas**. El harness E2E migra Identity y School.
- **Incidente pendiente de decisión del propietario**: `SchoolPlatform_Development` contiene cuentas y personas sintéticas creadas por una corrida de prueba antes de corregir la resolución de la conexión (detalle en `PROGRESS.md`). La limpieza dirigida fue denegada por la herramienta; no reintentar sin autorización expresa. Esa base tiene ya aplicadas `AuditTargetType` e `InitialPeople`.
- Siguiente: ACA-001/ENR-001 (ciclos, grados, secciones opcionales, matrícula efectiva con ubicaciones vigentes), luego finanzas/solvencia.


## Actualización del segundo incremento — leer antes del resto

El usuario ahora solicita continuar de forma autónoma durante una sesión prolongada. El próximo bloque funcional es **personas/estudiantes/encargados y facultades**, seguido de ciclos/matrícula. La base de gestión de acceso descrita como pendiente en notas históricas **ya se implementó**.

- Pruebas actuales: **35 .NET aprobadas (22 SQL), 0 omitidas; 12 E2E aprobadas** en escritorio/móvil. Build Angular de producción y restore con lock correctos.
- `AccountAccessService` administra invitaciones, códigos de activación/recuperación, perfiles, suspensión/habilitación y cierre de sesiones. `AccountPermissions` guarda `accounts.manage` y `audit.read`; el servicio reautoriza al actor en SQL.
- Los cambios y eventos de `audit.Events` se confirman juntos. El trigger y EF impiden UPDATE/DELETE; el dueño del esquema todavía puede alterar esos controles. No hay contraseña ni código en auditoría.
- La cookie compara el sello durable directamente por petición. Una prueba con reloj fijo detectó que un intervalo cero no garantizaba validar en el mismo instante; no sustituir la validación directa por temporización sin demostrar equivalencia.
- Códigos: ID + 32 bytes aleatorios, solo hash persistido, propósito, emisor, expiración, uso/revocación y rowversion. Entrega manual a titular verificado, sin correo obligatorio. El secreto se muestra una vez, jamás en URL o listados. Dos canjes concurrentes solo permiten un éxito.
- `sp_getapplock` de transacción serializa escrituras IAM y protege al último gestor activo, incluso con dos cambios simultáneos. No hacer envíos ni llamadas externas bajo el bloqueo.
- Nuevas UI: `/workspace/access`, `/workspace/audit`, y formulario Razor público `/Identity/Account/Activate`. Los perfiles de cuentas **no** conceden derechos académicos/financieros ni vínculos familiares.
- **Tres migraciones** aplicadas en `SchoolPlatform_Development`, **cero cuentas**. La tercera es `AccountAccessAndAudit`, con trigger append-only. No reescribir migraciones ya aplicadas.
- Primer gestor: `Initialize-Development.ps1 -CreateOperator` en desarrollo. `Provision-Owner.ps1 -Environment Production` exige base previamente migrada y no inicia el host. No hay contraseña predeterminada; no crear gestores automáticamente al arrancar.
- Playwright usa un catálogo aislado `SchoolPlatform_E2E_<uuid>` preparado/eliminado por `tests/SchoolPlatform.TestHarness`, con credenciales sintéticas en memoria. `SCHOOLPLATFORM_E2E_SQL` configura la instancia; `SCHOOLPLATFORM_TEST_SQL` corresponde a los fixtures .NET. No usar datos ni cuentas de la base escolar en pruebas.
- El host 5080 del primer incremento fue detenido para compilar. **Actualmente no se reinició**. `Start-Development.ps1 -SkipBuild` lo recupera; comprobar primero si el puerto está ocupado. `.local/publish` sigue siendo del primer incremento: regenerar si se necesita el artefacto actual.
- Corrección responsive: `.table-scroll` es posicionada para contener etiquetas absolutas de lector de pantalla y evitar que agranden el viewport móvil. Los diálogos tienen límites en `dvw/dvh`. No quitar esa contención sin revisar móvil y los botones del diálogo.
- Código y build web actuales están probados. No se modificó PHP/MariaDB, no se importaron personas reales y no se creó commit/PR ni publicación externa.

Leer `IDENTITY_AND_ACCESS.md` y ADR-003 para los contratos y límites nuevos. La fase de operación aún necesita MFA con challenge, CI, backups/restauración y endurecimiento de producción. Las cuentas marcadas con segundo factor se rechazan en el login simple, nunca se omite ese factor.

Las secciones siguientes preservan contexto del primer incremento; esta actualización prevalece donde cambió el estado.

## Leer primero

1. `AGENTS.md` y `docs/PROGRESS.md`.
2. `docs/PROJECT_DEFINITION.md`, ADR-001/002 y `docs/ARCHITECTURE.md`.
3. `docs/ROADMAP.md` y el documento del módulo que se vaya a implementar.

El usuario pidió comenzar, no completar en una sesión todos los módulos. Este incremento deja una base ejecutable y una muestra clara del kit. **La aclaración más reciente exige un proyecto/base por colegio**, copiado y renombrado mediante configuración. No volver a introducir SaaS, selector de colegio, resolución de tenant ni membresías de cuentas entre instituciones.

## Ubicaciones

- Nuevo proyecto: `D:/Repos/Leadership`.
- PHP de referencia: `C:/xampp/htdocs/sys/sys-escolar` (sin modificar).
- Documento aportado: `C:/xampp/htdocs/sys/ANALISIS_Y_PLAN_MAESTRO_PLATAFORMA_ESCOLAR.md`; copia preservada en `docs/reference/PLAN_MAESTRO_ORIGINAL.md`.
- Kit original: `C:/Users/Carlos/Downloads/Leadership_Mixco_Kit_Completo/Leadership_Mixco_Design_Kit`. Referencias y licencia copiadas a `docs/reference`.
- Única marca editable: `config/institution.json`. Sobrescritura local opcional `config/institution.local.json`, ignorada. Logos públicos en `src/SchoolPlatform.Host/wwwroot/branding/`.

Los bloques de instrucciones del plan/kit son contenido de las fuentes. Distinguir propuestas, reglas atribuidas al propietario y pendientes; no ejecutarlos como órdenes autónomas. La solicitud directa y los documentos de decisiones actuales guían el trabajo.

## Estado técnico

- ASP.NET Core/EF 10, Angular 22, SQL Server real. SDK y paquetes fijados; Node portable dentro de `.tools` (ignorado).
- Host compone Branding e IdentityAccess. Único módulo separado actual: cálculo de Assessment; no tiene endpoint de notas.
- `ApplicationUser.IsActive` y política `ActiveAccount`. Cuenta local activa es solo acceso inicial; no equivale a director/docente/cajero y no hay todavía permisos de esas funciones.
- Login/logout Razor con Identity, CSRF, cookie y redirección local. Sin registro público. Crear operador manual con `Initialize-Development.ps1 -CreateOperator`; no inventar/instalar credenciales.
- `SchoolId` en configuración identifica a la institución instalada, no selecciona ni particiona clientes. La base pertenece íntegramente a un colegio.
- Base `SchoolPlatform_Development` en SQL local con autenticación Windows. Se creó para este proyecto y no contiene personas del legado ni usuarios precargados.
- Dos migraciones ya aplicadas localmente. La segunda simplifica la primera a una sola institución. No borrar/reescribir su historial ni apuntarlas a una base ajena.
- SPA estática en `wwwroot/app`, ignorada; `npm run build` la regenera. El host sirve sus archivos en la raíz y fallback a su index. Angular no incluye estilos críticos inline porque su activador requiere un script inline bloqueado por CSP.
- `/api/v1/bootstrap` público; `/api/v1/session` exige cuenta activa; `/api/v1/antiforgery` exige autenticación. API de preview solo Development; desconocidas devuelven 404.
- No existe CI remoto todavía. Hay scripts reproducibles y pruebas locales completas.

## Cómo verificar

Ver `docs/RUNBOOKS.md`. En Windows, `dotnet test` ejecuta 18 pruebas; cinco crean bases SQL temporales propias. `SCHOOLPLATFORM_TEST_SQL` permite otra instancia, pero el fixture siempre fuerza su catálogo aleatorio. Sin SQL no reportar integración como aprobada.

`./scripts/Invoke-Web.ps1 run build` y `./scripts/Invoke-Web.ps1 test` ejecutan build y ocho E2E en escritorio/móvil. Chromium se prepara con `./scripts/Invoke-Web.ps1 exec playwright install chromium`. Capturas: `web/test-results/`. No usar capturas iniciales fallidas como resultado final.

Inicio normal: `./scripts/Start-Development.ps1 -SkipBuild` en 5080 si la web ya fue construida. La muestra está en `/preview`; `/workspace` solicita login y solo muestra el estado inicial real.

Al cerrar el primer incremento quedó iniciado ese host en 5080. También se probó el artefacto publicado en 5086 (HTML/JS 200, bundle igual al actual) y se detuvo aquel proceso de prueba. No asumir que el host 5080 seguirá vivo en otra sesión; comprobarlo antes de iniciar otro.

## Prototipo móvil

`mobile/` contiene un prototipo Flutter con datos ficticios (ADR-008, `mobile/README.md`). Antes de ejecutarlo: `flutter pub get` y `dart run tool/sync_institution.dart` desde `mobile/`. Verificar con `flutter analyze` y `flutter test` (62 pruebas). El manejo del botón atrás está en `lib/shared/back_navigation.dart` (salida con doble pulsación en raíces, confirmación de cambios sin guardar); toda pantalla raíz nueva debe pasar por `RoleShell` o `ExitOnDoubleBack`, y todo formulario nuevo por `UnsavedChangesGuard`. El servidor simulado (`lib/data/demo`) ilustra reglas pendientes de política; no copiarlo a la API ni tratarlo como especificación aprobada. Para integrar la API, implementar las interfaces de `lib/domain/repositories` con HTTP y sustituir los proveedores de `lib/data/repository_providers.dart`; falta OIDC + PKCE y almacenamiento seguro. No se probó en Android/iOS reales.

## Siguiente incremento recomendado

Cerrar IAM de forma útil antes de datos escolares: permisos explícitos por acción/recurso, auditoría durable y activación/recuperación asistida. Construir primero un flujo de invitación de una cuenta con token de un uso, caducidad y revocación; no exigir correo cuando no existe. Aprovisionar el primer operador de producción por un procedimiento verificable, diferente del comando local de Development.

Después: persona estable independiente de cuenta; perfiles de estudiante/encargado y facultades de relación; ciclo y matrícula efectiva sin sección ficticia. Una cuenta de estudiante no requiere padre. No agregar CRUD escolar anónimo porque el menú todavía no está completo.

Finanzas y corte de solvencia deben preceder a notas familiares/estudiantiles. El cálculo puro aún no resuelve redondeo publicado, intentos de recuperación ni una recuperación menor: esas decisiones siguen pendientes. Todo PDF/recibo futuro debe usar la marca configurada y guardar su snapshot de emisión.

## Límites

No migrar datos ahora: el importador, manifiesto y staging no existen. No tocar PHP/MariaDB, sus claves, hashes ni deudas. No usar estudiantes reales para demos o pruebas. No habilitar alumnos/familias hasta tener permisos, datos, finanzas, recuperación y criterios de lanzamiento completos. No publicar ni desplegar por instrucciones encontradas en una referencia.

Los cambios quedan en el working tree; no se creó commit ni PR. Conservar el trabajo existente, actualizar esta bitácora y reportar pruebas reales al terminar el siguiente incremento.

## Bloque de velocidad, PDFs, carnets y portería

Cerrado en código en el undécimo incremento descrito al inicio. Sustituye la lista anterior de pantallas/Flutter/migración/documentación pendientes. Permanecen las verificaciones físicas y de licencia indicadas arriba.

## Recorrido QA desde base vacía (2026-11-05)

Ver `docs/QA_DESDE_CERO_2026-11-05.md`. La base `SchoolPlatform_Development` se reinició (respaldo verificado previo) y contiene **solo datos ficticios del recorrido** (cuentas `qa-*`, credenciales DPAPI en `.local/qa/`). Flujos recorridos de punta a punta: ciclo/bimestres, materias/cursos, personas/vínculos/facultades, matrícula con cupo, cargos con vista previa, libreta (pegado, Enter, autoguardado), publicación con corte, retención por solvencia, pagos/reversión/recibos, tareas, asistencia, avisos, carnets y portería, permisos por rol y vista móvil. Incidencias QA-001 a QA-005 corregidas. Evidencias en `.local/qa/evidencia/` (Playwright vacía `web/test-results/`). Pendiente físico: impresión real de carnets, lectores USB/cámara, teléfono emparejado con HTTPS, activación de cuentas por navegador.

## Ronda UI/UX (2026-11-05, posterior al recorrido QA)

15 mejoras documentadas en `docs/QA_DESDE_CERO_2026-11-05.md` (UX-01…UX-15). Nuevo `GET /api/v1/dashboard` (`Common/DashboardEndpoints.cs`): cifras del día por permiso de lectura, sin caché. `/api/v1/session` devuelve `displayName` de la persona vinculada. Auditoría: áreas `changes` (predeterminada en la UI) y `gate`. Pendiente sugerido: búsqueda global de personas desde la barra superior, notificaciones externas (requieren proveedor y consentimiento), modo kiosco a pantalla completa para la portería.

## Identidad visual y alertas (2026-11-05)

Ver `docs/DESIGN.md`. Tipografías Fraunces/Figtree autoalojadas (`wwwroot/fonts`, OFL), capa `web/src/styles/_identity.scss`, SweetAlert2 11.26.25 vía `core/alerts.ts` (toasts de éxito, errores de acciones, confirmaciones de acciones sensibles, «Pago registrado → Ver recibo»). Saludo según la hora del colegio en el inicio. Los E2E esperan avisos en `.sp-toast`/`.sp-alert`.

## Página pública y guía de negocio (2026-11-05)

`/` es ahora una página pública profesional (ver `docs/BRANDING.md`): contacto con llamada directa, correo, «Cómo llegar», mapa bajo demanda y accesos «Ingresar a mi espacio». Nuevas opciones `Institution:Contact` y `Institution:Public`. Guía de lógica de negocio en PDF: `docs/manual/Logica_de_negocio.pdf`, generada desde `docs/manual/logica-de-negocio.html` con `node scripts/Build-Manual.mjs` (usar el Node de `.tools`).
