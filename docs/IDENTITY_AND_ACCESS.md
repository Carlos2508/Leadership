# Cuentas, permisos y activación

Segundo incremento: funciones persistentes de cuentas en la base de **un solo colegio**. No existen tenants ni selección de colegio. Una cuenta sigue siendo independiente de persona, estudiante, encargado y matrícula.

## Roles configurables (ADR-012)

Los roles de acceso se administran en `/workspace/roles` y se asignan desde Cuentas. Su nombre y conjunto de permisos se guardan en SQL: Director y Secretaría pueden recibir todos los permisos o solo los seleccionados. Cada cuenta tiene un rol en este incremento. Estudiante, docente y padre/encargado siguen siendo perfiles escolares independientes; los vínculos, facultades y asignaciones se autorizan por recurso.

Editar un rol actualiza de forma transaccional los permisos efectivos de sus cuentas, cierra sus sesiones y agrega auditoría. Se protege al último gestor activo y no se desactiva un rol mientras tenga cuentas asignadas. La migración conserva los permisos previos y convierte los casos sin coincidencia en roles propios, sin ampliar su acceso. Los identificadores de acciones protegidas permanecen definidos por el servidor; la composición de roles es configurable.

## Ejemplos iniciales (editables; referencia histórica)

| Perfil | Permisos |
| --- | --- |
| Cuenta básica | Iniciar sesión y consultar su espacio inicial |
| Gestión de accesos | `accounts.manage`: invitar y administrar las cuentas de esta instalación |
| Consulta de auditoría | `audit.read`: consultar eventos de acceso |
| Gestión de accesos y auditoría | Ambos permisos; perfil del primer gestor |
| Consulta de personas y familias | `people.read` |
| Secretaría: personas, familias e inscripciones | `people.read`, `people.manage`, `academic.read`, `enrollment.manage` |
| Coordinación académica | `people.read`, `academic.read`, `academic.manage` |
| Caja: cobros y recibos | `people.read`, `academic.read`, `finance.read`, `finance.collect` |
| Administración financiera | `people.read`, `academic.read`, `finance.read`, `finance.charges`, `finance.collect`, `finance.reverse` |
| Administración institucional | Todos los anteriores |

La tabla resume los ejemplos originales, no la configuración vigente de cada instalación. La fuente actual es el módulo de roles. El primer gestor recibe gestión y auditoría desde un rol SQL; puede asignarse otro rol autorizado. Vincular una cuenta a una persona está descrito en `PEOPLE_AND_FAMILIES.md`.

Los perfiles de cuentas no conceden facultades familiares. Las facultades sobre un estudiante se conceden por vínculo en el módulo de personas. No representan los futuros roles de docente, familia o estudiante. Esas facultades deben autorizarse por recurso/vínculo cuando existan los módulos.

`AccountPermissions` guarda concesiones explícitas por usuario. Las políticas comprueban autenticación, cuenta activa y permiso en SQL. No se concede acceso por ocultar/mostrar menús ni por un permiso antiguo contenido en la cookie. El servicio de escritura vuelve a comprobar al actor dentro de la transacción.

## Primer gestor

En desarrollo, `./scripts/Initialize-Development.ps1 -CreateOperator` aplica migraciones y crea el **primer** gestor de accesos/auditoría. Pide usuario y contraseña; no hay valores predeterminados. Si ya existe un gestor activo, rechaza crear otro por ese procedimiento; las cuentas posteriores se invitan desde la plataforma. No modifica una cuenta existente con el mismo nombre.

`./scripts/Provision-Owner.ps1 -Environment Production` permite preparar el primer gestor de una instalación real **ya migrada**, usando la conexión del ambiente. No migra ni crea bases, no inicia el servidor y rechaza gestores existentes. Preparar antes el destino y sus migraciones según el runbook; no ejecutar contra una base desconocida. Este comando local no está expuesto por HTTP.

## Invitación y entrega

1. Un gestor entra a `/workspace/access`, pulsa «Invitar cuenta» y define usuario, perfil y motivo.
2. Se crea una cuenta pendiente, sin contraseña ni correo obligatorio, y un código de activación.
3. El código se muestra una sola vez en la respuesta de creación. El gestor entrega por un canal verificado el código y la dirección de `/Identity/Account/Activate`.
4. El titular escribe el código y su propia contraseña. La operación activa su cuenta, consume el código, invalida otros códigos pendientes y registra auditoría.
5. La página no inicia sesión automáticamente. El titular entra mediante el formulario normal.

El gestor no elige ni conoce la contraseña del invitado. La aplicación no envía correos/SMS ni otros mensajes. Los códigos se ingresan en el formulario; no se ponen en enlaces, query strings, historial, logs, listados ni auditoría. La interfaz conserva el código solo en memoria mientras su diálogo está abierto.

Cada código usa un ID aleatorio y 32 bytes criptográficos de secreto. En SQL solo se guarda SHA-256 del secreto, propósito, emisor, fechas, estado y rowversion. La comparación del hash usa tiempo constante. Un índice filtrado limita las concesiones pendientes y se anulan las anteriores antes de emitir un reemplazo.

Vigencia predeterminada: activación 1440 minutos, recuperación 30. Se pueden definir `AccountAccess:ActivationMinutes` y `AccountAccess:RecoveryMinutes` en configuración del host; la validación acepta 1…4320 y 1…60, respectivamente. No son opciones editables por el administrador.

## Recuperación asistida

El gestor identifica al titular siguiendo el procedimiento institucional; registra una nota sin copiar documentos ni datos sensibles y confirma expresamente esa verificación. El software exige la confirmación y su evidencia textual; **no certifica por sí mismo la identidad física** ni integra RENAP.

