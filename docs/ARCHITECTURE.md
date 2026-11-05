# Arquitectura inicial

Estado: primer incremento de infraestructura, no núcleo escolar completo.

Cada colegio tiene una copia independiente del proyecto, su despliegue y su base de datos. No hay selección de cliente en runtime, tenants, directorio compartido ni membresías entre colegios. `SchoolId` es un identificador de la institución instalada, sin función de enrutamiento o particionamiento.

`SchoolPlatform.Host` compone ASP.NET Core, Identity, SQL Server, Razor Pages y la API. Angular se compila y copia a `wwwroot/app`, y el mismo host sirve sus archivos en la raíz. No se habilitó CORS ni se usan tokens en localStorage. El servidor verifica que la cuenta siga activa antes de devolver la sesión.

El primer módulo separado es `SchoolPlatform.Assessment`, una biblioteca sin dependencias web/EF que contiene el cálculo por materia. IdentityAccess y Branding se mantienen como carpetas del host durante esta base pequeña. Al crecer, los módulos de personas/matrícula tendrán sus propios límites, contratos y tablas; no importar entidades EF ajenas ni crear un repositorio genérico universal.

SQL Server conserva Identity en `iam`; `ApplicationUser.IsActive` controla el acceso a los recursos privados y se comprueba con la cookie actual. Las migraciones son explícitas; el host normal no ejecuta migraciones ni crea cuentas al arrancar. `--initialize-database` y `--create-operator` son comandos solo de Development. El historial incluye la primera migración local y su simplificación a cuentas de una sola institución; el modelo final no contiene membresías ni tablas de tenants.

## Contratos existentes

| Ruta | Acceso | Contenido |
| --- | --- | --- |
| GET `/api/v1/bootstrap` | Público | Marca validada y proyección pública |
| GET `/branding/theme.css` | Público | Cuatro variables CSS validadas para Razor |
| GET `/api/v1/session` | Cookie + cuenta activa | Usuario y SchoolId institucional |
| GET `/api/v1/antiforgery` | Usuario autenticado | Token de formulario; permite salir aunque la cuenta se desactive |
| GET `/api/v1/preview` | Solo Development | Seis estudiantes y gráfica sintéticos |
| GET `/health/live` | Público | Proceso vivo; no verifica SQL ni representa readiness |
| GET/POST `/Identity/Account/Login` | Público, POST antiforgery | Identity real, bloqueo por intentos y redirección local |
| GET/POST `/Identity/Account/Logout` | Autenticado, POST antiforgery | Confirmación y cierre real de cookie |

Las API desconocidas devuelven 404, las privadas 401/403 sin HTML de login. La SPA puede descargar su shell anónimamente, pero no obtiene datos privados sin autorización del servidor. La muestra no se usa en `/workspace` y su API no se registra en Production. Las rutas Angular se cargan por funcionalidad.

No hay todavía permisos académicos/financieros, personas, matrícula, invitaciones, auditoría durable ni worker. No se expusieron endpoints ficticios para esos módulos. Redis, OpenIddict y Flutter se posponen hasta contar con casos y contratos reales.

Se fijaron SDK estable 10.0.302, paquetes .NET/EF 10.0.14, Angular 22.2.4/CLI 22.2.6, Node 24.15.0 y TypeScript 6.0.2 según los paquetes disponibles y [compatibilidad oficial de Angular](https://angular.dev/reference/versions). El host local tenía runtime 10.0.10; para producción se debe usar un runtime mantenido compatible, elegido al preparar ese ambiente. La configuración usa el [patrón Options de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0).
