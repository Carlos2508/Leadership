# Arquitectura inicial

Estado: infraestructura, acceso, personas/familias, ciclos y matrícula; cursos, finanzas, tareas y notas pendientes. Los errores de enlace de modelo (JSON inválido) devuelven 400 `invalid-request` mediante `BadRequestExceptionHandler`.

Cada colegio tiene una copia independiente del proyecto, su despliegue y su base de datos. No hay selección de cliente en runtime, tenants, directorio compartido ni membresías entre colegios. `SchoolId` es un identificador de la institución instalada, sin función de enrutamiento o particionamiento.

`SchoolPlatform.Host` compone ASP.NET Core, Identity, SQL Server, Razor Pages y la API. Angular se compila y copia a `wwwroot/app`, y el mismo host sirve sus archivos en la raíz. No se habilitó CORS ni se usan tokens en localStorage. El servidor verifica que la cuenta siga activa antes de devolver la sesión.

El primer módulo separado es `SchoolPlatform.Assessment`, una biblioteca sin dependencias web/EF que contiene el cálculo por materia. IdentityAccess y Branding se mantienen como carpetas del host durante esta base pequeña. Al crecer, los módulos de personas/matrícula tendrán sus propios límites, contratos y tablas; no importar entidades EF ajenas ni crear un repositorio genérico universal.

SQL Server conserva Identity en `iam`; `ApplicationUser.IsActive` controla el acceso a los recursos privados y se comprueba con la cookie actual. Las migraciones son explícitas; el host normal no ejecuta migraciones ni crea cuentas al arrancar. `--initialize-database` y `--create-operator` son comandos solo de Development. El historial incluye la primera migración local y su simplificación a cuentas de una sola institución; el modelo final no contiene membresías ni tablas de tenants.

El segundo incremento añade concesiones de permisos y códigos en `iam`, y eventos inmutables por aplicación/trigger en `audit`. `AccountAccessService` mantiene cada cambio y su auditoría en una transacción corta con un bloqueo de aplicación SQL: protege el último gestor y el canje de un solo uso. No se ejecutan envíos ni llamadas externas bajo ese bloqueo. Las cookies validan su sello durable directamente por petición. `--provision-owner` prepara solo el primer gestor contra una base ya migrada, sin iniciar la web ni migrar Production.

El tercer incremento agrega `SchoolDbContext` (carpetas `People/` y `SchoolData/`) para registros escolares, con historial `school.__EFMigrationsHistory`. Mapea `iam.AspNetUsers`, `iam.AccountPermissions` y `audit.Events` excluidos de sus migraciones, para FK, recomprobación de permisos y auditoría en el mismo `SaveChanges`. Identity migra primero. Ver ADR-004.

## Contratos existentes

