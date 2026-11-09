# QA de simplificación del trabajo escolar

Fecha: 8 de noviembre de 2026, America/Guatemala. Base: main `714bfc7`. Incremento en `codex/simplify-school-workflows`. Solicitud: reducir pasos que aporten facilidad conservando el rendimiento y las reglas del modelo nuevo.

| Verificación | Resultado |
| --- | --- |
| SQL Server real, suite completa | **160 aprobadas**, 0 fallos, 0 omisiones |
| Flutter, suite completa | **101 aprobadas**, análisis limpio |
| Nuevos recorridos web, escritorio/teléfono | **6 aprobados**, Axe y desbordes correctos |
| Regresión web completa | **95 aprobadas**, 0 fallos, 3 omisiones previstas |
| Asistente de migración, MariaDB/SQL aislados | **2 aprobadas**, escritorio y teléfono; resuelven dos omisiones anteriores |
| Angular / host Release | Correctos |
| SchoolDbContext / snapshot | Sin cambios pendientes |

La omisión restante es la medición de rendimiento móvil, ejecutada una vez en escritorio. La extracción sintética y el recorrido completo del asistente conservan las cuentas importadas inactivas. Incremento integrado en main por autorización vigente; sin despliegue.

## Cobertura

- Familia con dos hijos, facultades diferentes y cero facultades por defecto; fichas, vínculos, inscripción y auditoría atómicos. No crea cuentas, enlaces cuenta/persona, contraseñas o cargos.
- Reintento después de guardar con respuesta perdida; cuatro solicitudes simultáneas devuelven el mismo registro sin duplicar. UUID reutilizado con otro comando se rechaza.
- Cupo insuficiente en el segundo hijo revierte la primera inscripción, todas las fichas y auditoría del intento. El formulario conserva datos y permite corregir sección antes de confirmar otra vez.
- Reutiliza encargado/estudiante existentes con versión; conserva sus datos y facultades. Duplicados de nombre/código exigen revisión y no dejan registros parciales. Estudiante sin encargado permitido; entradas inválidas y fecha fuera del ciclo rechazadas.
- Sesión, cuatro permisos, CSRF y MFA de inscripción comprobados por HTTP. El detalle de notas no queda accesible a un docente básico por conocer la URL.
- Carnets desde un listado sin filtro de grado, elección de grado/sección y PDF real válido. Se conserva el atajo con el grado previamente filtrado en el recorrido de portería.
- Coordinación identifica curso, docente y cantidad faltante, recupera un fallo temporal y abre la libreta correcta. Los totales coinciden con publicación: cero y estado explícito Missing cuentan como capturados; período ajeno rechazado.
- Interceptor SQL: al pasar de uno a nueve cursos, mismo número de consultas; conteos correctos de grado completo/secciones y sin mezclar ciclos. El detalle de publicación se solicita solo al abrirlo.

## Incidencias corregidas

- Angular sin Zone.js no actualizaba los candidatos ni el botón tras una búsqueda asíncrona. Se notifica el cambio de la ficha al terminar búsqueda/selección; el recorrido reutiliza al mismo encargado para un tercer hijo.
- Separación visual entre nombre del curso y docente/avance, especialmente en teléfono.
- La carga de períodos retira acciones anteriores, rechaza ciclos solicitados desconocidos, ignora respuestas fuera de orden y ofrece reintento sin presentar un fallo como ciclo vacío.

## Evidencia y límites

Logs/TRX en `.local/qa/test-results/workflow-*`; capturas revisadas en `.local/qa/evidencia/school-workflows-2026-11-08/targeted/`, junto con `full-suite/` y `migration/`. Host local actualizado en `.local/publish/workflows/`. Datos exclusivamente sintéticos; fixtures SQL con nombre aleatorio y retiro limitado a sus propias bases. No se modificaron PHP, MariaDB escolar ni datos de la instalación.

Nueva migración aditiva `20261109043528_FamilyRegistrationReceipts`: aplicada solo a fixtures, pendiente actualización normal de la instalación. El asistente familiar es web adaptable a teléfono; el app conserva sus flujos anteriores y se comprobó regresión Flutter. No hay pantalla nativa equivalente nueva ni distribución APK nueva en este incremento. No se implementan políticas pendientes de asistencia diaria, recibo conjunto o cobro automático, ni la migración de currículo. No hubo despliegue ni mensajes externos; las pruebas no garantizan ausencia absoluta de errores.
