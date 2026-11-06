# Migración de estudiantes y familias para iniciar en 2027

Fecha: 6 de noviembre de 2026. Responsable de revisión y operación: propietario/desarrollador.

**Recomendación:** importar un padrón revisado mediante consola, con personas, vínculos y matrícula 2027 separados, y preparar cuentas pendientes sin contraseña ni código. Activar después persona por persona. Este documento prepara ese trabajo; no ejecuta una migración ni certifica datos del colegio.

## Qué se estudió y qué existe hoy

Se revisó código local, sin conectar a MariaDB ni leer registros personales. Los nombres de columnas se contrastaron con el inventario de esquema de la referencia; falta comprobarlos contra metadatos de la instalación antes de extraer.

| Evidencia | Implicación |
| --- | --- |
| PHP `mvc/models/student_m.php`, `get_promoted_student` | El código contempla copias por ciclo y busca por varios identificadores. Es evidencia de un riesgo de duplicación, no una medición de duplicados reales. |
| PHP `mvc/models/parents_m.php`, `get_existing_for_promotion` | También contempla múltiples candidatos de encargado entre ciclos; no copiar su selección automática. |
| PHP `mvc/controllers/student.php` | Lee ciclo desde sesión y escribe usando configuración en distintos flujos. No confiar en el ciclo global del legado como destino. |
| PHP `mvc/models/studentrelation_m.php` | Hay campos de grado/sección/ciclo repetidos respecto de `student`; una discrepancia debe revisarse. `studentrelation` no representa parentescos. |
| PHP `mvc/language/spanish/student_lang.php` y formulario `student/add.php` | `state` se presenta como código institucional, `dni` como código personal y `registerNO` como número de registro. No confundirlos por el nombre técnico. |
| PHP `mvc/controllers/parents.php`, alta | Asigna `parents.name` desde `father_name`; `parents` contiene además `mother_name`. El nombre principal no identifica de forma fiable a quien usa la ficha familiar. |
| [LegacyPeopleImporter.cs](../src/SchoolPlatform.Host/LegacyImport/LegacyPeopleImporter.cs) | Ya importa estudiantes/encargados normalizados, mapa legado y vínculos sin facultades. No crea cuentas ni matrículas; solo admite un `parentLegacyId` por estudiante. |
| [AccountAccessService.cs](../src/SchoolPlatform.Host/IdentityAccess/AccountAccessService.cs) | Invitación crea cuenta inactiva **y emite código**. Activación añade contraseña y pone `IsActive=true`; el botón de habilitar rechaza cuentas sin contraseña. |
| [EnrollmentService.cs](../src/SchoolPlatform.Host/Academic/EnrollmentService.cs) | Ya valida ciclo abierto, fecha, grado, sección y cupo. La importación deberá conservar esas reglas y aportar fecha explícita. |

El esquema conocido está en `reference/PLAN_MAESTRO_ORIGINAL.md`, apartados `student`, `parents`, `studentrelation`, `schoolyear`, `classes`, `section`. El PHP es referencia de estructura; sus reglas de acceso no se trasladan.

## Alternativas consideradas

| Forma | Ventaja | Coste o limitación para este caso | Decisión |
| --- | --- | --- | --- |
| Registrar todo manualmente en la nueva web | Usa validaciones existentes | Mucha captura repetida; difícil comparar con el origen y evitar omisiones | Reservar para excepciones y altas posteriores |
| Cargar directamente SQL/volcado MariaDB a tablas finales | Parece rápida | Mezcla modelos incompatibles y omite IAM, reglas de matrícula y auditoría | Descartar |
| Archivo de hoja de cálculo como único importador | Fácil de revisar | Fechas/códigos pueden alterarse; una fila por alumno duplica adultos y mezcla matrícula | Útil como interfaz de revisión, convertir después a paquete estricto |
| Importador web permanente para el cliente | Accesible desde navegador | Añade carga de archivos sensibles, pantallas y mantenimiento a una operación del propietario | No construir para esta migración |
| Sincronización continua entre plataformas | Mantiene dos sistemas actualizados | Conflictos de identidad y dos fuentes de verdad; innecesaria sin historial a transferir | Descartar |
| Consola con extracción acotada, paquete revisado, plan y conciliación | Repetible, trazable y permite separar cada concepto | Requiere ampliar la herramienta existente y revisar ambigüedades | **Recomendada** |