El código de recuperación se emite únicamente para una cuenta activada y habilitada. Al canjearlo se cambia la contraseña mediante Identity, se limpia el bloqueo por intentos, se consumen/anulan códigos y se actualiza el sello de seguridad. Las cookies anteriores quedan rechazadas desde la siguiente petición. Un código inválido, vencido, usado, anulado o alterado da un mensaje genérico; no revela el titular.

Una contraseña débil o una confirmación distinta no consume el código. Ni el código ni las contraseñas se vuelven a insertar en el HTML de respuesta. El mensaje de error sigue visible para poder corregir el formulario.

## Administración y concurrencia

El gestor puede cambiar perfil, suspender/habilitar, cerrar sesiones, emitir o anular códigos. Las cuentas pendientes deben activarse mediante su código; no se habilitan directamente con un cambio de estado. Suspender una cuenta cierra sesiones y anula códigos; habilitarla de nuevo no revive códigos anteriores.

Cada escritura sobre una cuenta existente incluye su `ConcurrencyStamp`. Una versión vieja devuelve 409 y no aplica el cambio. Los cambios de perfil, estado, revocación y recuperación actualizan el sello de seguridad. La cookie compara ese sello directamente en **cada petición**, sin ventana de espera ni dependencia de que avance el reloj.

Se exige conservar al menos un gestor activo y con contraseña. Una invitación pendiente no cuenta como gestor disponible. Las escrituras IAM usan una transacción SQL corta con `sp_getapplock`, exclusivo y propiedad de la transacción, para serializar la protección del último gestor y el canje de códigos entre procesos. No se hacen llamadas externas dentro de ese bloqueo. Dos canjes concurrentes producen un solo éxito; dos gestores no pueden retirarse ambos sus facultades al mismo tiempo.

## Auditoría

`audit.Events` registra actor, cuenta afectada, acción, hora UTC, motivo, detalle acotado y trace ID. Los cambios de cuentas y su evento se confirman juntos; si falla la auditoría, la cuenta/código/permisos se revierten. Se auditan también ingresos correctos, rechazados, bloqueo y salida. En un ingreso fallido el actor queda sin identificar; no se atribuye el intento al titular de la cuenta.

EF impide modificar/eliminar eventos y un trigger SQL bloquea UPDATE/DELETE incluso por SQL directo. No hay API de edición/borrado. Esto no impide a un administrador con privilegios de alterar el esquema desactivar el trigger: no se afirma resistencia criptográfica frente al dueño del servidor. La consulta requiere `audit.read`, está paginada y usa la zona horaria del colegio al presentar fechas.

## Rutas

| Ruta | Requisito |
| --- | --- |
| GET `/api/v1/access/profiles` | `accounts.manage` |
| GET `/api/v1/access/accounts?search=…&page=…` | `accounts.manage`, páginas de 20 |
| POST `/api/v1/access/invitations` | `accounts.manage` + CSRF |
| POST `/api/v1/access/accounts/{id}/profile` | Igual, versión y motivo |
| POST `/api/v1/access/accounts/{id}/state` | Igual, versión y motivo |
| POST `/api/v1/access/accounts/{id}/revoke-sessions` | Igual, versión y motivo |
| POST `/api/v1/access/accounts/{id}/codes` | Igual, versión, propósito y verificación |
| POST `/api/v1/access/accounts/{id}/cancel-codes` | Igual, versión y motivo |
| GET `/api/v1/audit?page=…` | `audit.read`, páginas de 25 |
| GET/POST `/Identity/Account/Activate` | Código de un uso, POST con CSRF |

Las respuestas de cuentas no incluyen hashes ni códigos en claro. El único retorno con secreto es la creación/emisión autorizada de un código. Las páginas Identity y API mantienen `no-store`; las páginas Identity tienen límite por IP. No hay endpoints públicos de creación arbitraria de usuarios.

## Verificación en dos pasos (implementada)

- Autoservicio en `/workspace/security`: QR (SVG generado por el servidor con QRCoder, sin servicios externos) y clave manual, confirmación con un código TOTP, 10 códigos de respaldo mostrados una sola vez, regeneración y desactivación con código vigente.
- Ingreso: tras la contraseña, una cuenta con MFA recibe solo la cookie de segundo paso y pasa por `/Identity/Account/LoginWith2fa` (código de la aplicación o de respaldo, con bloqueo por intentos). Una cuenta marcada con MFA pero sin autenticador es rechazada (Identity omitiría el segundo paso).
- Gestor: «Restablecer verificación en dos pasos» en `/workspace/access` tras verificar al titular; cierra sus sesiones.
- `AccountAccess:RequireMfaForPermissions`: permisos que solo funcionan con MFA activo (verificado en `PermissionHandler`); `/api/v1/session` devuelve `mfaPending` y el inicio muestra el aviso. Quien tiene un permiso que lo exige no puede desactivar su MFA.
- Auditoría: `MfaEnabled`, `MfaDisabled`, `MfaRecoveryCodesRegenerated`, `MfaReset`, `LoginSecondFactorRequired`, `LoginSecondFactorFailed`, `LoginRecoveryCodeUsed`.

## Pendiente

Ciclo formal de revisión de acceso/retención, permisos sobre expedientes reales y despliegue/operación de producción. Una cuenta que tenga `TwoFactorEnabled` no se autentica por el formulario simple mientras no exista el desafío; no se omite el segundo factor. No activar MFA manualmente en SQL para intentar probar una UI que aún no existe.
