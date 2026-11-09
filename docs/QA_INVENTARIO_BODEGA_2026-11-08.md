# QA del inventario y bodega del colegio

Fecha: 8 de noviembre de 2026, America/Guatemala. Solicitud: terminar `feat/school-store-inventory`, orientarlo a bodega escolar y hacer merge/push a main. Base inicial `11b41c9`; implementación `6442cf4`; integración de `origin/main` `5fdc9fb` en `7faaffa` para conservar las correcciones de revisión de flujos, carnets, libreta y app docente.

| Verificación | Resultado |
| --- | --- |
| SQL Server, suite completa combinada | **150 aprobadas**, 0 fallos, 0 omitidas |
| Flutter, suite completa combinada | **101 aprobadas**, análisis sin observaciones |
| Web escritorio/teléfono, versión final combinada | **89 aprobadas**, 0 fallos y 3 omisiones previstas |
| Asistente, MariaDB/SQL reales aislados | **2 aprobadas**, escritorio/teléfono; extracción sintética correcta |
| Angular / host Release / APK Android debug | Correctos |
| SchoolDbContext respecto al snapshot | Sin cambios pendientes |

La suite web completa contiene 92 casos. Las dos omisiones del asistente se ejecutaron correctamente aparte con su fixture MariaDB aislado; la restante mide rendimiento móvil, comprobado una vez en escritorio. No quedaron fallos pendientes en el alcance probado.

## Flujos y restricciones

- Alta con stock inicial y su movimiento en la misma transacción, entradas, salidas, conteo físico hasta cero, archivo/reactivación, historial conservado, búsqueda y stock bajo. Cuantificación por unidades enteras; existencias independientes del ciclo escolar.
- Reintento de alta y movimientos sin duplicar, cinco intentos concurrentes idénticos, reintento después de otras operaciones y rechazo del mismo UUID con distinto comando. Dos salidas simultáneas sobre la misma versión no generan saldo negativo; exceso de salida/capacidad no modifica historial. SQL comprueba límites y aritmética; trigger y EF impiden modificar/borrar movimientos.
- Cuentas anónimas/sin acceso/solo consulta y CSRF. MFA específico para carga inicial y conteo; no se admite Count por la ruta de entradas/salidas. Los permisos se conceden explícitamente mediante el editor SQL de roles y no por nombre del cargo o rol.
- CSV respeta filtros, orden, máximo de 10,000 artículos y neutralización de fórmulas. Auditoría y movimiento se guardan con actor, fecha, motivo, referencia y saldo anterior/posterior.
- Flutter: alta de artículo con unidades iniciales, respuesta perdida después de descontar/reintento con el mismo UUID, conteo a cero, cuenta de consulta, fallo de actualización sin mostrar stock anterior y pantallas 320 px / texto 1,5 / tema oscuro.
- Navegador: carga inicial, salida confirmada en servidor con respuesta perdida, reintento único, rechazo de salida insuficiente, conteo cero, CSV, archivo con historial y consulta de archivados. Respuestas tardías/filtros fallidos no recuperan datos anteriores. Axe y desbordes revisados en los recorridos; el teléfono abre el detalle al seleccionar el artículo y permite volver a la lista.

## Incidencias resueltas durante la revisión

1. Etiqueta del selector Estado: nombre accesible explícito para no incorporar el texto de sus opciones. Prueba de consulta/archivo y Axe.
2. Botones del inventario sin estilo del sistema: clases comunes button/outline. Navegación de detalle en teléfono para no obligar a bajar toda la lista del catálogo.
3. Fallo previo del chat detectado en la primera regresión (88 casos correctos, uno fallido y tres omisiones previstas): el poll borraba el error de envío y ocultaba Reintentar con un intento aún pendiente. Se conserva el aviso hasta confirmar el mensaje; corregido en web y app. Las pruebas esperan explícitamente un poll antes de reintentar. La suite final se repite sobre la integración y el build actualizados.

## Evidencia y límites

TRX/logs ignorados en `.local/qa/test-results/`: `inventory-merged-20261108.trx`, `inventory-flutter-merged-20261108.log`, `inventory-web-merged-20261108.log`, `inventory-android-20261108.log`, `inventory-release-20261108.log`, `inventory-migration-20261108.log`. Capturas finales de escritorio/teléfono revisadas en `.local/qa/evidencia/inventory-bodega-2026-11-08/`, junto con `suite-completa/`; APK `mobile/build/app/outputs/flutter-apk/app-debug.apk`, host local `.local/publish/inventory-bodega/`.

Todos los datos son sintéticos y cada fixture posee exclusivamente su catálogo `SchoolPlatform_Test_*` / `SchoolPlatform_E2E_<uuid>`. No se detuvieron procesos ajenos ni se modificaron PHP/MariaDB o bases escolares. La migración aditiva `20261107004645_SchoolStoreInventory` conserva su identificador de preparación; su alcance final es inventario general de bodega. Se aplica mediante la actualización normal de la instalación, antes de conceder los permisos nuevos. Guía: `INVENTARIO_BODEGA.md`; decisión: ADR-018.

El APK acredita compilación e integración nativa; no sustituye una prueba física de biometría ni una compilación iOS, pendientes del incremento anterior. No se realizó despliegue o distribución. Estas verificaciones no certifican ausencia absoluta de errores.
