# Revisión completa de flujos y experiencia

Fecha: 5 de noviembre de 2026, America/Guatemala. El propietario pidió repetir todos los flujos y mejorar la experiencia de uso. Este incremento continúa sobre el trabajo local documentado en `QA_FLUJOS_2026-11-05.md`.

## Incidencias y mejoras

| Caso | Hallazgo reproducido | Resultado |
| --- | --- | --- |
| EX-001 | Cambiar la fecha de asistencia descartaba marcas y observaciones sin avisar. | La navegación de fecha/curso y la salida protegen cambios pendientes. Se puede seguir editando o confirmar el descarte. Una salida durante el guardado espera su confirmación. Cancelar devuelve también el campo Fecha al día correcto. |
| EX-002 | Una lectura fallida de asistencia conservaba filas de otro día; las respuestas tardías podían reemplazar el contexto. | Fecha explícita en URL, historial, retiro de datos anteriores, contador de solicitudes y reintento visible. No se presentan las marcas de otra fecha como vigentes. |
| EX-003 | Inscripciones no restauraba el ciclo al usar Atrás/Adelante y perdía el contexto de filtros. | Ciclo, grado, sección, estado, búsqueda y página viven en la URL. El historial los restaura; Limpiar filtros conserva el ciclo. |
| EX-004 | Fallar el nuevo ciclo de matrícula dejaba registros y acciones anteriores visibles sin un error recuperable. | Se retiran filas y acciones, se muestra el error y se puede reintentar. Una respuesta anterior no reemplaza el ciclo actual. Un ciclo desconocido no se sustituye por otro. |
| EX-005 | Al vencer la sesión durante una consulta, el ingreso perdía el ciclo/filtros de la URL. | El regreso conserva ruta, query y fragmento. Prueba con expiración real de cookies y nuevo ingreso, sin simular la autenticación. |

Los tres recorridos de asistencia y los de matrícula/sesión fallaron antes de las correcciones, en escritorio y móvil. Las 12 ejecuciones nuevas aprobaron después de corregirlos. Un ajuste adicional de detección de cambios fue necesario para que cancelar el cambio de fecha actualizara también el control visible en Angular.

Mejoras de uso:

- Asistencia distingue Sin registrar, Registro parcial, Registrada y Cambios sin guardar. Los totales reflejan lo marcado en pantalla. Guardar se habilita cuando hace falta registrar o hay cambios; los controles se bloquean durante el envío. Un error se muestra junto al formulario y conserva marcas y observaciones.
- Los botones de día anterior/siguiente avanzan un día calendario; no se impone silenciosamente un calendario lectivo de lunes a viernes. La API conserva sus reglas de fecha, autorización y versiones.
- Selector de fecha y resumen con espaciado de formulario y ancho adaptable. Comprobados en 320/768/1920 px, además de escritorio/móvil habituales, con Axe, campo de fecha legible y sin desbordamiento horizontal.
- Caja explica una búsqueda sin coincidencias y qué puede probarse. Al cambiar el texto retira resultados anteriores; una respuesta cuyo término ya cambió se descarta. Un fallo de consulta no se interpreta como búsqueda vacía.
- El estado vacío de matrícula diferencia «sin inscripciones» de «sin coincidencias con estos filtros» y orienta a limpiar la búsqueda.

## Cobertura de la revisión

| Área | Integración SQL y reglas | Recorrido de navegador |
| --- | --- | --- |
| Cuentas, roles, MFA y auditoría | Identity, AccountAccess, AccessRole, Mfa | Invitación, activación, recuperación, roles, MFA y recuperación de sesión |
| Personas y familias | People | Alta, directorios escolares, vínculos, facultades y acceso familiar |
| Ciclos y matrícula | Academic | Estructura, cupos/ubicaciones, inscripción, historial y errores de contexto |
| Cursos y docentes | Course | Curso/sección, asignación y lista autorizada del docente |
| Notas | Assessment, Grade | Captura, pegado/autoguardado, publicación, correcciones y retención por solvencia |
| Finanzas | Finance, Dashboard | Cargos, vista previa, cobro, recibo, reversión, ajuste, solvencia y cambios de ciclo |
| Tareas y archivos | Learning | Publicación, entrega, adjuntos y retroalimentación |
| Asistencia | Attendance | Marcas, notas, fechas, guardado fallido/lento, historial y portal |
| Avisos | Communication | Audiencia, publicación y lectura |
| Carnets y portería | Gate | Emisión, lectura, duplicado, salida manual, reporte, dispositivos y facultades |
| Pública y límites | PublicBoundary | Marca configurada, ingreso, acceso anónimo rechazado, error inicial, responsive y rendimiento |
| Migración limitada | LegacyImport y prueba de migración de roles | Datos sintéticos de fixtures; no se ejecuta extracción/importación del PHP |
| Flutter | 68 pruebas y análisis estático | Widgets de perfiles, tamaños, navegación y cliente del lector; no se prueba hardware físico |

La revisión automatizada cubre los flujos implementados y sus casos de autorización/concurrencia en las suites. No convierte en implementadas las pantallas de muestra ni certifica equipos físicos, operación o políticas pendientes.

## Validación

- **89 pruebas .NET aprobadas**, sin omisiones, con SQL Server real.
- **68 pruebas Flutter aprobadas** y `flutter analyze --no-pub` sin observaciones, usando el SDK fijado del repositorio.
- Build Angular correcto; advertencia conocida SweetAlert2 CommonJS.
- Nuevos recorridos: 12 aprobados. **Regresión completa: 75 E2E aprobados, 1 omisión prevista de rendimiento móvil, 0 fallos**, en una corrida de 76 casos.
- Tras el último ajuste visual: **6 E2E adicionales aprobados**, incluidos cambio de día calendario, guardado/error/historial y revisión responsive con Axe. Capturas finales de escritorio/móvil y 320/768/1920 px revisadas.
- Consulta final de SQL Server: **0 bases temporales Test/E2E**. `git diff --check` correcto. No se requirió cambiar reglas o esquema backend.

Evidencias en `.local/qa/evidencia/experience-2026-11-05/`, fuera de la carpeta que Playwright vacía. Las pruebas usan exclusivamente bases `SchoolPlatform_Test_*`/`SchoolPlatform_E2E_<uuid>` de sus fixtures y datos sintéticos. No se cambia el PHP, MariaDB, Numix o los registros de desarrollo; no hay migración de esquema nueva. Capturas MFA enmascaradas y sin trazas de sus secretos.