| Ruta | Acceso | Contenido |
| --- | --- | --- |
| GET `/api/v1/bootstrap` | Público | Marca validada y proyección pública |
| GET `/branding/theme.css` | Público | Cuatro variables CSS validadas para Razor |
| GET `/api/v1/session` | Cookie + cuenta activa | Usuario, SchoolId institucional y permisos actuales |
| GET `/api/v1/antiforgery` | Usuario autenticado | Token de formulario; permite salir aunque la cuenta se desactive |
| GET `/api/v1/preview` | Solo Development | Seis estudiantes y gráfica sintéticos |
| GET `/health/live` | Público | Proceso vivo; no verifica SQL ni representa readiness |
| GET/POST `/Identity/Account/Login` | Público, POST antiforgery | Identity real, bloqueo por intentos y redirección local |
| GET/POST `/Identity/Account/Logout` | Autenticado, POST antiforgery | Confirmación y cierre real de cookie |
| GET/POST `/Identity/Account/Activate` | Código de un uso; POST antiforgery | Activación o recuperación sin correo obligatorio |
| `/api/v1/access/*` | `accounts.manage`; POST con CSRF | Invitaciones, perfiles, estado, sesiones y códigos |
| GET `/api/v1/audit` | `audit.read` | Eventos paginados de acceso y de registros escolares (`TargetType`) |
| `/api/v1/people/*`, `/api/v1/relationships/*` | `people.read` / `people.manage`; vínculo de cuenta además `accounts.manage` | Personas, perfiles, vínculos y facultades (ver `PEOPLE_AND_FAMILIES.md`) |
| `/api/v1/academic/*`, `/api/v1/years/{id}/enrollments`, `/api/v1/enrollments/*` | `academic.read` / `academic.manage` / `enrollment.manage` | Ciclos, períodos, grados, secciones e inscripciones (ver `ENROLLMENT.md`) |
| `/api/v1/academic/subjects`, `/api/v1/years/{id}/courses`, `/api/v1/courses/*`, `/api/v1/me/courses` | `academic.read` / `academic.manage` / docente asignado | Materias, cursos, asignaciones y listas (ver `COURSES.md`) |
| `/api/v1/courses/{id}/grades`, `/api/v1/years/{id}/publications`, `/api/v1/me/students/{id}/grades` | Docente asignado o `academic.manage` / `academic.read` / facultad `AcademicRecords` + solvencia | Libreta, publicación y consulta (ver ADR-007) |
| `/api/v1/courses/{id}/assignments*`, `/api/v1/me/students/{id}/assignments`, `/api/v1/me/assignments/{id}/submission` | Docente asignado / estudiante / facultad | Tareas y entregas (ver `LEARNING.md`) |
| `/api/v1/courses/{id}/attendance`, `/api/v1/me/students/{id}/attendance` | Docente asignado / facultad `AcademicRecords` | Asistencia (ver `ATTENDANCE.md`) |
| `/api/v1/announcements*`, `/api/v1/me/announcements*` | `communication.publish` o docente del curso; lectura por audiencia | Avisos (ver `COMMUNICATION.md`) |
| `/api/v1/me/mfa*`, `/api/v1/access/accounts/{id}/reset-mfa` | Cuenta activa / `accounts.manage` | Verificación en dos pasos |
| `/api/v1/me/assignments/{id}/files*`, `/api/v1/files/{id}` | Estudiante propio / personal del curso | Adjuntos privados |
| GET `/health/ready` | Público, sin detalles | SQL y migraciones al día |
| `/api/v1/finance/*` | `finance.read` / `finance.charges` / `finance.collect` / `finance.reverse` | Conceptos, cuotas, cortes, cargos, pagos, recibos y solvencia (ver `BILLING_AND_CLEARANCE.md`) |
| GET `/api/v1/me`, `/api/v1/me/students/{id}` | Cuenta activa + vínculo vigente | Autoservicio por recurso; 404 sin acceso |

Las API desconocidas devuelven 404, las privadas 401/403 sin HTML de login. La SPA puede descargar su shell anónimamente, pero no obtiene datos privados sin autorización del servidor. La muestra no se usa en `/workspace` y su API no se registra en Production. Las rutas Angular se cargan por funcionalidad.

Los perfiles actuales incluyen permisos de personas, académico, finanzas, avisos y portería; la autorización se comprueba por recurso en servidor. No hay worker ni Redis. Flutter conserva perfiles ficticios y añade un lector real de portería con autenticación de dispositivo (ADR-009); la integración de identidad móvil general sigue pendiente. El contrato detallado de gestión y sus límites están en `IDENTITY_AND_ACCESS.md`.

Se fijaron SDK estable 10.0.302, paquetes .NET/EF 10.0.14, Angular 22.2.4/CLI 22.2.6, Node 24.15.0 y TypeScript 6.0.2 según los paquetes disponibles y [compatibilidad oficial de Angular](https://angular.dev/reference/versions). El host local tenía runtime 10.0.10; para producción se debe usar un runtime mantenido compatible, elegido al preparar ese ambiente. La configuración usa el [patrón Options de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0).

## Documentos, portería y rendimiento

Ver ADR-009 y `GATE.md`. `Documents/` genera PDFs autorizados; `Gate/` administra carnets, fotos, eventos y teléfonos. Los eventos/carnets permanecen en la base de esta instalación. Los únicos endpoints con esquema bearer de dispositivo son `/api/v1/gate/device/whoami` y `/api/v1/gate/device/scan`; el resto requiere cuenta activa y permisos/facultad. La credencial de dispositivo no es una cuenta ni abre otros módulos.

Los bundles con hash se precomprimen y cachean, el shell se revalida y las API privadas siguen `no-store`. La reutilización breve de sesión en guards no sustituye autorización del backend. No se añade Redis sin una necesidad medida.
