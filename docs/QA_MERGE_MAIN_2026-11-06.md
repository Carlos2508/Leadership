# QA de integración en main

Fecha: 6 de noviembre de 2026, America/Guatemala.

Integración solicitada por el propietario: `feat/legacy-migration-2027` (`23a1bad`) sobre `main` (`2ab3a13`). Ambos partían de `12c9c0d`. Solo conflictuaron `PROGRESS.md` y `HANDOFF.md`; se conservaron ambas entregas. La gestión académica, primeros pasos y estilos nuevos de main permanecen junto al asistente local de migración.

| Validación del conjunto integrado | Resultado |
| --- | --- |
| Suite completa .NET con SQL Server real | **127 aprobadas, 0 fallos, 0 omitidas** |
| Suite Playwright completa, escritorio y teléfono | **83 aprobadas, 0 fallos, 3 omisiones previstas** |
| Migración con MariaDB/SQL reales sintéticos, escritorio y teléfono | **2 aprobadas, 0 fallos**; ejecutan las dos omisiones de migración de la suite normal |
| Rendimiento | Aprobado en escritorio; única omisión restante en móvil porque se mide una vez |
| Extractor PHP y detección de configuración literal | **7 + 4 escenarios aprobados** |
| Flutter | **74 pruebas aprobadas** y `flutter analyze` sin observaciones |
| Compilación Angular, Flutter web y publicación Release | Correctas; asistente actualizado en `.local/migration-tool/` |
| Modelo School frente al snapshot de EF | Sin cambios pendientes |
| Recorrido desde un colegio vacío por seis perfiles | **49/49 en escritorio y 49/49 en teléfono** |
| Limpieza de recursos propios | **0 bases temporales creadas hoy y 0 respaldos de migración E2E restantes** |

La suite web cubre ingreso, activación, recuperación, MFA, roles, auditoría, personas/familias, directorios, ciclo y períodos, cursos/asignaciones, matrícula/cambios, caja/recibos/solvencia, asistencia, tareas/entregas, notas/publicaciones, avisos/campana, portería y recuperación de sesión. Incluye navegación por ciclo/historial, respuestas lentas y fallidas, conservación de cambios y reintentos. Axe y comprobaciones de desborde forman parte de los casos; también se revisaron capturas de escritorio y teléfono de formularios, panel, asistente y app.

El recorrido adicional usa cuentas sintéticas de Dirección, Secretaría, Caja, Docente, Estudiante y Familia y prepara grados/ciclo/períodos/cursos desde una base vacía. Registra/vincula/inscribe estudiantes y madre, cobra, toma asistencia, captura/publica notas, entrega/revisa una tarea, publica un aviso y registra portería. Comprueba notas disponibles para una estudiante con pago y retenidas para el hermano. La app compilada entra con cuentas reales del fixture; en teléfono se verifican también respuestas correctas de autenticación y consultas académicas, libreta docente y notas de la estudiante. No sustituye pruebas en un dispositivo físico.

Los dos HTTP 401 observados al cambiar el rol de la propia cuenta corresponden al cierre de sesiones exigido por IAM; el reingreso funciona. La comprobación del recorrido los distingue de errores inesperados. Se corrigieron coordenadas antiguas del script local de QA de Flutter y se añadieron comprobaciones de respuestas, para verificar que abre el detalle y la libreta. No fue necesario cambiar el código de producto tras integrar las ramas.

La migración leyó una instancia MariaDB independiente con credenciales SELECT y cargó otra base SQL temporal: ciclo 2027 explícito, grado/sección por grupo, ceros de códigos, familias compartidas, estudiante sin encargado identificable, guardado/reanudación, respaldo `COPY_ONLY/CHECKSUM` verificado, importación y conciliación. Las cuentas terminan inactivas, sin contraseña ni códigos; no se trasladan notas, dinero ni credenciales antiguas. Los 127 casos SQL incluyen reintentos, concurrencia, rollback y controles de permisos del importador.

Los primeros intentos de compilación toparon con binarios en uso por otro host y por el testhost; se repitieron con salidas independientes. No se detuvo el host de otra sesión. Las compilaciones conservan avisos no bloqueantes de optimización CommonJS de SweetAlert2 y de familia de fuente Cupertino del SDK Flutter; no hubo errores de compilación ni se detectaron glifos ausentes en las pantallas inspeccionadas.

Reproducción: `dotnet test SchoolPlatform.slnx`, `./scripts/Invoke-Web.ps1 run build`, `./scripts/Invoke-Web.ps1 test`, `./tests/migration/Test-LegacyExtractor.ps1 -RunAssistantE2E`, PHP `tests/migration/exporter-test.php`, `flutter analyze` y `flutter test`. Si otro host usa los binarios o el puerto, establecer `BaseOutputPath` y `SCHOOLPLATFORM_E2E_PORT` propios antes de iniciar. Las pruebas SQL requieren el acceso local indicado en `AGENTS.md`.

Evidencia local ignorada: `.local/qa/test-results/merged-full.trx`, `.local/qa/evidencia/merge-main-2026-11-06/web/` (159 PNG), `.local/qa/evidencia/migration-assistant/{desktop,mobile}/` (cuatro estados por tamaño), `.local/qa/merge-journey/` (resultados JSON, capturas y ejecutor privado). No se guardaron contraseñas ni códigos en los reportes.

No se leyó el padrón real, no se modificaron PHP/MariaDB escolares ni se aplicaron migraciones a la base escolar. Solo se retiraron recursos aleatorios de los fixtures actuales; los históricos ajenos se conservaron. No hubo despliegue ni corte de datos. El resultado describe lo probado y no garantiza ausencia absoluta de defectos ni valida hardware/instalación de producción.
