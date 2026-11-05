# ADR-003: operaciones de acceso verificables

Estado: implementado en el segundo incremento. Fecha: 4 de noviembre de 2026, America/Guatemala.

Las fuentes señalan muchos encargados sin correo y la necesidad de separar cuenta, identidad escolar y facultades. Se adopta activación/recuperación mediante código de un uso entregado manualmente por personal con permiso. No se asume correo ni se envían mensajes automáticamente.

El estado durable del código vive en SQL y el secreto se conserva únicamente como hash. La pantalla autorizada lo recibe una sola vez; el formulario del titular lo recibe por POST con antiforgery. No se incluyen tokens en URLs. La verificación de titular se registra como actuación institucional; el booleano de confirmación no pretende sustituir una verificación real.

Los permisos iniciales son `accounts.manage` y `audit.read`, con alcance sobre las cuentas de la única instalación. No son roles de familias/docentes ni un sistema de tenants. Se conceden en SQL y se consultan al autorizar; una concesión obsoleta en el navegador no da acceso.

Se escoge una transacción con bloqueo SQL de aplicación para las pocas operaciones de gestión de acceso. Resuelve la protección del último gestor y la exclusión de canjes duplicados entre procesos sin añadir Redis. Una actualización de cuenta/código y su auditoría se confirman o revierten juntas. Los eventos se protegen contra UPDATE/DELETE en EF y SQL, con el límite explícito de que el administrador del esquema puede alterar esos controles.

Las cookies comprueban el sello durable directamente por petición. La alternativa de solo configurar un intervalo cero no garantiza validar si el reloj no avanza entre peticiones; una prueba con reloj fijo demostró esa frontera. La comparación directa evita esa ventana y se prueba con cookies emitidas antes de recuperar/cerrar sesiones.

Se sigue el modelo de [sello de seguridad de Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0) y [transacciones de EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions). No se habilitaron reintentos implícitos de EF que repitan operaciones externas; la gestión de códigos no ejecuta envíos ni llamadas de proveedor dentro de la transacción.

El primer gestor se provisiona por consola contra una base ya migrada; Development también conserva preparación cómoda local. No se crea un usuario privilegiado por el mero arranque de la web. El MFA completo y la operación de producción siguen pendientes de sus flujos y aceptación.
