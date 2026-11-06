# ADR-012: roles de acceso configurables por instalación

Fecha: 5 de noviembre de 2026, America/Guatemala. Estado: implementado.

## Solicitud y separación de conceptos

El propietario aclaró que los perfiles escolares de estudiante, docente y padre/encargado permanecen separados. Pidió crear roles de acceso como Director o Secretaría y poder asignarles cualquier conjunto de permisos, incluso todos, sin restricciones por su nombre.

- `iam.AccessRoles`, `AccessRolePermissions` y `AccountRoleAssignments` guardan el rol, sus permisos y la asignación. En este incremento cada cuenta tiene un rol de acceso. Los perfiles de personas, asignaciones docentes, facultades familiares y matrículas no se convierten en roles ni se cambian al asignar uno.
- `/workspace/roles` permite crear, renombrar, describir, editar permisos y desactivar roles sin cuentas asignadas. El editor agrupa acciones y permite seleccionar todos. Cuentas usa ese catálogo SQL para asignar el rol; el nombre no determina su alcance. No hay un rol especial inmutable por llamarse Director o Secretaría.
- Los identificadores de permisos representan acciones implementadas y protegidas en el servidor; `PermissionCatalog` documenta esas acciones. El usuario configura su composición en roles, no crea identificadores arbitrarios que la API no podría ejecutar. Se eliminó el catálogo de perfiles y su resolución por coincidencia de permisos en tiempo de ejecución.
- `iam.AccountPermissions` sigue como proyección efectiva durable consultada por los endpoints existentes. Editar un rol actualiza sus permisos, los de sus cuentas, sellos de seguridad y auditoría en la misma transacción. Las cookies anteriores dejan de ser válidas desde la siguiente petición. No se confía en nombres de rol ni menús ocultos para autorizar.
- Los cambios usan el mismo bloqueo SQL `SchoolPlatform.AccountAccess` de las operaciones IAM. Se rechaza quitar la gestión al último gestor activo, usar versiones obsoletas, permisos desconocidos o nombres repetidos. Un rol asignado no se puede desactivar hasta reasignar sus cuentas. Se exige `accounts.manage`, incluido MFA cuando corresponda, CSRF y motivo.
- La migración `ConfigurableAccessRoles` conserva los accesos previos exactamente. Si coinciden con un ejemplo inicial, asigna ese rol; si no, crea un rol conservado con ese conjunto preciso, sin elevarlo a una plantilla más amplia. Los ejemplos iniciales se insertan una vez en la migración y son editables; no se siembran al arrancar.
- El primer gestor se sigue preparando por consola, sin contraseña predeterminada ni usuario automático. Se elige en SQL un rol activo que permita gestión de cuentas y auditoría; no se usa un nombre para conceder permisos.

La clave `profile` de los comandos de cuentas se conserva como compatibilidad de transporte y contiene ahora la clave de un rol SQL. No vuelve a representar una lista fija en código. Esta decisión complementa ADR-003; no cambia políticas de minoría de edad o custodia ni concede vínculos por parentesco.