No es necesario un sistema ETL de terceros para este alcance. Reutilizar el host y las reglas de dominio, con contrato versionado y sin endpoints públicos nuevos.

## Qué queda guardado

| Concepto | Destino | Estado inicial |
| --- | --- | --- |
| Persona confirmada | `people.Persons` | `Active`: ficha utilizable; no concede ingreso |
| Estudiante | `people.Students` | Código institucional revisado, estable y único |
| Padre/madre/encargado identificado | `people.Guardians` | Una persona por adulto, compartida entre sus hijos |
| Relación confirmada | `people.GuardianRelationships` | Parentesco descriptivo y fecha de inicio revisada |
| Facultades familiares | `people.RelationshipFaculties` | Cero concesiones importadas |
| Inscripción | `academic.Enrollments` | Una por estudiante y GUID de ciclo de destino; `Active` para los confirmados |
| Grado/sección | `academic.EnrollmentPlacements` | Fecha efectiva explícita; sección opcional `null` |
| Cuenta | `iam.AspNetUsers` | `IsActive=false`, `PasswordHash=null`; sin códigos emitidos |
| Cuenta ↔ persona | `people.AccountLinks` | Una relación vigente por persona y cuenta; una cuenta sirve si la persona tiene varios perfiles |
| Rol de cuenta | `iam.AccountRoleAssignments` | Clave de rol SQL elegida explícitamente, activa y sin permisos administrativos |
| Procedencia | Mapa legado y lote por ampliar | Identidad original, decisiones de unificación/descomposición, hash y resultado persistentes |

La cuenta del operador y cualquier cuenta anterior quedan fuera del cambio masivo. No ejecutar `UPDATE ... SET IsActive=0` sobre toda la base.

### Cómo asegurar el ciclo correcto

1. Crear en la nueva plataforma el ciclo de etiqueta `2027`, con las fechas reales indicadas por el propietario; abrir inscripciones. No asumir que sus fechas son 1 de enero/31 de diciembre ni que tiene el ID numérico 2027.
2. Registrar o comprobar grados y secciones de ese ciclo. Los IDs MariaDB no se reutilizan como GUID SQL Server.
3. Seleccionar explícitamente un ciclo **de origen** para extraer candidatos. Puede ser 2026 o un padrón 2027 ya preparado; no se ha comprobado qué contiene la base antigua.
4. Revisar por estudiante `include/exclude/pending`, grado de destino, sección opcional y fecha efectiva. Una correspondencia por grado puede proponer valores, pero cada fila requiere confirmación. No trasladar automáticamente el grado 2026 ni subir uno por orden numérico.
5. Resolver el GUID del ciclo de destino en el paquete y confirmar su etiqueta/fechas en el plan. Todas las matrículas deben apuntar a ese GUID. Sección `0` se normaliza a `null`, nunca a una sección inventada.
6. Los candidatos que no continúan quedan excluidos con motivo; los pendientes bloquean el lote final. No se copia matrícula 2026 ni se crea otra persona por cambiar de ciclo.

Ejemplo: un estudiante de cuarto en el origen puede ir a quinto, repetir cuarto o no continuar. El padrón revisado determina cuál ocurre; no se necesita importar ninguna nota para decidirlo.

### Cómo tratar padres y hermanos

