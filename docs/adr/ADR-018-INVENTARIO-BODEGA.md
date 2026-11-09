# ADR-018: inventario y bodega del colegio

Fecha: 8 de noviembre de 2026, America/Guatemala. Solicitud del propietario: terminar el inventario, integrar y publicar en main; aclaración expresa de uso para bodega del colegio.

## Alcance y operación

No existía un módulo de inventario conectado. El propietario aclaró que se necesita para controlar la bodega del colegio. El catálogo admite útiles, materiales de limpieza, papelería e insumos que se cuentan por unidades. Categoría y ubicación son libres (por ejemplo, Bodega central · Estante A). Un catálogo por instalación, sin tenants ni datos de muestra en producción.

Artículos con código único normalizado, nombre, categoría, ubicación, stock mínimo y estado. Unidades enteras de 0 a 1,000,000 por artículo. Alta con stock inicial opcional, entradas, salidas y conteos físicos; búsqueda/paginación, aviso de stock bajo/agotado, historial y exportación CSV desde web. Web Bodega → Inventario (`/workspace/inventory`); app conectado, Colegio → Inventario y bodega. Interfaz y colores del sistema existente.

Las existencias no son matrículas ni un registro académico anual: se conservan al cambiar el ciclo y no usan un ciclo de sesión. Cada movimiento tiene fecha del servidor, cuenta, motivo, referencia opcional, secuencia del artículo y saldo anterior/posterior. No se convierte automáticamente una salida en venta, cobro o deuda; esos procesos requieren un alcance financiero explícito. No implica préstamos bibliotecarios.

## Integridad

Esquema `inventory.Products` / `inventory.Movements`, migración aditiva `20261107004645_SchoolStoreInventory`. El saldo es una proyección actualizada junto al movimiento y auditoría en una transacción serializable. Bloqueos SQL por intento/artículo y bloqueo de catálogo para unicidad; rowversion impide confirmar sobre información obsoleta. Una salida sin unidades suficientes se rechaza sin tocar saldo ni historial. No hay stock negativo ni borrado de artículos/historial; archivar exige saldo cero y reactivar conserva movimientos.

Altas y movimientos tienen UUID de intento y huella de su comando normalizado. Un reintento idéntico devuelve el artículo actual sin repetir stock, incluso después de otros movimientos; reutilizar el intento con otro contenido/cuenta se rechaza. El saldo inicial también tiene movimiento y se guarda atómicamente con su ficha. EF y un trigger SQL impiden modificar/eliminar movimientos. Las restricciones SQL comprueban cantidades, secuencia, tipo y continuidad aritmética dentro de cada registro.

La interfaz congela el comando cuando una respuesta es incierta y permite reintentar con su mismo UUID. No reemplaza silenciosamente el intento por otro. Un conflicto confirmado permite actualizar existencia y revisar; recargar una ficha para editarla exige confirmar que se reemplazarán los campos con los datos actuales. Se protegen cambios sin guardar. Las respuestas tardías de consultas no sobrescriben una consulta más reciente; al fallar se retiran los datos anteriores.

## Autorización y actualización

Tres acciones del catálogo (ADR-012): `inventory.read` para consultar/exportar, `inventory.move` para entradas/salidas, `inventory.manage` para fichas, archivo/reactivación y conteos. Escrituras también exigen lectura. Todas las APIs comprueban ActiveAccount, permiso SQL y CSRF; los servicios vuelven a exigir permisos durables. La cantidad inicial exige además el permiso/política MFA de movimientos. Conteo tiene endpoint/política propios y no puede enviarse por la ruta de entradas/salidas para eludir MFA.

Los permisos nuevos se seleccionan en Roles y permisos; no se conceden por nombre de rol, cargo, perfil escolar o por actualizar el código. No se amplían silenciosamente las cuentas/roles existentes. La auditoría general incorpora el área Inventario y bodega. CSV acota exportaciones a 10,000 artículos y neutraliza fórmulas en celdas de texto.

Aplicar migraciones por el procedimiento normal de actualización, con respaldo y configuración de la instalación. Se ensaya solo en catálogos SQL propios del fixture. Sin cambios en PHP/MariaDB, importación de datos reales o despliegue.
