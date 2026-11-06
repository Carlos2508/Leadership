# QA del importador inicial de estudiantes/familias

Fecha: 6 de noviembre de 2026, America/Guatemala. Rama: `feat/legacy-migration-2027`.

Esta evidencia utiliza exclusivamente datos sintéticos. No hubo conexión a MariaDB del legado, lectura de sus fichas reales, modificaciones de PHP, importaciones escolares reales ni despliegue. Se leyeron archivos fuente del legado para contrastar estructura y se ejecutó **otra instancia MariaDB** desde los binarios instalados, con `--no-defaults`, directorio aleatorio propio y puerto de loopback aleatorio.

## Resultados

| Comprobación | Resultado |
| --- | --- |
| Suite completa .NET con SQL Server real | **117 correctas, 0 fallos, 0 omitidas**; incluye 16 pruebas nuevas del padrón |
| Validación específica después de los ajustes finales | **17 correctas, 0 fallos, 0 omitidas** (16 del padrón + compatibilidad v1), incluyendo confirmación de base, BOM UTF-8 y consola sin logs de framework |
| Contrato del extractor PHP con consultas/control inyectados | **7 escenarios correctos**; ninguna conexión de base en esa prueba |
| Extractor y borrador contra MariaDB real aislado | Correctos: ciclo fuente, códigos con ceros, hermanos, ficha de dos adultos, familia de otro ciclo, referencia rota, sección `null`, filas históricas separadas, credenciales antiguas excluidas, usuario SELECT sin escritura y ciclo desconocido rechazado |
| Publicación Release del host | Correcta; artefacto local ignorado en `.local/migration-tool/` |
| Sintaxis/documentación | Cuatro scripts PowerShell sin errores de parseo, PHP sin errores de sintaxis, enlaces locales y `git diff --check` correctos |
| Migración School | Generada `20261106202522_InitialRosterImport`, aplicada por los fixtures SQL; snapshot sin diferencias pendientes. No aplicada a la base escolar/desarrollo |
| Datos personales/credenciales reales en pruebas | Ninguno; fixtures inventados y credenciales sintéticas en memoria/entorno |

Las primeras corridas detectaron y corrigieron: una huella basada en contador global SQL que invalidaba reintentos tras fallos, un fixture que intentaba cerrar el ciclo sin períodos, y una expansión de propiedad de diccionario incorrecta en el generador PowerShell. Se corrigieron antes de los resultados aprobados. La primera corrida SQL dentro del sandbox no pudo negociar cifrado; la suite aprobada se ejecutó con acceso a SQL local y sus catálogos aleatorios propios, sin reducir las garantías de conexión ni sustituir SQL Server.

## Escenarios del padrón y consola

Implementados en `tests/SchoolPlatform.Tests/InitialRosterSqlTests.cs`:

| Caso | Evidencia comprobada |
| --- | --- |
| Plan de solo lectura | No crea personas, cuentas, lotes, códigos ni auditoría; repetir plan conserva su huella |
| Importación completa | Tres estudiantes, dos adultos, tres vínculos, matrícula del GUID/ciclo/fecha/grado exactos y cinco cuentas inactivas sin contraseña/códigos/permisos administrativos |
| Exclusiones | Un candidato excluido no produce persona ni matrícula; pendientes bloquean el lote entero |
| Personas y familia | Hermanos comparten adulto; una ficha familiar se divide en dos adultos revisados; estudiante sin encargado; referencia rota requiere decisión expresa |
| Perfiles combinados | La misma persona estudiante/encargado recibe una cuenta y un enlace, conservando ambos perfiles |
| Datos inválidos | 26 variantes en el caso de validación: IDs/personas/usuarios/códigos/alias/vínculos repetidos, nombres/fechas/contactos inválidos, perfiles desconocidos, facultades o contraseñas/códigos prohibidos, familias o revisión pendientes |
| JSON estricto | Campos requeridos ausentes, propiedades desconocidas/repetidas, elementos nulos, raíz inválida y modo mal escrito se rechazan; UTF-8 con BOM se acepta conservando hash de bytes originales |
| Destino | Etiqueta de ciclo distinta, fecha fuera del calendario, grado/sección inexistente, ciclo cerrado y rol con permisos se rechazan |
| Operador | Contraseña errónea, usuario inexistente, falta de permisos y MFA ausente/inválido se rechazan; TOTP válido permite plan y apply |
| Hash/estado | Paquete cambiado o destino cambiado requieren otro plan; un lote aplicado no acepta otros bytes |
| Atomicidad | Fallo inyectado después de insertar cuentas Identity revierte también esas cuentas, personas, enlaces y lote; el mismo scope puede volver a planificar/aplicar |
| Concurrencia | Dos apply simultáneos crean un solo lote; una inscripción normal de la web y el importador no exceden el cupo de una sección |
| Cupo de lote | Se cuenta el conjunto de filas del paquete, además de plazas ocupadas en destino |
| Activación gradual | Cuenta pendiente falla al ingresar; código individual habilita solo al titular; otro estudiante responde 404; repetir el lote conserva la activación y la informa |
| Ausencia de arrastre | Sin publicaciones de notas, cargos, pagos, tareas, asistencia, carnets o dispositivos creados por la importación |
| Conflictos existentes | No fusiona por nombre/código; código, usuario y alias existentes se rechazan; confirmar personas distintas no salta conflictos de identificadores |
| Volumen | 150 alumnos sintéticos importados y conciliados con cuentas pendientes; no equivale a prueba del máximo de 5.000 personas |
| Consola real | Proceso independiente `dotnet ... --import-roster` ejecuta plan → apply → reconcile contra el catálogo SQL del fixture; errores salen con código no cero y sin stack/secretos |
| Confirmación de base | La consola exige `Roster:Database` coincidente con la conexión final; un nombre distinto se rechaza antes de importar |
| Convivencia v1/v2 | El comando anterior no acepta el JSON nuevo ni reimporta una fuente que ya tiene un lote v2 |

La aplicación usa transacción compartida Identity/School y bloqueos IAM/matrícula. Los fixtures verifican el nombre `SchoolPlatform_Test_<uuid>` antes de migrar y solo eliminan el catálogo que generaron. Las pruebas no usan ni copian la base escolar. No se detuvo ningún host ajeno.

## Reproducir

Desde el worktree, con SDK fijado y SQL local disponible o `SCHOOLPLATFORM_TEST_SQL`:

```powershell
dotnet restore SchoolPlatform.slnx --locked-mode
dotnet test tests/SchoolPlatform.Tests/SchoolPlatform.Tests.csproj --no-restore

# Suite específica:
dotnet test tests/SchoolPlatform.Tests/SchoolPlatform.Tests.csproj --no-restore `
  --filter 'FullyQualifiedName~InitialRosterSqlTests|FullyQualifiedName~LegacyImportSqlTests'

# Contrato del extractor, sin DB:
C:/xampp/php/php.exe tests/migration/exporter-test.php

# MariaDB real, instancia sintética propia que se retira en finally:
./tests/migration/Test-LegacyExtractor.ps1
```

`-MariaDbBin`/`-PhpPath` permiten otras ubicaciones de binarios en la prueba MariaDB. No instala servicios ni usa el directorio de datos del legado. La prueba prepara su esquema/lector SELECT, genera el raw y borrador, comprueba rechazos y retira únicamente su proceso/directorio aleatorio. Las carpetas temporales/artefactos se mantienen ignorados.

Evidencia local: `.local/qa/test-results/migration-validation.trx` (suite completa) y `migration-final.trx` (validación específica posterior). No hay capturas o interfaz nueva en este incremento, por lo que no se ejecutaron E2E visuales de Angular/Flutter. Las cookies/autorización se comprobaron por HTTP en las pruebas SQL y por la consola real.

## Qué falta para operar con el padrón real

El propietario debe configurar el catálogo/secretos de su instalación, aplicar la migración allí, verificar respaldo, usar una credencial de lectura del legado, revisar su padrón y ejecutar plan/apply/reconcile. Los motores/esquema y las ambigüedades de los registros reales se validarán durante ese procedimiento; no se certifican por haber pasado fixtures sintéticos. No se puede garantizar ausencia absoluta de bugs ni anticipar todos los datos posibles. El importador rechaza inconsistencias y permite reintentar sin duplicar o desactivar cuentas ya habilitadas.