- `student.parentID → parents.parentsID` aporta una referencia familiar, no una facultad ni prueba de custodia.
- Un encargado confirmado con varios hijos se guarda una vez y tiene varios vínculos; no una cuenta por hijo.
- Una ficha con padre y madre puede producir dos personas **si se confirma quién es cada una**. `parents.name`, `father_name` y `mother_name` pueden repetir un nombre: no crear tres adultos ni asignar automáticamente el `dni`/teléfono de la fila a todos.
- Si el titular real no se puede identificar, importar el estudiante sin encargado y conservar el asunto pendiente en el expediente de revisión privado. No crear una persona ficticia «Familia de…».
- Un alumno sin referencia familiar se importa normalmente. Una referencia inexistente se informa; el operador decide excluir ese vínculo antes del lote final.
- Nombres semejantes, correo o teléfono compartido generan candidatos de revisión; no uniones automáticas. Separar nombres/apellidos requiere confirmación humana, especialmente nombres compuestos.
- Seleccionar un solo ciclo de origen reduce copias históricas. Cuando una persona tenga varias referencias antiguas, asignar alias confirmados a la misma persona canónica; no confiar en DNI/código/nombre por sí solo.
- Los datos oficiales de identidad no tienen todavía almacenamiento específico en la nueva plataforma. No reutilizar `StudentCode` para ese fin ni copiarlos a observaciones. La extracción propuesta los omite.

### Códigos y contactos

Revisar primero `state` como candidato a código institucional y `registerNO` como alternativa de revisión. Mantener ceros a la izquierda. El destino exige 1–30 caracteres `A-Z 0-9 . -` y unicidad global en esta instalación. Una colisión detiene la decisión: no añadir sufijos silenciosamente ni fusionar fichas por código. Si hace falta asignar códigos nuevos, decidir el patrón y dejar la correspondencia en el paquete antes de aplicarlo.

Fechas vacías o inválidas no se inventan. Los contactos inválidos se corrigen o se excluyen expresamente; no se descartan en silencio. No crear correos ficticios, ni usar el teléfono como contraseña o como identidad única. Direcciones, fotografías, documentos, datos médicos y demás campos quedan fuera de esta primera carga salvo un incremento autorizado posterior.

## Acceso inactivo y activación gradual

Se compararon dos variantes: crear la cuenta solo cuando alguien la solicite, o prepararla pendiente durante la importación. La segunda satisface mejor la solicitud: permite ver quién está pendiente, reservar un usuario único y activar sin volver a enlazar la ficha.

La preparación propuesta crea una cuenta por persona confirmada, sin contraseña y sin `AccessGrant`. El identificador de ingreso será un código técnico opaco reservado en el plan, sin DNI/fecha de nacimiento; no se deduce del nombre ni se reutiliza el usuario antiguo. No representa un secreto. Su formato definitivo debe quedar fijado en el implementador antes del primer lote.

El rol SQL se selecciona por clave y se verifica contra `AccessRolePermissions`/`AccountPermissions`: cero permisos de gestión/lectura administrativa del catálogo actual. No asumir que un rol llamado «Estudiante» o «Familia» es seguro. Los accesos propios/familiares los resuelve `PeopleService.StudentAccessAsync` y las facultades verificadas, no el nombre del rol. Un cambio posterior del rol debe seguir el flujo IAM y su auditoría normales.

Activación individual, usando las pantallas existentes:

1. Comprobar identidad del titular y que la cuenta está vinculada a la persona correcta. Para un adulto, revisar hijos, vigencia y las facultades que procedan; parentesco por sí solo no basta.
2. Emitir **en ese momento** un código de activación de un uso desde Cuentas, con `accounts.manage`. No habilitar masivamente ni generar centenares de códigos que caduquen antes de entregarse.
3. Entregar manualmente por canal verificado. El secreto se muestra una vez y su hash queda durable; nunca en URL, exportación, reporte o Git.
4. El titular canjea el código y elige contraseña. `RedeemAsync` activa esa cuenta; el flujo compartido de ingreso sirve a web y app. No hay otra contraseña inicial.
5. Verificar que ve solo su información autorizada. Para encargados, activar la cuenta no concede automáticamente información académica o financiera de sus hijos.

