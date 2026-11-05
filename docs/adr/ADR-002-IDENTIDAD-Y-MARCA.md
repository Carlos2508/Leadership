# ADR-002: acceso y marca de una instalación

Estado: base adoptada; identidad funcional todavía parcial.

El mismo host sirve Angular y las páginas de acceso. Identity guarda usuarios/credenciales en SQL Server; Razor maneja login/logout mediante cookie y antiforgery. Las cuentas son independientes del ciclo, de personas y de vínculos familiares. El primer modelo contiene `IsActive` y la política `ActiveAccount`; no contiene un modelo de membresías entre instituciones.

No hay registro público, credenciales de demostración instaladas ni importación de hashes PHP. Se dispone de un comando interactivo **solo Development** para crear una cuenta local. Ese comando no concede permisos académicos/financieros. El aprovisionamiento de operadores en producción, invitaciones, recuperación verificada, permisos y auditoría durable son trabajo posterior, obligatorio antes del uso real.

La marca se suministra como opciones tipadas, se valida al arrancar y se proyecta explícitamente al navegador. Los componentes no conocen el nombre Leadership, un teléfono ni una ruta de recursos específica de una institución. No hay API de edición administrativa ni selector de colegio. Para otra entrega se copia el proyecto y se edita el único archivo de configuración, recursos y conexión a una base nueva.

Los logos de comprobante y boleta son claves independientes desde esta base. Cuando se emitan documentos se tomará un snapshot de la configuración usada; cambiar la marca después no debe reescribir emisiones históricas.
