# ADR-019: alta familiar y atajos operativos

Estado: implementado en el incremento de simplificación. Fecha: 8 de noviembre de 2026, America/Guatemala.

## Contexto

El propietario autorizó reducir pasos del trabajo escolar, conservando el modelo nuevo y el rendimiento. El registro de una familia con varios hijos exigía alternar personas, vínculos e inscripciones. Los carnets solo aparecían al filtrar un grado; publicación mostraba el total de notas faltantes sin indicar los cursos.

## Decisiones

- Web `/workspace/new-family`: encargados → estudiantes/inscripción → revisión. Admite hasta cuatro encargados y ocho estudiantes por operación; permite reutilizar fichas activas por búsqueda, sin modificarlas. No introduce una entidad hogar ni fusiona personas. El ciclo y la fecha son explícitos. Los formularios individuales siguen disponibles.
- Una transacción serializable conserva fichas, vínculos, facultades, inscripciones, auditoría y comprobante del intento. Comparte el bloqueo de matrícula con las inscripciones individuales y el importador para respetar cupos concurrentes. Si un hijo falla, no queda media familia registrada.
- UUID, actor y huella del comando permiten reintentar una respuesta perdida sin duplicar ni restablecer registros posteriores. El comprobante solo conserva identificadores, no contactos. La web retiene en memoria el mismo comando mientras no se confirma su resultado y protege la salida con cambios; no guarda datos personales en almacenamiento del navegador.
- Exige conjuntamente `people.read`, `people.manage`, `academic.read` y `enrollment.manage`, con ActiveAccount, MFA por cada permiso y CSRF. El servicio vuelve a comprobar permisos SQL. Los parentescos no conceden facultades: inicialmente vacías, selección explícita por encargado y personalización por hijo. Un vínculo existente se reutiliza únicamente si coincide; los cambios de facultad siguen en la ficha ordinaria.
- No crea cuentas ni cargos. Activación, vínculo cuenta/persona y revisión de permisos conservan sus operaciones y autorizaciones propias. Un estudiante previamente inscrito en el mismo ciclo se gestiona desde inscripciones; no se mueve silenciosamente.
- Carnets: un diálogo permite grado, sección y formato sin cambiar los filtros de la lista. Reutiliza generación PDF/autorización vigentes.
- Publicación: consulta bajo `academic.read`, solicitada solo al abrir el detalle, muestra cursos con estudiantes sin nota ni estado. Reutiliza exactamente la regla de pertenencia fechada de la publicación y abre la libreta del curso/período. Cero y estado pendiente/exonerado cuentan como registro, no se reinterpretan como ausencia de captura.
- Listas de cursos: conteo SQL agrupado para todos los cursos seleccionados, en lugar de una consulta por curso. Se conservan año, grado, sección y condición de matrícula activa/ubicación abierta. Prueba con interceptor verifica número constante de consultas al aumentar materias.

## Persistencia y límites

Migración aditiva `20261109043528_FamilyRegistrationReceipts`, solo aplicada a bases sintéticas durante desarrollo. Actualizar la instalación por el procedimiento normal antes de servir la nueva versión. El asistente familiar es web adaptable a teléfono; no se declara una pantalla equivalente nueva en Flutter.

Asistencia diaria por grupo, un recibo para varios hermanos y reglas automáticas de cobro quedan pendientes de decisión del colegio. No se importa currículo en este incremento: el plan de migración de materias permanece separado. No se modifica el PHP ni se usan datos escolares reales.