No se necesita correo obligatorio ni enviar mensajes automáticamente. Cambiar `IsActive` por SQL no es el procedimiento de activación.

## Procedimiento operativo propuesto

### 1. Inspección y extracción autorizada

El archivo [extract-readonly.sql](migration-2027/extract-readonly.sql) es un **borrador de consultas**, no un extractor ejecutado ni un cliente configurado. Por defecto su ciclo origen es `NULL`: las consultas de candidatos no devuelven datos hasta elegirlo explícitamente. Primero verificar metadatos/versión/motores y que el catálogo seleccionado sea el correcto; no leer `mvc/config/database.php` ni copiar sus credenciales al repositorio.

Para la operación real, usar una credencial de solo lectura aportada al proceso por secretos de la instalación. Mantener todas las consultas del lote en una sola conexión. La propuesta usa `REPEATABLE READ` y `START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY`, que MariaDB documenta para una vista consistente en InnoDB. Si alguna tabla no es transaccional, o hay DDL concurrente, no afirmar que esa instantánea la cubre; definir una extracción desde una copia consistente con el propietario. [Documentación de MariaDB](https://mariadb.com/docs/server/ha-and-performance/standard-replication/enhancements-for-start-transaction-with-consistent-snapshot).

Los resultados contienen personas reales solo cuando el propietario ejecute después la extracción. Guardarlos fuera de Git, con acceso restringido y sin volcarlos por consola/CI. El extractor futuro escribirá archivos UTF-8 locales, no `SELECT INTO OUTFILE` en el servidor. Este borrador no modifica tablas, crea usuarios ni cambia permisos.

### 2. Padrón de revisión

Trabajar fuera de Git, por ejemplo en `staging/2027/` (ignorado). Conservar original, revisión y paquete final separados. La revisión puede hacerse con una hoja de cálculo o herramienta local, pero el artefacto final será JSON estricto. Si se usa hoja de cálculo, importar códigos como texto y tratar los contenidos de origen como texto, sin evaluar fórmulas.

Cada candidato tendrá una decisión y motivo, identidad canónica, nombres/apellidos revisados, código y matrícula destino. Los adultos y los vínculos estarán en listas separadas. El JSON [review.example.json](migration-2027/review.example.json) muestra **solo datos inventados**: hermanos, dos encargados de un estudiante y otro estudiante sin encargado. Es un contrato de ejemplo para diseñar la ampliación; **ningún comando actual lo aplica**. No cambiarle nombres de campos para forzarlo a pasar por `--import-people`.

El paquete final deberá incluir versión, lote, fuente estable de esta instalación, `SchoolId`, instante/hash de extracción, ciclo destino por GUID, decisiones cerradas, personas canónicas y alias, vínculos, matrículas y política de cuentas pendientes. Los nombres/contactos se guardan solo donde hacen falta; reportes operativos y auditoría general usan IDs y conteos.

### 3. Plan sin escrituras

El futuro plan validará el paquete contra el destino real y producirá:

- GUID/etiqueta/fechas/estado del ciclo, huella de la base/instalación y SHA-256 de los bytes exactos del paquete.
- Candidatos = incluidos + excluidos; pendientes = 0 antes del corte. Adultos sin identificar se resuelven como vínculo excluido, con el estudiante incluido si corresponde.
- Personas, perfiles, vínculos, matrículas, cuentas y alias a crear; coincidencias ya importadas separadas de conflictos. Totales por grado/sección y ocupación prevista contando filas del lote y del destino.
- Incidencias bloqueantes: duplicados/ambigüedades sin resolver, códigos repetidos, fechas inválidas, identificadores desconocidos, cuenta/persona ya enlazada de forma incompatible, permisos del rol, matrículas existentes diferentes, sección de otro grado/ciclo o cupo insuficiente.
- Cero contraseñas, códigos o facultades por generar; cero notas/cargos/recibos/asistencia/carnets a migrar.

No aplicar parcialmente «las filas que pasaron». Resolver el paquete o dividirlo explícitamente en lotes cerrados y volver a planificar. Un hash acredita qué archivo se revisó, no que el destino siga igual.

### 4. Ensayo y aplicación

Desarrollar/probar únicamente con datos sintéticos y SQL Server real en bases aleatorias del fixture, sin copiar la base escolar. Un ensayo con datos reales sería una operación posterior separada, no una prueba de desarrollo autorizada por este documento.

El comando futuro de aplicación deberá exigir actor activo, `people.manage`, `enrollment.manage` y `accounts.manage`, más el hash del plan y el GUID destino explícito. El ciclo/estructura se preparan previamente con `academic.manage`; el importador no crea estructura desconocida. La política MFA de la instalación también debe cumplirse en el procedimiento de consola, sin asumir que comprobar permisos SQL equivale a verificar segundo factor.

Antes de aplicar: respaldo verificable del destino y revisión del lote exacto. Dentro de una transacción SQL corta: bloqueo de lote, bloqueo IAM compartido, revalidación del actor/permisos y del estado del destino, creación de personas/perfiles/alias/vínculos/matrículas/cuentas/enlaces, proyección de permisos y auditoría, confirmación conjunta. Identity y School son contextos distintos: compartir conexión/transacción explícitamente; no confirmar uno antes del otro. No introducir `SaveChanges` independientes que dejen cuentas o personas huérfanas.

La concurrencia con inscripciones normales debe quedar cubierta por bloqueo/rango SQL o una ventana operativa verificada: un bloqueo exclusivo del importador por sí solo no impide que la web ocupe el último cupo. Revalidar cupos bajo la protección elegida y probarlo. No iniciar extracciones, entregas o respaldos bajo el bloqueo IAM.

Si el destino cambió desde el plan y afecta a sus precondiciones, abortar y generar otro plan. En errores, rollback completo del lote; no borrar personas a mano como compensación.

### 5. Conciliación y corte

Releer el destino tras confirmar; no tomar como conciliación el reporte que se calculó antes del `commit`. Conservar un resultado durable por lote, hash, actor y IDs; el detalle privado de revisión queda fuera de Git y de la auditoría general.

- Cada estudiante incluido tiene una persona canónica, código esperado, alias de origen y exactamente una matrícula en el GUID de 2027 con la ubicación/fecha revisadas.
- Cada adulto confirmado existe una vez según las decisiones revisadas; hijos y parentescos coinciden. Ningún vínculo tiene facultades importadas.
- Cada persona importada tiene una cuenta pendiente vinculada: inactiva, sin contraseña, sin concesión de activación, sin permisos administrativos. No se alteró ninguna cuenta ajena al lote.
- Totales conciliados de alumnos por grado/sección, adultos, vínculos y cuentas, con conteos de ya existentes y conflictos diferenciados.
- No aparecen notas, tareas, asistencia, obligaciones, pagos, carnets o credenciales de dispositivos por efecto de la migración. La falta de saldo no se presenta como solvencia.

Repetir el mismo lote debe devolver un resultado sin nuevas creaciones. Un lote posterior con datos cambiados no sobrescribe identidades, cuenta, contactos o matrículas: se informa el conflicto y se corrige mediante flujo explícito. Las activaciones individuales que ya hayan ocurrido nunca se revierten al repetir la importación.

Como el legado sigue cambiando, hacer una extracción final del mismo alcance y comparar su hash/contenido normalizado con el revisado. Un hash diferente obliga a revisar los cambios, no a aplicarlos automáticamente. El propietario define el momento de corte y qué plataforma recibe nuevas altas desde entonces. No activar personas hasta pasar conciliación.

Si falla la transacción, se revierte entera. Si el error se descubre después del commit, corregir por procedimientos auditados; restaurar el respaldo solo en una ventana sin escrituras posteriores verificadas. No prometer un «deshacer» que borre auditoría, cuentas activadas o cambios de terceros.

## Brechas del importador actual antes de usarlo para 2027

Estas observaciones proceden de lectura de código; no se ejecutaron fallos contra datos reales.

| Brecha | Trabajo necesario |
| --- | --- |
| No tiene ciclo/matrícula ni cuentas | Ampliar el flujo; ejecutar hoy `--import-people` no satisface la solicitud |
| Deserialización tolera propiedades desconocidas; `scope` no se valida | Versión/esquema estricto y rechazo de alcance/campos/modo inesperados |
| Relación `parentLegacyId` única y mapa de encargado uno a uno por fila | Listas de vínculos y alias discriminados para dos adultos de una ficha y adultos compartidos |
| Aplica filas válidas aunque reporte incidencias en otras | Lote cerrado con bloqueos explícitos y exclusiones revisadas |
| Encargados se seleccionan antes de excluir alumnos inválidos | Evitar crear adultos sin ningún estudiante incluido válido |
| Dos registros repetidos pueden dejar válida la primera aparición | Marcar el grupo completo como ambiguo; ninguna política «primero gana» |
| Plan cuenta parejas sin descontar vínculos ya existentes | Conteos reales del estado destino, incluidas repeticiones |
| Verificación del actor/lectura del mapa ocurre antes de iniciar la transacción | Revalidar bajo transacción y protección de concurrencia |
| Contactos inválidos se omiten; algunos límites no se validan en plan | Validación completa antes de escribir, sin pérdidas silenciosas |
| Fecha de vínculo usa UTC actual; nacimiento limita a 1990 y usa reloj real | Fecha revisada para vínculo y validación coherente con dominio/TimeProvider; no inventar umbrales |
| `InviteAsync` genera código y `SchoolDbContext` no escribe IAM | Nueva operación pendiente sin secretos y transacción compartida con Identity, no escritura indirecta del contexto escolar |
| No hay conciliación durable de matrículas/cuentas/lote | Persistencia y consulta del resultado para reintento seguro y verificación posterior |

## Implementación por incrementos y aceptación

1. **Contrato/extractor y revisión:** versión estricta, extracción solo de columnas permitidas, alias/descomposición familiar y decisiones del padrón. Metadatos y consola redactados; pruebas sintéticas de códigos, fechas, nombres, hermanos, dos adultos, huérfanos y duplicados. No leer tablas académicas/financieras del legado.
2. **Plan 2027 y persistencia del lote:** resolver GUIDs, validar estado/fechas/cupos/permisos y reportar exactamente las creaciones. Cero escrituras en plan, incluso sobre incidencias; no generar cuentas ni códigos para planificar.
3. **Apply atómico:** cuentas pendientes sin códigos, enlaces, matrícula y auditoría compartidos. Pruebas SQL reales de rollback por fallo después de crear personas/cuentas; dos aplicaciones concurrentes; cupo competido desde la web; actor revocado; rol modificado desde el plan; último gestor intacto.
4. **Reconcile/activación:** doble ejecución sin duplicación, destino alterado produce conflicto, repetición tras una activación la conserva; web y app rechazan cuentas pendientes, canje individual habilita solo al titular, encargado sin facultades no obtiene datos protegidos. Otro estudiante/ciclo permanece inaccesible.
5. **Operación del propietario:** respaldo/restauración verificados, extracción final y delta revisados, aplicación real y conciliación. Este paso no se ha realizado.

No se establece fecha de corte ni se necesita decisión del cliente para terminar esta investigación. La selección de estudiantes, grados, fechas reales y titulares se resuelve en el padrón del propietario cuando se prepare el lote real.
