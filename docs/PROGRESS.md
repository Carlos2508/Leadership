# Progreso

Actualizado: 5 de noviembre de 2026, America/Guatemala.

## Alcance vigente

**Un proyecto y una base independientes por colegio.** El propietario aclaró expresamente que copiará este proyecto para otro colegio y cambiará su nombre/configuración. No construir SaaS, selección de clientes, tenants ni membresías entre instituciones. La marca está en un único `config/institution.json`, editable por el desarrollador, sin panel administrativo.

## Implementado

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

## Próximo bloque

ACA-001/ENR-001: ciclos escolares con estados, períodos, grados/niveles, secciones opcionales y matrícula anual canónica con ubicaciones de vigencia (sin sección ficticia, sin ciclo global en sesión). Después, finanzas y solvencia antes de liberar consulta de notas. Personas/familias e IAM ya están implementados; no reconstruirlos.

La fase operativa sigue **parcial** por MFA completo, CI, despliegue/endurecimiento, backups/restauración y preparación de producción. No existen todavía matrícula, cobros, tareas, notas publicadas, boletas/PDF, promoción, importador, Flutter ni Redis. La muestra del directorio no reemplaza el directorio real implementado en `/workspace/people`.
