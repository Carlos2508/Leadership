# Plataforma escolar: análisis del legado y plan maestro de reconstrucción

**Fecha de referencia:** 4 de noviembre de 2026, America/Guatemala.  
**Estado:** propuesta de arquitectura y especificación de construcción; no es una plataforma implementada.  
**Fuente analizada:** C:/xampp/htdocs/sys/sys-escolar y base local leadership.  
**Destino inicial:** un colegio, con posibilidad de sedes y crecimiento posterior.  
**Migración solicitada:** estudiantes, padres/encargados y sus vínculos verificables.  
**Destinatarios:** propietario, dirección, coordinación académica, equipo de desarrollo, QA y operaciones.

## Índice

1. [Decisiones principales y requisitos confirmados](#1-decisiones-principales-y-requisitos-confirmados)
2. [Qué se investigó y qué no puede darse por demostrado](#2-qué-se-investigó-y-qué-no-puede-darse-por-demostrado)
3. [Radiografía técnica y de datos](#3-radiografía-técnica-y-de-datos)
4. [Funcionamiento real del sistema actual](#4-funcionamiento-real-del-sistema-actual)
5. [Hallazgos y consecuencias para el reemplazo](#5-hallazgos-y-consecuencias-para-el-reemplazo)
6. [Elección tecnológica: MVC, Angular, Flutter y Redis](#6-elección-tecnológica-mvc-angular-flutter-y-redis)
7. [Arquitectura del monolito modular](#7-arquitectura-del-monolito-modular)
8. [Identidad, cuentas y autorización](#8-identidad-cuentas-y-autorización)
9. [Modelo de datos propuesto](#9-modelo-de-datos-propuesto)
10. [Ciclos e historial](#10-ciclos-e-historial)
11. [Notas, recuperación, boletas y promoción](#11-notas-recuperación-boletas-y-promoción)
12. [Tareas, entregas y archivos](#12-tareas-entregas-y-archivos)
13. [Especificación funcional del resto de módulos](#13-especificación-funcional-del-resto-de-módulos)
14. [API compartida por web y app](#14-api-compartida-por-web-y-app)
15. [Experiencia y diseño de producto](#15-experiencia-y-diseño-de-producto)
16. [Migración limitada de personas y relaciones](#16-migración-limitada-de-personas-y-relaciones)
17. [Seguridad, privacidad y auditoría](#17-seguridad-privacidad-y-auditoría)
18. [Operación, infraestructura y rendimiento](#18-operación-infraestructura-y-rendimiento)
19. [Pruebas y aceptación](#19-pruebas-y-aceptación)
20. [Plan de ejecución e instrucciones para construir](#20-plan-de-ejecución-e-instrucciones-para-construir)
21. [Decisiones pendientes y riesgos](#21-decisiones-pendientes-y-riesgos)
22. [Fuentes y referencias](#22-fuentes-y-referencias)
23. Anexo A: inventario completo de controladores.
24. Anexo B: diccionario físico completo de la base local.
25. Anexo C: consultas reproducibles de diagnóstico.

## 1. Decisiones principales y requisitos confirmados

### 1.1 Recomendación ejecutiva

Construir un **monolito modular con ASP.NET Core sobre .NET 10 LTS, EF Core y SQL Server**, una **web Angular**, páginas de autenticación de **ASP.NET Core Identity con scaffolding de Razor Pages**, y una **app Flutter posterior**, consumiendo la misma API. Incorporar Redis como acelerador y soporte de necesidades distribuidas cuando aporte valor medido.

La recomendación de Angular responde a la edición intensiva de notas, filtros, paneles por rol, entregas y experiencias interactivas que se quieren construir. No significa que MVC sea menos seguro, menos potente en el servidor o incapaz de tener un diseño excelente. Si el equipo termina siendo una sola persona especializada en C#, la alternativa MVC/Razor descrita en §6 es plenamente válida y reduce trabajo de frontend.

La calidad dependerá especialmente de separar bien la identidad, los vínculos familiares, la matrícula anual, los permisos y las reglas académicas. Cambiar PHP por .NET sin cambiar esas separaciones repetiría los problemas actuales.

### 1.2 Lo confirmado por el propietario en esta conversación

| ID | Confirmación | Consecuencia obligatoria |
|---|---|---|
| DEC-001 | Un colegio, preparado para crecer | No construir una plataforma comercial multicolectiva completa en el primer lanzamiento; establecer SchoolId y límites institucionales desde el diseño. |
| DEC-002 | Reconstrucción desde cero | El PHP es evidencia funcional y fuente de extracción, no base de la nueva aplicación. |
| DEC-003 | Basta migrar estudiantes y padres si están ligados | La migración no incluye automáticamente notas, pagos, tareas, chats ni credenciales. |
| DEC-004 | El estudiante debe poder entrar con su propia cuenta, incluso sin padre | Cuenta opcional independiente del perfil escolar y del vínculo familiar; el sistema no exigirá un padre para autenticar a un adulto. |
| DEC-005 | Cada materia se aprueba con promedio de los cuatro bimestres de al menos 60 | Calcular por materia, no aprobar por promedio general de todas las materias. |
| DEC-006 | Las materias reprobadas van a recuperación y deben aparecer reprobadas en la boleta inicial | La boleta inicial refleja el resultado ordinario y la necesidad de recuperación. |
| DEC-007 | La recuperación reemplaza la nota final de la materia | Registrar una evaluación de recuperación separada; no modificar bimestres para simularla. |
| DEC-008 | Después de recuperación se genera otra boleta | Conservar la emisión original y crear una nueva revisión con el resultado efectivo actualizado. |
| DEC-009 | Se desea evaluar .NET MVC o .NET + Angular, con Flutter, SQL Server y Redis | La comparación y la elección razonada están en §6; las dos variantes comparten backend y dominio. |
| DEC-010 | El PHP se usa actualmente solo de forma administrativa; el portal de estudiantes no está lanzado | Las ramas de portal del código son capacidades previstas, no flujos demostrados en operación. |
| DEC-011 | La nueva plataforma sí debe permitir consultar notas y entregar tareas con flujos completos | Diseñar y probar publicación, audiencia, matrícula efectiva, entregas y excepciones desde el primer portal. |
| DEC-012 | Deben contemplarse ingresos tardíos al ciclo y distribución correcta de tareas del docente | Usar membresías con vigencia y reglas explícitas sobre trabajo anterior al ingreso; ver §11, §12 y §19. |
| DEC-013 | Las notas bimestrales solo se muestran con las colegiaturas requeridas completamente cubiertas | Cada publicación define un corte configurable. El ejemplo de marzo ilustra que deben cubrirse todos los meses aplicables hasta ese corte, no solo un abono; marzo no es una regla fija. |
| DEC-014 | Se requieren cobros masivos por grado/grupo y tipos de cobro mejorados | Finanzas y solvencia forman parte del núcleo del portal, no una extensión opcional. |

DEC-007 quedó expresamente confirmada por el propietario: **la recuperación reemplaza la nota final de la materia**. La nota anual ordinaria permanece guardada. Su reemplazo como resultado efectivo no equivale a borrar el resultado previo.

### 1.3 Alcance del producto y alcance de migración son distintos

El nuevo producto podrá tener notas, cobros, tareas, asistencia, comunicación y módulos adicionales. Eso **no obliga a importar sus transacciones antiguas**. El primer traslado se limita a personas y relaciones; los catálogos y matrículas del nuevo ciclo se configuran como parte de la puesta en marcha.

Con ese alcance, el historial académico dentro del nuevo sistema comenzará cuando se empiecen a registrar datos allí. Los años anteriores permanecen en un archivo legado separado y protegido. Si después se desean boletas antiguas dentro del nuevo portal, será una ampliación expresa de migración, no una capacidad que pueda prometerse sin importar esos datos.

### 1.4 Qué significa una plataforma de alto nivel en este proyecto

- Un padre entra una vez y ve exclusivamente los hijos que tiene autorizados, aunque estén en grados diferentes.
- Un estudiante entra con identidad propia y conserva su expediente al cambiar de ciclo.
- Un docente captura notas rápidamente, conoce qué se guardó y puede resolver conflictos sin perder trabajo.
- La misma regla produce el mismo resultado en pantalla, PDF, app, recuperación y promoción.
- Un ciclo cerrado protege la escritura; consultar un año anterior no cambia globalmente el contexto de otras pestañas.
- Una recuperación genera trazabilidad y una nueva boleta; una promoción no duplica personas.
- El colegio puede restaurar sus datos, investigar un cambio y operar cuando fallen correo, Redis o un proveedor externo.

## 2. Qué se investigó y qué no puede darse por demostrado

### 2.1 Investigación realizada

1. Inventario de aplicación: **62 controladores, 75 modelos y 260 vistas PHP** bajo mvc.
2. Lectura de entrada, rutas, configuración relevante, controlador base, modelo base y gestión de permisos.
3. Seguimiento detallado de acceso, estudiantes, padres, ciclos, notas, promoción, tareas, carga/descarga de archivos y cobros.
4. Revisión de flujos y dependencias de asistencia, horarios, materias, recursos, conversaciones, avisos, biblioteca, transporte, residencia y nómina.
5. Inspección física de **72 tablas y 706 columnas**, claves, índices y conteos reales con COUNT(*).
6. Consultas agregadas de integridad: huérfanos, duplicados, relaciones familiares, credenciales disponibles y consistencia de ciclos.
7. Consulta de documentación oficial de .NET, Identity, Angular, OpenIddict, Flutter, EF Core, OWASP y W3C para fundamentar la propuesta.

La base local estaba apagada. Se inició una instancia temporal vinculada a 127.0.0.1:3307, con read_only activado y event_scheduler desactivado. Las consultas de investigación fueron SELECT y consultas de metadatos. Se apagó ordenadamente al terminar. No se ejecutó el SQL de reparación existente ni se hicieron cambios funcionales al PHP o a sus registros de negocio. El arranque normal de MariaDB puede actualizar sus archivos internos de operación; read_only no hace inmutables físicamente los archivos del motor.

### 2.2 Clasificación de evidencia

| Etiqueta | Significado |
|---|---|
| OBS-CÓDIGO | Comportamiento visible en una ruta o función examinada. |
| OBS-BD | Resultado de una consulta a esta copia local. |
| CONFIRMADO | Regla o preferencia confirmada por el propietario. |
| PROPUESTO | Diseño recomendado para el reemplazo. |
| PENDIENTE | Requiere decisión o verificación adicional antes de habilitar el flujo correspondiente. |

Las cifras son de **esta copia local**, no una certificación del contenido de producción. Los conteos estimados de information_schema.TABLES no se usaron como conteos finales: en InnoDB pueden diferir mucho de COUNT(*).

No se realizaron pruebas de penetración, envío de mensajes, pagos de prueba, ejecución interactiva de cada pantalla ni validación de todas las reglas con personal del colegio. Un controlador existente no demuestra que el módulo esté completo o se utilice. Los hallazgos de seguridad de código señalan rutas y condiciones concretas; no afirman que un ataque haya ocurrido.

**Aclaración operacional confirmada:** el PHP se usa solo administrativamente. No se ha lanzado el autoservicio de estudiantes para entregar tareas o consultar notas. Las referencias de este documento al acceso de padres/estudiantes describen las ramas del código; no certifican que esos portales estén activos ni probados con usuarios finales. El lanzamiento nuevo necesita validación de experiencia, soporte, permisos y adopción además de desarrollo técnico.

### 2.3 Documento previo encontrado

Existe PROMPT_NUEVA_PLATAFORMA_ESCOLAR_GT.md. Aporta una visión anterior de .NET, Angular, Flutter y módulos amplios. Este análisis conserva su intención útil, pero actualiza las decisiones con evidencia real y las respuestas del propietario. Particularmente, **no arrastra su migración amplia de notas y finanzas** al alcance actual y evita imponer versiones futuras por una referencia numérica sin verificación.

## 3. Radiografía técnica y de datos

### 3.1 Arquitectura actual

~~~text
Navegador
  -> Apache / XAMPP / .htaccess
  -> index.php
  -> CodeIgniter 3.1.4
  -> MY_Controller -> Admin_Controller
  -> controlador de cada pantalla
  -> modelos derivados de MY_Model + SQL directo
  -> MariaDB local: leadership
  -> vistas PHP + JavaScript/jQuery + archivos en uploads
~~~

- Framework identificado en main/core/CodeIgniter.php: **CodeIgniter 3.1.4**.
- composer.json declara PHP >= 7.4; eso no demuestra qué versión corre en producción.
- La base inspeccionada responde como **MariaDB 10.1.29** y las 72 tablas observadas usan InnoDB.
- La ruta predeterminada es signin; no se encontró una API de negocio versionada que pueda utilizar una app nueva como contrato estable.
- Las respuestas de AJAX alternan HTML, texto y redirecciones; las reglas están distribuidas entre controladores, modelos y vistas.
- index.php fija America/La_Paz, distinto de la zona solicitada para Guatemala.
- La sesión usa school_sessions. El contexto de consulta anual se guarda como defaultschoolyearID.
- Los archivos de imágenes, informes y entregas están debajo de uploads, dentro del árbol web.

### 3.2 Volumen comprobado

| Entidad física | Filas exactas | Lectura correcta |
|---|---:|---|
| schoolyear | 2 | Ciclos 2025 y 2026; IDs 16 y 17. |
| student | 261 | Filas de estudiantes por ciclo; no equivalen necesariamente a 261 personas distintas. |
| parents | 211 | Filas de cuentas/fichas familiares por ciclo; tampoco equivalen necesariamente a 211 adultos distintos. |
| studentrelation | 293 | Proyección histórica/académica adicional; no es la relación padre-hijo. |
| classes | 17 | Grados/clases globales. |
| section | 1 | Aun así, los 261 estudiantes tienen sectionID = 0. |
| subject | 225 | Materias asociadas a grados y docentes, sin ciclo en la tabla. |
| teacher | 9 | Registros de docentes. |
| exam | 4 | Los cuatro bimestres, con IDs sin orden cronológico. |
| mark | 13,834 | Cabeceras de nota por estudiante/materia/bimestre/ciclo. |
| markrelation | 138,830 | Detalle de actividades o componentes de la nota. |
| assignment / assignmentanswer | 4 / 0 | Hay tareas antiguas; no hay entregas registradas en esta copia. |
| invoice / payment | 564 / 10 | Cargos y pagos registrados. |
| promotionlog | 33 | Configuraciones/resultados de procesos de promoción. |
| conversations / conversation_msg / conversation_user | 10 / 0 / 46 | Estructura de mensajería parcialmente poblada. |

El inventario exacto de todas las tablas está en el anexo B. No se debe dimensionar la infraestructura como si estos volúmenes exigieran microservicios.

### 3.3 Estudiantes y padres por ciclo

| Métrica | 2025 | 2026 |
|---|---:|---:|
| Filas student | 114 | 147 |
| Activos | 113 | 147 |
| Sin parentID útil: NULL o 0 | 6 | 59 |
| parentID positivo que no existe | 0 | 1 |
| Padres vinculados pertenecientes a otro ciclo | 0 | 0 |
| Estudiantes con username no vacío | 0 | 0 |
| Estudiantes con password no vacío | 0 | 0 |
| Fechas de nacimiento ausentes o inválidas | 2 | 7 |
| Filas parents | 112 | 99 |
| Padres sin ningún estudiante asociado por parentID | 7 | 15 |
| Padres sin correo | 111 | 98 |
| Padres sin teléfono | 87 | 86 |

**Implicaciones:** hay 65 filas de estudiantes sin vínculo útil y otra con padre inexistente. No se pueden inventar 66 relaciones familiares para completar una importación. La falta de correo y teléfono en muchos padres exige activación asistida, no un diseño que dependa exclusivamente de enviar correos.

### 3.4 Integridad y duplicación comprobadas

| Comprobación | Resultado | Consecuencia |
|---|---:|---|
| Grupos con username de padre repetido dentro de un ciclo | 1, con 1 fila adicional | Iniciar sesión por username + ciclo no es inequívoco. |
| Grupos con código personal de estudiante repetido dentro de un ciclo | 1, con 1 fila adicional | Código personal no puede asumirse único sin depurar. |
| Códigos personales de estudiantes presentes en más de un ciclo | 80 | Candidatos a unir identidades; no autorización para unir automáticamente. |
| Códigos Balam de estudiantes presentes en más de un ciclo | 80 | Segunda señal útil de continuidad anual. |
| Grupos de código personal con nombres diferentes | 1 | Posible corrección o colisión; revisión humana. |
| Grupos de código personal con nacimientos diferentes | 1 | Inconsistencia que bloquea una unión automática. |
| DPI de padres presente en más de un ciclo | 72 | Posibles copias anuales; validar identidad real y alcance de la ficha familiar. |
| Notas cuyo studentID no existe | 101 | El archivo histórico requiere preservar referencias originales. |
| Notas cuyo subjectID no existe | 105 | No reconstruir historial por catálogos actuales solamente. |
| Grupos duplicados de cabecera de notas | 25 | Falta una restricción natural de unicidad. |
| Grupos duplicados de componente por markID + markpercentageID | 500 | Una consulta puede sumar o seleccionar datos de forma ambigua. |
| Componentes duplicados con valores no vacíos diferentes | 0 | No se encontraron conflictos numéricos en esa consulta; siguen existiendo duplicados físicos. |
| Componentes sin calificación: NULL o vacío | 7,638 | Pendiente no puede interpretarse siempre como cero. |
| Componentes no vacíos no numéricos | 0 | La columna sigue siendo varchar aunque la muestra sea numérica. |
| Componentes fuera del máximo del catálogo | 0 | La muestra no demuestra que el servidor impida futuras escrituras inválidas. |
| Estudiantes sin studentrelation compatible con su grado y ciclo | 79 | Hay consultas con mecanismo de respaldo para no ocultarlos. |
| studentrelation sin student existente | 111 | Esa tabla no es un padrón confiable de personas. |
| Grupos repetidos de studentrelation por estudiante/grado/ciclo | 12 | Debe preferirse evidencia validada, no el primer resultado. |
| Filas student con bloodgroup que contiene un año 20xx | 260 | Campo reutilizado, no dato de salud. |
| Facturas sin estudiante existente | 12 | No migrarlas como transacciones del nuevo sistema en este alcance. |
| Tareas cuyo ciclo ya no existe en schoolyear | 4 | Las cuatro tareas registradas tienen referencias a años ausentes. |
| Entradas de configuración de componentes sin catálogo correspondiente | 1 | setting contiene mark_12 y no existe markpercentage 12. |
| Foreign keys declaradas en la base inspeccionada | 0 | La integridad depende de código y procesos manuales. |
| Restricciones UNIQUE distintas de PK | 0 | No hay protección física de las identidades naturales del negocio. |

El único índice secundario observado corresponde al timestamp de sesiones. Predominan las PK. La ausencia de índices de consulta contribuye a recorridos completos y explica parte de las optimizaciones manuales encontradas.

## 4. Funcionamiento real del sistema actual

### 4.1 Inicio de sesión y cuentas

**OBS-CÓDIGO:** Signin_m::signin recorre student, parents, teacher, user y systemadmin buscando username y hash de contraseña. Padres y estudiantes se restringen al ciclo configurado globalmente en setting.school_year. Los demás tipos no se filtran de ese modo.

El hash se calcula en MY_Model::hash con SHA-512 de contraseña concatenada con una clave global. No es el esquema de contraseñas adaptable y con salt individual de Identity. Los hashes no deben copiarse a PasswordHash del nuevo sistema.

El inicio carga loginuserID, loginTable, usertypeID, nombre, username y defaultschoolyearID en sesión. Hay loginlog con IP/navegador y fechas. Para padres repetidos existe una selección que prioriza cuenta activa, número de hijos vinculados y parentsID descendente. Es una reparación pragmática de ambigüedad, no una identidad estable.

**OBS-BD:** todos los estudiantes tienen usertypeID = 3, pero ninguno tiene username o password y **el catálogo usertype ni siquiera contiene el ID 3**. El código prevé estudiantes autenticados, pero esta copia no dispone de cuentas estudiantiles utilizables con ese flujo.

La búsqueda continúa entre tablas y puede sobrescribir la cuenta encontrada si el mismo login coincide en varias. El reemplazo debe tener un único almacén de cuentas y un único identificador de usuario.

Referencias: mvc/models/Signin_m.php:11; mvc/core/MY_Model.php:87; mvc/controllers/Signin.php:54.

### 4.2 Permisos y administración de usuarios

Admin_Controller calcula permisos como módulo o módulo_acción, carga permissions y permission_relationships, los conserva en sesión y arma el menú. La combinación usertypeID = 1 y loginuserID = 1 tiene trato especial. Algunos métodos verifican permissionChecker y otros dependen del constructor.

El control del constructor rechaza un permiso cuando existe explícitamente con valor no. Una acción ausente no queda denegada automáticamente por esa condición. Ocultar un botón no sustituye la autorización sobre la operación y sobre el estudiante específico.

Usertype permite tipos de usuario; Teacher, Systemadmin y User administran tablas separadas. Resetpassword permite restablecimiento administrativo. Permissionlog es un CRUD asociado a leaveapp, no un registro completo de cambios de autorización: el nombre puede inducir a error.

**Promover un estudiante de grado y elevar privilegios de una cuenta son operaciones diferentes.** El nuevo sistema debe nombrarlas y autorizarlas por separado.

Referencias: mvc/libraries/Admin_Controller.php:10; mvc/helpers/action_helper.php:228; mvc/models/Permissionlog_m.php.

### 4.3 Alta de padre y vínculo con hijos

Parents::add crea una fila parents, guarda father_name y mother_name dentro de la misma ficha, toma father_name como name de la cuenta, asigna usertypeID = 4 y almacena el ciclo. Se crea username/password y se intenta enviar la contraseña por correo.

Student::add selecciona un encargado a través del campo guargianID y lo guarda como student.parentID. Cada estudiante admite **un solo parentID**, mientras un padre puede estar referenciado por varios estudiantes. Los textos father_name y mother_name no son dos cuentas independientes ni dos relaciones verificadas.

Parents::index muestra padres y estudiantes filtrados por ciclo. Diversos módulos resuelven al padre por username + ciclo y luego buscan student.parentID = parentsID. Otros caminos no aplican exactamente la misma combinación. Eso produce diferencias entre pantallas cuando hay copias anuales o duplicados.

La nueva migración debe conservar que una fila parents puede representar una ficha familiar operada por una cuenta. No debe convertir automáticamente father_name y mother_name en dos adultos autenticables.

Referencias: mvc/controllers/Parents.php:122 y :131; mvc/controllers/Student.php:224; mvc/models/Parents_m.php:62.

### 4.4 Alta y edición de estudiante

Student::add guarda datos personales y académicos mezclados en student, fija usertypeID = 3, ciclo global, estado activo y banderas de biblioteca/residencia/transporte. Después crea studentrelation con datos redundantes de nombre, grado, sección y registro. El alta usa una transacción para esas dos escrituras.

La sección puede quedar en 0; el código ya contempla ausencia de sección. **El nuevo sistema debe soportar matrícula sin sección**, usando NULL en lugar del valor 0.

Campos que requieren traducción semántica, comprobada en etiquetas y código:

| Campo legado | Significado observado | Destino propuesto |
|---|---|---|
| student.dni | Código Personal | StudentExternalIdentifier, tipo LegacyPersonalCode; no asumir CUI/DPI. |
| student.state | Código Balam, generado inicialmente desde fecha/hora | Identificador externo legado; no departamento geográfico. |
| student.bloodgroup | Ciclo Escolar | Dato auxiliar de reconciliación del ciclo; nunca grupo sanguíneo. |
| student.bloodgroup2 | Grado textual | Etiqueta auxiliar a cotejar con classesID. |
| student.classesID | Grado estructurado | GradeLevel / destino de matrícula validado. |
| student.schoolyearID | Ciclo estructurado | Referencia principal del registro anual de origen. |
| student.parentID | Ficha/cuenta de encargado | Candidato a StudentGuardianRelationship tras validación. |
| parents.dni | DPI según la etiqueta de la interfaz | Identificador declarado del titular, pendiente de validar. |
| parents.father_name / mother_name | Nombres dentro de ficha familiar | Datos de origen; no crear identidades de adultos sin verificación. |

Student::edit actualiza student y studentrelation; parte de sus referencias usa ciclo seleccionado y otra parte ciclo global. Los identificadores de negocio no deben inferirse por el nombre de una columna inglesa.

Student::delete contiene una defensa reciente: inspecciona tablas que tienen columna studentID y evita borrar si encuentra historial. Eso mejora el comportamiento, aunque no equivale a FK ni cubre todas las referencias con nombres distintos, por ejemplo uploaderID.

Referencias: mvc/controllers/Student.php:270, :413 y :800; mvc/language/spanish/student_lang.php:25 y :77; mvc/language/spanish/parents_lang.php:48.

### 4.5 Ciclo escolar e historial

Existen dos contextos:

- setting.school_year: ciclo global de operación de la institución.
- session.defaultschoolyearID: ciclo seleccionado por el usuario para consultar.

Schoolyear::toggleschoolyear cambia el segundo en sesión y redirige al referente. Varias lecturas usan la sesión, pero altas de estudiantes, tareas, cabeceras de notas y otras escrituras toman el ciclo global. En consecuencia, estar viendo 2025 no garantiza que una escritura vaya a 2025.

Además, al cambiar el ciclo se vuelven a resolver estudiantes o padres por username y año. La navegación histórica depende de que existan copias compatibles. La sesión también es compartida por pestañas del navegador, por lo que cambiar el año en una afecta el contexto de otra.

El ciclo es una dimensión necesaria; lo que debe desaparecer es su uso como parte de la identidad y como estado global implícito de cada operación.

Referencias: mvc/controllers/Schoolyear.php:215; mvc/models/Signin_m.php:11; mvc/controllers/Mark.php:300 y :497.

### 4.6 Estructura académica

Classes administra grados y docente asociado. Section depende del grado. Subject almacena materia, grado, docente, nombre del docente y campos para mínimo/máximo/tipo; estos últimos están NULL en las 225 materias observadas. Estas tablas no están versionadas por ciclo.

Routine almacena horario por año, grado, sección, materia, docente, día y texto de hora. Los validadores de conflicto comparan combinaciones de inicio y fin; para el reemplazo se requiere detectar también intervalos parcialmente superpuestos.

Exam representa actualmente bimestres: examID 4 = primero, 7 = segundo, 3 = tercero y 2 = cuarto. **Ordenar por ID no ordena el año académico.** Examschedule agrega programación de exámenes y Eattendance su asistencia. Syllabus permite recursos/documentos por grado y ciclo.

Referencias: mvc/controllers/Classes.php; mvc/controllers/Subject.php; mvc/controllers/Routine.php:300; tablas exam, examschedule, eattendance y syllabus.

### 4.7 Cómo se crean y guardan las notas

Modelo actual:

~~~text
exam (bimestre) -----+
student ------------+--> mark (cabecera: año, alumno, grado, materia, bimestre)
subject ------------+      |
                            +--> markrelation (componente, valor textual)
                                   |
                                   +--> markpercentage (nombre y máximo)
~~~

1. En Mark::add se eligen bimestre, grado, sección y materia.
2. El controlador toma el ciclo global, obtiene estudiantes y crea cabeceras mark faltantes para el grupo.
3. Para cada cabecera se crean filas markrelation según el catálogo de componentes.
4. Hay un bloqueo GET_LOCK por ciclo/bimestre/grado/materia para reducir creación concurrente duplicada, y escrituras por lote.
5. La grilla carga valores y topes; el JavaScript limita el valor al máximo del componente.
6. Mark::mark_send recibe identificadores codificados en los inputs y actualiza markrelation para el ciclo global.
7. No se encontró en ese método una validación completa de rango, estado de cierre y asignación del docente equivalente a la que exige el nuevo diseño.
8. La consulta getMark también puede insertar relaciones de componentes faltantes al preparar una boleta. Una lectura puede modificar datos.

La configuración actual es ocho actividades de 5 puntos, parcial de 20 y examen bimestral de 40: **100 puntos posibles por materia y bimestre**. Aunque la tabla se llama markpercentage, se están capturando puntos máximos por componente; no multiplicar de nuevo por esos porcentajes.

Los valores están en varchar(128), las cabeceras carecen de unicidad natural y varios cálculos están en vistas. student_all_mark_array filtra solo por materia y getMarkRelationArray indexa por estudiante/componente, perdiendo la dimensión de bimestre. Son señales de fragilidad al reutilizar la consulta para varias unidades.

No hay una relación estructurada entre assignmentanswer y una calificación de markrelation. Entregar una tarea y capturar una nota son flujos separados en este modelo.

Referencias: mvc/controllers/Mark.php:300, :393, :482 y :492; mvc/models/Mark_m.php:100; mvc/views/mark/add.php:298; mvc/controllers/Mark.php:104.

### 4.8 Boletas y promoción actuales

Las boletas se generan en varias vistas: view, print_preview, print_nueva y print_all. Las nuevas impresiones suman componentes, omiten ciertas ausencias y usan redondeo; hay lógica académica distribuida entre plantillas.

Promotion::index guarda un promotionlog con grado/ciclo origen, destino, exámenes y componentes elegidos y mínimos por materia. El cálculo reúne notas y arma resultados. La vista marca aprobación con un promedio total >= 60, mientras el cálculo del controlador también tiene condiciones dependientes de mínimos y tipo de materia. Con esos campos NULL y reglas distribuidas **no puede darse por hecho que la promoción implementa el criterio por materia confirmado por el propietario**.

promotion_to_next_class toma estudiantes seleccionados y el log guardado en sesión, valida origen, abre transacción y:

- Busca una copia del padre en el año destino por DPI o username; si no encuentra, copia la fila parents.
- Busca copia del estudiante por username, código personal, registro o nombre/nacimiento.
- Si no existe, copia student, cambia grado/año, pone sección 0 y vincula al padre del nuevo ciclo.
- Crea studentrelation cuando falta y guarda los IDs promovidos en JSON del log.
- El modo enroll conserva el grado y cambia de ciclo; el otro usa el grado destino.

La rutina contiene protecciones, transacción y reutilización de copias para evitar algunos duplicados. Sin embargo, **duplica identidades de estudiante/padre por diseño**, depende de coincidencias de datos y no deja toda la regla de aprobación impuesta en el comando de promoción. El destino no debe aprobarse solo porque el navegador seleccionó un checkbox.

No se identificó una entidad específica de recuperación. El flujo de editar notas y volver a imprimir descrito por el propietario se sustituirá por recuperación explícita y emisión versionada.

Referencias: mvc/controllers/Promotion.php:166 y :352; mvc/views/promotion/add.php:96; mvc/models/Student_m.php:47; mvc/models/Parents_m.php:62.

### 4.9 Subida de tareas y entregas actuales

Assignment administra título, descripción, grado, materia, secciones serializadas en JSON, ciclo, fecha límite y un archivo. La carga permite documentos e imágenes y los guarda en uploads/images con nombre derivado de un valor aleatorio y hash. El límite configurado es 100024 KB.

El estudiante accede a tareas de su grado. assignmentanswer permite subir antes o el mismo día de deadlinedate. La fecha tiene precisión de día, sin una hora de vencimiento explícita. Si ya hay respuesta para usuario/tipo/tarea/ciclo se actualiza la fila; no hay modelo de intentos, estados o revisiones de entrega.

El padre puede consultar entregas del hijo en algunas ramas. Otras ramas obtienen estudiante o tarea por ID con verificaciones incompletas de relación y audiencia. download y answerdownload filtran por ID y ciclo, pero no aplican allí una comprobación de pertenencia equivalente a alumno/encargado/docente asignado.

La tabla assignmentanswer solo guarda archivo, uploader y fecha. No registra rúbrica, retroalimentación ni nota vinculada a la libreta. No se encontraron entregas reales en esta copia, así que existe evidencia de implementación, no de uso exitoso con alumnos.

Referencias: mvc/controllers/Assignment.php:70, :250, :409, :488, :647 y :672; mvc/models/Assignmentanswer_m.php:14.

### 4.10 Asistencia

Sattendance soporta asistencia diaria o por materia según setting.attendance. attendance y sub_attendance guardan un registro mensual con columnas a1 a a31; P y A representan presencia y ausencia. Hay operaciones individuales y masivas. Tattendance y Uattendance repiten el patrón para docentes y personal; Eattendance cubre exámenes.

Se encontró una incompatibilidad concreta: hay métodos que validan columnas con el patrón day + número, mientras el controlador y el esquema usan a + número. La captura puede rechazar esas llamadas. Otro método masivo compone SQL con columna y valores recibidos; no es un patrón que deba trasladarse.

Las tablas attendance, sub_attendance, tattendance y eattendance están vacías en la copia. Uattendance_m apunta a uattendance, tabla que no existe en el esquema observado. No se puede concluir que esos flujos de asistencia estén operando habitualmente.

Referencias: mvc/controllers/Sattendance.php:330 y :377; mvc/models/Sattendance_m.php:38, :59 y :71.

### 4.11 Cobros y pagos

Feetypes define conceptos. Invoice crea cargos por estudiante o grupo, con monto, descuento, año y paidstatus. Payment registra abonos ligados a una factura y estudiante. El pendiente se calcula restando descuento y pagos. paidstatus 0, 1 y 2 corresponde al cálculo sin pago, parcial y cubierto.

Invoice::payment permite ingreso administrativo de Cash, Cheque y Paypal, además de ramas de pasarelas. La ruta manual puede guardar Paypal como etiqueta sin demostrar una operación real en ese proveedor. **Los 10 pagos locales dicen Paypal, pero eso no prueba que los diez pasaran por PayPal.**

Paymenthistory permite editar o borrar pagos manuales y recalcula el estado de la factura dentro de una transacción. isEditablePayment excluye Stripe y PayUMoney, pero no representa un libro financiero inmutable. Invoice::delete puede borrar ciertas facturas; no equivale a una anulación fiscal/contable.

El modelo usa double para dinero, un pago apunta a una sola factura y no tiene asignaciones independientes, reversiones ni recibos inmutables como entidades separadas. Las mejoras de transacción existentes son útiles, pero no bastan para conciliación, concurrencia e idempotencia completas.

No se migrarán estos movimientos en el alcance actual. Sí se conservará su archivo con política de acceso y retención.

Referencias: mvc/controllers/Invoice.php:326, :511 y :745; mvc/controllers/Paymenthistory.php:16, :108 y :179; tablas invoice y payment.

### 4.12 Comunicación, archivos, avisos y eventos

- Conversation usa conversations, conversation_user y conversation_msg; permite borradores, enviados, favoritos, papelera, destinatarios y adjuntos. Hay verificación de membresía para ver una conversación, pero debe aplicarse también a cada mutación. No se encontró un contrato de tiempo real comparable al propuesto con SignalR.
- Message y reply_msg corresponden a otra estructura de mensajes usada por código de sesión/panel. La coexistencia requiere decidir un solo modelo nuevo de conversaciones.
- Mailandsms tiene envío segmentado, plantillas y registros; parte de la entrega se ejecuta en la solicitud web. Tener las ramas de SMS no acredita que exista un proveedor conectado en producción.
- Media permite carpetas, archivos y compartición por clase o pública mediante media_share.
- Notice genera avisos y alert permite registrar lecturas por username. Event y eventcounter manejan eventos y participación. Holiday registra feriados.
- La exclusión de uploads en .htaccess y las rutas usadas por vistas dejan los archivos en un árbol servido por el sitio; la aleatoriedad del nombre no es una política de autorización.

Referencias: mvc/controllers/Conversation.php; mvc/models/Conversation_m.php; mvc/controllers/Mailandsms.php; mvc/controllers/Media.php; sys-escolar/.htaccess.

### 4.13 Biblioteca, transporte, residencia y personal

- Book guarda cantidades y existencias disponibles; Lmember asocia alumno con membresía bibliotecaria; Issue registra préstamo/devolución y puede crear cargos por multa. Hay 1 libro, 4 membresías y 2 préstamos en la copia.
- Transport define ruta/vehículo/tarifa y Tmember vincula estudiantes; student.transport es una bandera redundante. Tablas sin filas observadas.
- Hostel, Category y Hmember modelan alojamiento, categorías y estudiantes; hay ajustes manuales de capacidad y banderas student.hostel. Tablas sin filas.
- Expense administra gastos. Manage_salary y Make_payment intentan gestionar salarios y pagos del personal. Faltan modelos cargados explícitamente: salary_template_m, salaryoption_m y hourly_template_m. Además no aparecen sus tablas ni manage_salary/make_payment en el esquema inspeccionado. No se puede presentar nómina como módulo funcional completo.
- Report cubre reportes de estudiantes, grado, asistencia y certificados. La rama de certificados referencia certificate_template_m, que no está entre los modelos disponibles. Requiere reconstrucción y validación.
- Backup produce una descarga de respaldo desde una solicitud web; no sustituye un plan de copias automáticas y restauración ensayada.
- Migration llama al mecanismo de migraciones de CodeIgniter por una ruta web. Las migraciones del reemplazo se ejecutarán desde el pipeline o una herramienta administrativa controlada.
- Exam_type e Instruction tienen modelos que apuntan a online_exam_type e instruction, tablas ausentes de esta base. La existencia de sus controladores tampoco demuestra un módulo operativo completo.

### 4.14 Configuración y recuperación de cuenta

Setting almacena pares campo/valor; Paymentsettings e ini_config mantienen parámetros de integraciones; Language cambia idioma; Menu permite modificar navegación. No debe confundirse menú configurable con permisos seguros.

Reset::password compara el token con un registro de reset, actualiza por correo en distintas tablas y limpia reset globalmente. La tabla no tiene expiración explícita por token. La nueva recuperación se construirá sobre Identity y una cuenta concreta, con expiración y uso controlados. Admin_Controller::usercreatemail incluye la contraseña en el mensaje de bienvenida: reemplazarlo por activación de un solo uso.

Referencias: mvc/controllers/Reset.php:115; mvc/libraries/Admin_Controller.php:168; mvc/controllers/Setting.php; mvc/controllers/Paymentsettings.php.

## 5. Hallazgos y consecuencias para el reemplazo

Las prioridades siguientes orientan diseño y contención. No son un informe de explotación ni una modificación autorizada del legado.

| ID | Prioridad | Evidencia | Cambio requerido |
|---|---|---|---|
| LEG-001 | Crítica | student y parents se copian entre años | Persona y cuenta estables; Enrollment anual separado. |
| LEG-002 | Crítica | Mark::view rama padre llama getMark con IDs sin comprobar vínculo del hijo | Autorizar cada recurso en backend, también PDF, exportación y descarga. |
| LEG-003 | Alta | Acción ausente del conjunto de permisos no se deniega por defecto en Admin_Controller | FallbackPolicy autenticada, permisos explícitos y pruebas negativas. |
| LEG-004 | Alta | csrf_protection = FALSE; cookie_secure y cookie_httponly = FALSE en configuración | Cookies seguras y antiforgery; revisar configuración efectiva de despliegue. |
| LEG-005 | Alta | Hash SHA-512 con clave compartida y contraseñas enviadas por correo | Identity, activación segura y renovación de credenciales. |
| LEG-006 | Alta | Carga y descargas en árbol público con controles incompletos de audiencia | Almacenamiento privado, cuarentena y acceso autorizado. |
| LEG-007 | Alta | Cálculo de promoción y aprobación en vistas con criterios distintos | Una única política académica versionada y ejecutada en servidor. |
| LEG-008 | Alta | Recuperación no modelada como evaluación separada | RecoveryAttempt, resultado efectivo y boleta revisada. |
| LEG-009 | Alta | 0 FK y 0 UNIQUE de negocio | Restricciones, claves compuestas, transacciones y controles de concurrencia. |
| LEG-010 | Alta | Ciclo global vs ciclo en sesión para escribir/leer | Contexto anual explícito en URL/comando; no sesión global mutable. |
| LEG-011 | Alta | 101 notas y 12 facturas sin estudiante | Archivo separado; migración acotada, sin reconstrucciones inventadas. |
| LEG-012 | Alta | Lecturas getMark crean datos | GET sin efectos; preparación explícita de libretas mediante comando. |
| LEG-013 | Media | bloodgroup y state tienen significado local distinto | Mapeo semántico revisable y conservación del origen. |
| LEG-014 | Alta | Sin credenciales de estudiante y falta usertype 3 | Activación de cuentas estudiantiles nuevas; no copiar roles numéricos. |
| LEG-015 | Media | Modelos/tablas de nómina ausentes | Marcar extensión futura; no estimar como funcionalidad ya existente. |
| LEG-016 | Alta | Edición/borrado de pagos y dinero double | Finanzas con decimal, asignaciones, reversión e idempotencia. |
| LEG-017 | Media | Zona horaria distinta y vencimientos de tareas solo por fecha | UTC para instantes, Guatemala para calendario y presentación. |
| LEG-018 | Alta | 209 de 211 padres sin correo y 173 sin teléfono | Onboarding asistido, sin asumir canales digitales verificados. |

También hay mejoras ya presentes que vale la pena entender: transacciones de alta/promoción/pagos, bloqueo de creación de notas, consultas por lote, defensas de borrado y mecanismos de respaldo para studentrelation incompleto. No se afirma que el legado carezca de toda protección. El reemplazo debe convertir esas reparaciones locales en reglas de dominio y restricciones sistemáticas.

## 6. Elección tecnológica: MVC, Angular, Flutter y Redis

### 6.1 Qué papel cumple cada tecnología

| Tecnología | Responsabilidad | No debe decidir |
|---|---|---|
| ASP.NET Core | API, autenticación, autorización, casos de uso, validaciones y procesos | Aspectos visuales específicos de cada cliente. |
| MVC/Razor Pages | Alternativa para renderizar la web en servidor; también UI de Identity | Reglas académicas copiadas en vistas. |
| Angular | Interfaz web, formularios, navegación, grillas y estado de presentación | Autorización real, notas oficiales, promociones o saldos. |
| Flutter | Aplicación Android/iOS y experiencia móvil | Un segundo backend con reglas independientes. |
| SQL Server | Persistencia transaccional y restricciones | Guardar archivos grandes como BLOB por conveniencia. |
| Redis | Caché y coordinación temporal según necesidad | Ser el único lugar donde existan notas, permisos, entregas o trabajos críticos. |
| Object storage privado | Contenido de archivos y documentos emitidos | Conceder acceso sin revisar permisos. |

**Angular no centraliza servicios de negocio: eso lo hace .NET.** Los servicios pueden ser reutilizados por MVC, Angular, Flutter y procesos del servidor mediante contratos y casos de uso comunes. Exponer una API no sacrifica seguridad si se autentica, autoriza y valida cada operación correctamente.

### 6.2 Comparación aplicable al colegio

| Criterio | .NET MVC/Razor + Flutter | .NET API + Angular + Flutter |
|---|---|---|
| Equipo inicial | Conveniente para equipo pequeño centrado en C# | Conviene contar con capacidad real de frontend TypeScript |
| Diseño visual | Puede alcanzar el mismo nivel de calidad | Puede alcanzar el mismo nivel de calidad |
| Formularios administrativos sencillos | Menos piezas | Más infraestructura de frontend |
| Grilla de notas con pegado, conflictos, filtros y borradores | Requiere JavaScript especializado en esas pantallas | Encaja bien con componentes y estado de cliente |
| Experiencia de portal sin recargas completas | Posible mediante mejoras progresivas | Natural para una aplicación SPA |
| Seguridad | Cookie + antiforgery + permisos por recurso | Cookie + antiforgery en mismo origen + permisos por recurso |
| App Flutter | La misma API .NET | La misma API .NET |
| Compartir código visual con Flutter | No | No; se comparten contratos y reglas de servidor |
| Costo de mantener clientes | Web y móvil | Web y móvil, con frontend web independiente |
| Escalado del negocio | Depende del backend y los datos | Depende del backend y los datos |

**Selección propuesta:** Angular para la web del producto completo descrito aquí. Usar componentes accesibles y un sistema visual propio; no adoptar una plantilla enorme como arquitectura. No construir simultáneamente una web completa MVC y otra Angular.

**Alternativa de menor costo inicial:** mantener exactamente los módulos, casos de uso y API del diseño, y usar MVC/Razor para la web, con TypeScript en grilla de notas y carga de archivos. Los controllers MVC invocan los casos de uso en proceso; no deben llamar por HTTP a su propio servidor para compartir reglas con Flutter. Una decisión por MVC cambia la presentación, no el modelo académico.

La decisión final deberá registrarse como ADR-002 antes de crear la web. El valor predeterminado de este plan es Angular; no es necesario detener todo el trabajo mientras se implementan dominio, contratos y autenticación.

### 6.3 Versiones y soporte

- **.NET 10 LTS** como línea base; verificar y fijar el parche y SDK al iniciar. La política oficial identifica soporte de .NET 10 hasta noviembre de 2028; no iniciar un proyecto nuevo en .NET 8/9 cuando su soporte termina el 10 de noviembre de 2026. [Política oficial de soporte de .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
- **EF Core de la misma generación compatible**, con proveedor Microsoft.EntityFrameworkCore.SqlServer, paquetes fijados y migraciones revisadas.
- **Angular 22** es una línea activa según el calendario consultado. Fijar versión exacta y versiones compatibles de Node, TypeScript y componentes, verificando la tabla oficial en el momento de creación. No interpretar un lanzamiento programado como ya publicado. [Versiones y soporte de Angular](https://angular.dev/reference/releases), [matriz de compatibilidad](https://angular.dev/reference/versions).
- **Flutter stable**: elegir la versión efectivamente publicada y compatible al iniciar la app. No imponer un número futuro; el archivo oficial distingue stable, beta y main y contiene un calendario de lanzamientos que no equivale a disponibilidad. [Archivo oficial del SDK Flutter](https://docs.flutter.dev/install/archive).
- **SQL Server/Azure SQL**: elegir edición soportada, licencia, límites de recursos y compatibilidad en ADR-003. El plan no depende de una característica exclusiva de la edición más costosa. Desarrollo e integración deben usar el mismo motor de producción.
- **Redis**: elegir servicio/versión mantenida y revisar licencia del producto o imagen concretos. Integrarlo detrás de IDistributedCache/HybridCache donde corresponda. No instalarlo por una expectativa abstracta de velocidad. [HybridCache en ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0).

### 6.4 Uso concreto de Redis

Inicio: caché de catálogos y proyecciones costosas con claves que incluyan colegio, ciclo, usuario/alcance y versión. Invalidar al cambiar el origen y usar TTL acotado. No compartir respuestas privadas de un alumno con otro por una clave incompleta.

Con más de una instancia: puede servir como caché distribuida, almacenamiento temporal de presencia y backplane de SignalR si se adopta ese despliegue. El limitador integrado de una instancia no se convierte automáticamente en limitador distribuido: coordinar límites en gateway/servicio adecuado.

La cola durable, outbox, resultados de importación, recibos y decisiones de promoción quedan en SQL Server. Si Redis cae, las lecturas comunes usan SQL con límites de carga; presencia y otras funciones accesorias se degradan. Si se elige guardar tickets de sesión en Redis, su caída puede cerrar sesiones o impedir acceso: documentar esa dependencia y no prometer continuidad de login sin probarla. Para el inicio se propone sesión validada con datos durables en SQL y caché opcional.

## 7. Arquitectura del monolito modular

### 7.1 Topología inicial

~~~mermaid
flowchart TD
    Web[Web Angular] -->|HTTPS mismo origen y cookie| Host[ASP.NET Core Host]
    Login[Identity UI en Razor Pages] --> Host
    Mobile[Flutter] -->|HTTPS y access token| Host
    Host --> IAM[Identidad y acceso]
    Host --> Directory[Personas y familias]
    Host --> Enrollment[Inscripciones y matrícula]
    Host --> Academics[Organización académica]
    Host --> Learning[Tareas y entregas]
    Host --> Assessment[Notas y promoción]
    Host --> Communication[Comunicación]
    Host --> Finance[Finanzas]
    IAM --> SQL[(SQL Server)]
    Directory --> SQL
    Enrollment --> SQL
    Academics --> SQL
    Learning --> SQL
    Assessment --> SQL
    Communication --> SQL
    Finance --> SQL
    Host --> Cache[(Redis opcional)]
    Host --> Storage[Almacenamiento privado]
    Worker[Worker del mismo producto] --> SQL
    Worker --> Storage
    Worker --> Providers[Correo y push]
~~~

Un despliegue inicial puede ejecutar Host y workers en un proceso. Si PDFs o escaneo compiten con solicitudes web, ejecutar el worker como otro proceso con el mismo código y versión de producto. Eso no exige separar dominios en microservicios.

No agregar Kubernetes, service mesh, broker externo, microfrontends, GraphQL, event sourcing completo ni un clúster de búsqueda en la primera entrega sin un requisito demostrado.

### 7.2 Propiedad y contratos de módulos

| Módulo / esquema | Propietario de datos | Contratos públicos principales |
|---|---|---|
| IdentityAccess / iam | ApplicationUser, membresías, permisos, invitaciones, sesiones, clientes OIDC | CurrentActor, autorización, invitaciones y revocaciones |
| Directory / people | Personas, perfiles, contactos, identificadores y relaciones de encargados | StudentSummary, GuardianAccess, validación de identidad |
| Enrollment / enrollment | Admisión, inscripción, matrícula y movimientos de ubicación | EnrollmentStatus, vigencias y listado elegible |
| Academics / academic | Colegio/sedes, ciclos, calendarios, planes, grados, grupos, cursos, docentes y horarios | CourseRoster, AcademicContext, AcademicYearStatus |
| Learning / learning | Tareas, audiencia, entregas, intentos, retroalimentación | AssignmentStatus, SubmissionOutcome |
| Assessment / assessment | Esquemas de evaluación, calificaciones, resultados, recuperación, publicación y promoción | PublishedResult, PromotionDecision, GradebookVersion |
| Communication / communication | Conversaciones, avisos, destinatarios, notificaciones y preferencias | EnqueueNotification, AnnouncementAudience |
| Finance / finance | Cargos, pagos, aplicaciones, recibos y reversiones | BalanceSummary y FinancialClearance obligatoria para consultar notas familiares/estudiantiles |
| Attendance / attendance | Sesiones de asistencia, marcas y justificaciones | AttendanceSummary, ausencia notificada |
| Reporting / reporting | Proyecciones de consulta y solicitudes de reporte | ReportJob y documentos emitidos |
| Platform services | Archivos, outbox, auditoría técnica de negocio, reloj y proveedores | IFileStore, IAuditWriter, IClock, INotificationTransport |

Biblioteca, transporte, residencia, RR. HH. e inventario se agregan después como módulos propios cuando su alcance se confirme. Platform services no debe convertirse en un módulo que contiene las reglas de todos los demás.

### 7.3 Reglas de dependencia

1. Cada módulo escribe únicamente sus tablas y oculta sus entidades/DbContext internos.
2. Otros módulos consumen contratos públicos; no importan repositorios o entidades internas.
3. La API es una frontera externa; llamadas dentro del monolito son en proceso.
4. Una consulta institucional puede combinar proyecciones de reporting publicadas por contratos/eventos. Cualquier vista SQL transversal excepcional se declara como contrato de lectura con propietario; no abrir acceso informal a todos los esquemas.
5. Las FK entre referencias estables de módulos se permiten cuando protegen integridad, con migraciones coordinadas. Una FK no autoriza a consultar o escribir internals ajenos.
6. Invariantes que deben cumplirse inmediatamente usan validación síncrona y una transacción local acordada. No poner una regla crítica detrás de un evento que puede llegar tarde.
7. El envío de correo, PDF o push se hace después del commit mediante outbox. Un proveedor caído no revierte una nota ya guardada.
8. Evitar un SharedKernel con Student, Enrollment o Grade compartidos por todos. Compartir solo primitivas y contratos deliberados.

### 7.4 Organización física recomendada

~~~text
src/
  SchoolPlatform.Host/
    IdentityUI/                    # integración/páginas de acceso
    Composition/                   # registro de módulos y políticas
  SchoolPlatform.Worker/           # opcional como proceso separado
  BuildingBlocks/                 # primitivas pequeñas y contratos técnicos
  Modules/
    Directory/
      Contracts/
      Domain/
      Features/RegisterStudent/
      Features/LinkGuardian/
      Infrastructure/
    Enrollment/
    Academics/
    Assessment/
    Learning/
    Attendance/
    IdentityAccess/
    Communication/
    Finance/
    Reporting/
web/                              # Angular por funcionalidades
mobile/                           # Flutter cuando se active su fase
tools/Migration.Cli/
tests/                            # integración, dominio, E2E, arquitectura
docs/adr/
infra/
~~~

Empezar con un proyecto de implementación y otro de contratos por módulo relevante, o carpetas internas si el alcance inicial es muy pequeño. Separar ensamblados ayuda a hacer cumplir límites; no crear decenas de proyectos vacíos para aparentar modularidad. Usar vertical slices por caso de uso y entidades de dominio donde haya reglas reales.

EF Core ya ofrece seguimiento y unidad de trabajo. Evitar un repositorio genérico universal que oculte transacciones, consultas e índices. MediatR y bibliotecas de validación son opcionales; revisar mantenimiento y licencia de la versión elegida antes de adoptarlas.

### 7.5 Transacciones, eventos y tareas durables

Ejemplo: publicar notas produce, en la misma transacción, publicación, auditoría y OutboxMessage. Un worker entrega GradebookPublished a comunicación/reporting. Si el proceso cae después del envío pero antes de confirmar el mensaje, puede reenviar: los consumidores usan InboxMessage o una clave única para deduplicar.

No prometer entrega exactamente una vez. Implementar **al menos una vez con efectos idempotentes**. Las notificaciones pueden llegar tarde; la disponibilidad de la nota o tarea en el portal no depende de que llegue el correo.

Los workers necesitan reserva con lease, reintentos, backoff, máximo de intentos, cola de errores, reanudación y métricas. Fecha de ejecución, identidad de colegio y política de acceso deben viajar con el trabajo; no tomar el ciclo actual del servidor al procesarlo días después.

## 8. Identidad, cuentas y autorización

### 8.1 Diferencias esenciales

- **Persona:** ser humano dentro del colegio, con datos de expediente.
- **Perfil:** estudiante, encargado, docente o personal; una misma persona puede tener varios.
- **Cuenta:** credencial de acceso opcional, administrada por Identity.
- **Membresía institucional:** acceso de la cuenta al colegio y sus funciones.
- **Relación familiar:** facultades de una persona respecto de un estudiante concreto y durante una vigencia.
- **Matrícula:** participación académica de un estudiante en un ciclo. No es una cuenta.

Un estudiante puede existir sin cuenta; un adulto estudiante puede activar cuenta sin padre; una madre docente no necesita dos contraseñas. Un hermano mayor que también estudia puede tener su propio perfil y ser contacto autorizado de otro alumno, si el colegio valida esas facultades.

### 8.2 Identity con scaffolding, bien integrado

Usar ASP.NET Core Identity con ApplicationUser propio, almacén EF Core en esquema iam, UserManager y SignInManager. Scaffold únicamente las páginas que se personalizan: login, logout, recuperación, confirmación, activación y administración de seguridad. Aplicar el mismo sistema visual y accesibilidad de la web.

El scaffolding genera base de UI; aún hay que implementar proveedores de correo, políticas, invitaciones, recuperación asistida y pruebas. No crea por sí solo permisos académicos ni un servidor OIDC. [Scaffolding oficial de Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/scaffold-identity?view=aspnetcore-10.0).

Ejemplo orientativo, después de instalar la herramienta y paquetes compatibles y conectar el DbContext concreto:

~~~powershell
dotnet aspnet-codegenerator identity -dc SchoolPlatform.IdentityAccess.IdentityDbContext --files "Account.Login;Account.Logout;Account.ForgotPassword;Account.ResetPassword"
~~~

No habilitar registro público irrestricto que permita declarar un SchoolAdmin o asociarse a un hijo. Las rutas de registro/activación deben consumir invitación validada. Deshabilitar las páginas de registro por defecto si exponen una alternativa al flujo controlado.

### 8.3 Autenticación web

Publicar Angular y .NET bajo el mismo origen HTTPS. El Host valida la cookie de sesión HttpOnly, Secure y SameSite apropiado. Angular nunca necesita leer la cookie ni almacenar un access token en localStorage.

Las solicitudes que cambian estado requieren antiforgery. Dar un endpoint para obtener el token de petición y enviarlo en encabezado; esa pieza puede ser visible a JavaScript, a diferencia de la cookie de autenticación. Proteger login/logout, uploads completados y cualquier acción con efectos. No usar GET para publicar, borrar, promover o cambiar permisos.

En este despliegue, el Host sirve de frontera de sesión para la web y ejecuta directamente los casos de uso. No hace falta añadir un proxy BFF que se llame por HTTP a sí mismo. Si después se separa un frontend servidor y una API en distintos despliegues, documentar BFF y conservación de tokens en el servidor.

Microsoft recomienda cookies para clientes de navegador y advierte que los tokens de las API simplificadas de Identity usan un formato propietario; no asumir que MapIdentityApi equivale a OAuth/OIDC o JWT estándar. [Identity y API para SPA](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0).

### 8.4 Autenticación móvil

Cuando se construya Flutter, integrar **OpenIddict** con Identity en el mismo producto o un proveedor OIDC institucional compatible si se decide externalizarlo. Recomendación base: OpenIddict para conservar Identity y control de las cuentas.

Flujo Authorization Code + PKCE mediante navegador del sistema, redirect URI registrada y app/universal links cuando sea viable. No usar una WebView embebida para pedir contraseñas ni un client secret incluido en Flutter. El servidor valida state, nonce cuando corresponda, issuer, audience, vigencia, PKCE y redirect URI.

Guardar refresh token con almacenamiento seguro de plataforma. Usar access tokens breves y renovación controlada; detectar reutilización del refresh token y revocar la sesión/dispositivo según la política implementada. El número exacto de minutos y días se define y prueba como política operacional.

La misma API admite cookie web o bearer móvil mediante esquemas explícitos, sin convertir cookies en tokens improvisados. El recurso comprueba membresía, permisos y vínculo vigente después de autenticar. Para revocación inmediata en operaciones sensibles, comprobar sesión/estado durable y validación de token compatible; un JWT autocontenido que solo expira no revoca acceso instantáneamente.

[Flujos recomendados por OpenIddict](https://documentation.openiddict.com/guides/choosing-the-right-flow.html), [integración con ASP.NET Core](https://documentation.openiddict.com/integrations/aspnet-core.html).

### 8.5 Roles, permisos y alcance

| Función | Alcance inicial |
|---|---|
| Administrador institucional | Configuración y administración de cuentas según permisos delegados |
| Dirección | Consulta institucional, aprobación de cierres y excepciones |
| Coordinación académica | Planes, carga docente, revisión y publicación autorizada |
| Secretaría | Personas, inscripciones, documentos y asignaciones permitidas |
| Docente | Cursos asignados, tareas, asistencia y notas dentro de vigencia |
| Estudiante | Información propia, entregas, mensajes permitidos y resultados publicados |
| Encargado | Estudiantes vinculados y facultades concedidas para cada uno |
| Caja/contabilidad | Cobros, conciliación y reversiones autorizadas |
| Soporte/auditor | Alcance temporal explícito, sin acceso universal por defecto |

Permisos ejemplo: students.read, students.manage, guardians.link, accounts.invite, enrollments.manage, assignments.publish, submissions.review, grades.capture, grades.publish, grades.correct, recoveries.record, promotions.confirm, finance.collect, finance.reverse, audit.read.

La regla no es solo «tiene rol Docente». Debe ser: cuenta activa + colegio autorizado + permiso + curso asignado + período habilitado + recurso perteneciente a ese alcance. Un rol Guardian necesita además una relación activa con el estudiante y permiso para el tipo de información solicitado.

Revisar privilegios en servidor en cada operación, incluidos IDs en cuerpo, filtros, descargas, hubs SignalR y trabajos de exportación. Usar denegación por defecto y pruebas de acceso horizontal. [Guía de autorización de OWASP](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html).

### 8.6 Activación y recuperación sin correo disponible

1. Secretaría valida la persona y el canal de entrega; registra quién verificó y cómo, sin copiar documentos innecesariamente.
2. Genera una invitación aleatoria de suficiente entropía, de un uso, con expiración, propósito, colegio y persona destino. Guardar hash del secreto.
3. Entrega un enlace/QR o código seguro mediante un canal verificado o en persona; limitar intentos. El QR no contiene el expediente ni IDs que por sí solos concedan acceso.
4. El destinatario fija sus credenciales y confirma el canal cuando lo tenga. Puede usar username si no dispone de correo propio.
5. El registro queda Active; la invitación se consume atómicamente y no sirve para otra persona.
6. Si es encargado, los hijos aparecen por relaciones aprobadas previamente. No se crean vínculos por saber nombre, fecha de nacimiento o código del alumno.
7. Recuperación asistida aplica verificación de identidad y revocación; no entregar contraseña común ni aceptar el correo de un tercero sin confirmar su facultad.

Correo de contacto familiar y correo de recuperación de una cuenta son conceptos distintos. Si varias personas comparten un teléfono/correo, no convertirlo automáticamente en identificador único de todas ellas. La política de login por correo debe restringirse a correos de recuperación verificados y no ambiguos.

MFA obligatorio para administración privilegiada y operaciones de alto impacto; disponible para otros perfiles. No exigir a un niño un dispositivo que no posee para usar un flujo escolar autorizado.

### 8.7 Mayoría de edad y autonomía

La cuenta propia del estudiante no depende del padre ni del ciclo. La edad/nivel puede orientar el onboarding, pero no verifica identidad por sí sola. Permitir acceso de menores con política institucional apropiada, sin asumir que «básico» equivale a adulto.

Al cumplir la edad o condición definida por la política aprobada, iniciar revisión de facultades del encargado y consentimiento cuando corresponda. No transferir automáticamente la contraseña ni cortar acceso familiar sin política y aviso. Consultar la regla legal aplicable antes de automatizar efectos de mayoría de edad; este documento no certifica una obligación jurídica concreta.

## 9. Modelo de datos propuesto

### 9.1 Identidad y personas

| Entidad | Campos y propósito esenciales |
|---|---|
| School | Id, nombre, zona horaria, moneda, estado |
| Campus | SchoolId, Id, nombre y ubicación |
| ApplicationUser | Id global, NormalizedUserName, PasswordHash de Identity, SecurityStamp, estado y datos de seguridad |
| SchoolMembership | SchoolId, UserId, vigencia, estado y versión de permisos |
| UserPersonLink | SchoolId, UserId, PersonId; vínculo verificado entre credencial y persona del colegio |
| Person | SchoolId, Id, nombres/apellidos, nombre de presentación, nacimiento opcional, estado |
| PersonContact | SchoolId, PersonId, tipo, valor, verificación, uso y preferencia |
| PersonIdentifier | SchoolId, PersonId, tipo, emisor, valor protegido y referencia de origen |
| StudentProfile | SchoolId, PersonId, código institucional estable, estado de expediente |
| GuardianProfile | SchoolId, PersonId y datos necesarios de encargado |
| StaffProfile / TeacherProfile | SchoolId, PersonId, vinculación laboral/docente y estado |
| StudentGuardianRelationship | SchoolId, StudentId, GuardianId, parentesco, inicio/fin, validación y facultades |
| GuardianAccessGrant | Relación, ámbito de datos, vigencia y fundamento de autorización |
| Invitation / AccountSession | Propósito, usuario/persona destino, secreto protegido, vigencia y revocación |

Person se limita al colegio. Si en el futuro hay varios colegios independientes, evitar un directorio global de menores que los correlacione sin base legítima. ApplicationUser puede pertenecer a varios colegios, pero sus vínculos a personas y permisos son institucionales y explícitos.

La ficha familiar importada ambigua puede quedar en revisión antes de crear GuardianProfile autenticable. Conservar el dato de origen no demuestra quién controla legalmente la cuenta. El directorio debe soportar contacto sin cuenta, parentesco sin permiso de portal y responsable de pago sin facultad académica.

### 9.2 Organización y matrícula

| Entidad | Propósito |
|---|---|
| AcademicYear | SchoolId, Id, etiqueta, inicio/fin, estado y ciclo predeterminado |
| AcademicPeriod | AcademicYearId, orden explícito, fechas, peso y estado de captura/publicación |
| EducationLevel / GradeLevel / Program | Preprimaria/primaria/básico/diversificado y sus planes configurables |
| CurriculumVersion / CurriculumSubject | Plan por vigencia, materias obligatorias/electivas y reglas de evaluación |
| Shift / Section / ClassGroup | Jornada, sección visible opcional y grupo operativo del ciclo |
| Enrollment | SchoolId, StudentId, AcademicYearId, ingreso efectivo, estado y versión |
| EnrollmentPlacement | EnrollmentId, sede, grado, jornada, SectionId opcional, intervalo de vigencia y motivo |
| CourseOffering | Curso impartido de una materia/plan en ciclo, sede y grupo |
| TeachingAssignment | Docente, curso, función e intervalo de vigencia |
| CourseEnrollment | Matrícula en un curso e inicio/fin efectivos |
| ScheduleEntry | Curso, aula, día e intervalo de hora; calendario de excepciones |

Una matrícula anual canónica por estudiante/colegio/ciclo. Los cambios de sección, grado, retiro y reingreso se modelan como eventos/ubicaciones con vigencia, no creando personas ni dos matrículas activas equivalentes. Si se requiere cursar programas simultáneos, ampliar explícitamente la clave con ProgramId antes de habilitarlo.

ClassGroup es una agrupación operativa que puede existir sin sección visible. No inventar «Sección A» para satisfacer una columna. CourseEnrollment es la fuente verificable de quién pertenece al curso y desde cuándo.

### 9.3 Evaluación y aprendizaje

| Entidad | Propósito |
|---|---|
| GradingPolicyVersion | Escala, redondeo, pesos, nota mínima y tratamiento de estados especiales |
| Gradebook / GradebookItem | Curso/período, componentes, máximos, pesos y estado |
| AssessmentScore | Ítem + matrícula de curso, puntos decimal opcional y estado separado |
| ScoreRevision | Valor anterior/nuevo, razón, actor, fecha y versión |
| PeriodSubjectResult | Total del bimestre por materia, integridad y versión de cálculo |
| AnnualSubjectResult | Promedio ordinario de los cuatro bimestres, nota final efectiva y procedencia |
| RecoveryAttempt | Materia/resultado anual, intento, fecha, nota, estado y aprobación |
| PublicationBatch | Publicación consistente de resultados y destinatarios |
| ReportCardIssue | Emisión/versionado, snapshot, plantilla, archivo y relación de sustitución |
| PromotionBatch / PromotionDecision | Previsualización, resolución por alumno y ejecución autorizada |
| Assignment / AssignmentTarget | Actividad, curso/audiencia, publicación, vencimiento y políticas |
| AssignmentRecipient | Estudiante/matrícula destinataria, exigibilidad, asignación efectiva y plazo individual |
| Submission / SubmissionAttempt | Entrega, intentos, actor, estado y marca de tiempo del servidor |
| SubmissionAttachment / Feedback | Archivos seguros y retroalimentación privada o pública según política |
| AssessmentSourceLink | Relación explícita entre tarea/rúbrica y un ítem de libreta |

Estados de nota separados del número: Pending, Scored, Missing, Excused, NotEnrolled, TransferredCredit y UnderReview. El cero es una calificación válida. Un valor NULL requiere estado; no se usa para esconder si fue exonerado o quedó pendiente.

### 9.4 Entidades transversales y futuras

- FileAsset: colegio, propietario, categoría, storage key, MIME detectado, bytes, checksum, estado de escaneo, retención y versión.
- AttendanceSession / AttendanceRecord: sesión real por fecha/curso o jornada y estado por estudiante; sin columnas a1..a31.
- OutboxMessage / InboxMessage / Job: efectos durables, deduplicación y ejecución observable.
- AuditEvent: actor, colegio, acción, recurso, motivo y valores mínimos protegidos.
- LegacyImportBatch / LegacyIdentityMap / ImportReviewItem: procedencia y conciliación.
- Conversation / Participant / Message / ReadReceipt: comunicación con miembros autorizados.
- Invoice / InvoiceLine / Payment / PaymentAllocation / Receipt / Reversal: finanzas del núcleo con independencia de la identidad anual; permiten controlar solvencia por publicación.

### 9.5 Restricciones e índices mínimos

| Restricción | Regla |
|---|---|
| UNIQUE (SchoolId, StudentCode) | Código institucional estable por colegio |
| UNIQUE (SchoolId, StudentId, AcademicYearId) | Una matrícula anual canónica en el alcance inicial |
| UNIQUE (SchoolId, UserId) en vínculo principal de persona | Una cuenta no se enlaza a varias personas distintas del mismo colegio por accidente |
| UNIQUE (SchoolId, CourseOfferingId, EnrollmentId, StartDate) | Evitar membresías idénticas; controlar además solapamientos |
| UNIQUE (GradebookItemId, CourseEnrollmentId) | Un resultado actual por componente y alumno |
| UNIQUE (AssignmentId, CourseEnrollmentId) | Una obligación de entrega por alumno destinatario |
| UNIQUE (SubmissionId, AttemptNumber) | Intentos correlativos sin colisión |
| UNIQUE (SchoolId, IdempotencyScope, IdempotencyKey) | Evitar duplicación de comandos reintentados |
| UNIQUE (SchoolId, EnrollmentId, FeeConceptId, BillingPeriodId, Occurrence) | Una obligación automática por concepto/cuota, incluso si cambia el plan o el lote |
| UNIQUE (SchoolId, LegacySource, LegacyEntity, LegacyId) | Trazabilidad de extracción e importación |
| CHECK de puntos y máximos locales | Puntos >= 0; topes por ítem y política en caso de uso transaccional |
| CHECK de vigencias | Fin posterior a inicio; sin intervalos negativos |
| FK compuesta con SchoolId en referencias institucionales | No mezclar colegio en relaciones físicas |

Los solapamientos temporales no se resuelven únicamente con una UNIQUE por fecha inicial: bloquear/serializar la asignación relevante y comprobar intervalos dentro de la transacción. Para sección opcional, la FK incluye colegio/ciclo/grado cuando existe SectionId; el dominio también verifica esos atributos. Diseñar índices filtrados para restricciones que involucren NULL según la semántica de SQL Server.

Agregar índices según consultas: matrículas por año/estado/grado; curso por año/docente; destinatarios por alumno/estado/plazo; scores por libreta; mensajes por conversación/fecha/ID; auditoría por colegio/recurso/fecha. Medir planes; no indexar todas las columnas.

### 9.6 Tipos, concurrencia y documentos históricos

- Ids estables opacos; GUID/UUID es una opción, no un mecanismo de permisos. Elegir estrategia de índice que evite fragmentación excesiva.
- Dinero decimal(19,4) con moneda; cantidades y redondeos de presentación según política monetaria.
- Notas decimal con precisión definida, por ejemplo decimal(7,3), sin double; no redondear repetidamente durante cálculos intermedios.
- DateOnly para nacimiento/fecha escolar; DateTimeOffset o instantes UTC para publicación y entrega; zona IANA institucional para reglas locales.
- rowversion en agregados editables y versión de libreta/publicación para operaciones masivas. Devolver conflicto si cambió el recurso.
- Nombres, grado, institución y fórmula de una boleta emitida se conservan en snapshot. Actualizar el nombre actual de la persona no reescribe el PDF emitido.
- No hacer cascade delete de estudiantes hacia notas, pagos o documentos emitidos. Retiro y archivo son estados de negocio; borrado/anonimización se gobierna por política aprobada.

EF Core permite concurrencia optimista y SQL Server rowversion; manejar el conflicto en el caso de uso y en la interfaz, no simplemente reintentar sobrescribiendo al otro docente. [Documentación de concurrencia de EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

## 10. Ciclos e historial

### 10.1 Mantener el ciclo como filtro, quitar su dependencia de la sesión

El colegio debe tener un ciclo operativo predeterminado. El usuario puede cambiar el filtro a 2025 o abrir un historial completo, pero **cada URL y cada comando lleva su ciclo o recurso académico explícito**.

Ejemplos de navegación:

~~~text
/portal/students/{studentId}/overview
/portal/students/{studentId}/years/{academicYearId}/grades
/admin/years/{academicYearId}/enrollments
/teacher/years/{academicYearId}/courses/{courseId}/gradebook
~~~

La pantalla de expediente muestra una línea temporal de ciclos. Una boleta tiene enlace directo a su emisión. La preferencia de año recuerda navegación; no decide en silencio a qué año se escribe. Abrir otra pestaña no cambia un formulario ya abierto.

El servidor verifica que curso, matrícula, período y ciclo son compatibles. Si el año en la URL difiere del recurso, rechazar y explicar; nunca mover la escritura al ciclo global.

### 10.2 Estados independientes

| Recurso | Estados propuestos |
|---|---|
| Ciclo | Draft -> EnrollmentOpen -> Active -> Closing -> Closed -> Archived |
| Período | Planned -> CaptureOpen -> Review -> Published -> Locked |
| Matrícula | Draft -> PendingReview -> Active -> Withdrawn / Completed / Cancelled |
| Curso | Planned -> Active -> Closed |

Un ciclo puede admitir preparación del siguiente mientras el anterior sigue activo. Ciclo predeterminado no equivale a único ciclo editable. El permiso y estado del recurso gobiernan la operación.

Después de cierre, lectura continúa disponible según facultades. Una corrección excepcional usa apertura limitada o flujo de rectificación con motivo, aprobación y vencimiento; no «desbloquear todo el año» para editar una nota.

### 10.3 Acceso histórico familiar

El expediente del alumno permanece estable, pero el acceso del encargado tiene vigencia y ámbito. No asumir que ser encargado hoy autoriza consultar cualquier documento antiguo, ni que haberlo sido en 2025 permite ver 2026 después de una revocación.

Definir por relación si puede ver historial académico publicado, finanzas y documentos. Registrar cambios de custodia y restricciones con permisos especiales y datos mínimos. Un padre con dos hijos tiene dos evaluaciones de acceso independientes.

### 10.4 Inscripción tardía, retiro y cambios

**Ingreso a mitad del ciclo:** registrar fecha efectiva de ingreso y fecha de captura administrativa por separado. Crear CourseEnrollment desde esa fecha. Mantener períodos previos como NotEnrolled, TransferredCredit o UnderReview según procedencia, no como ceros automáticos.

**Cambio de sección:** cerrar la ubicación/membresía anterior y abrir la nueva con fecha efectiva. Mantener notas/entregas anteriores en su contexto. Si dos cursos equivalentes deben consolidarse para el promedio, usar equivalencia aprobada; no copiar scores sin procedencia.

**Cambio de grado:** requiere revisión académica, equivalencias y ubicación nueva. No reetiquetar retrospectivamente todas las notas existentes con el grado nuevo.

**Retiro:** termina membresía activa y nuevas obligaciones. Conservar expediente y acceso histórico según política; cancelar recordatorios futuros. No marcar ausente o con tarea vencida después del retiro.

**Reingreso en el mismo ciclo:** reactivar la matrícula canónica con nuevo intervalo; no crear otra persona ni destruir el tramo de retiro. Evitar solapamientos.

**Ingreso retroactivo capturado hoy:** generar un impacto previo revisable sobre asistencia, audiencia de tareas y notas; no crear faltas ni entregas vencidas masivamente sin confirmación administrativa concreta.

## 11. Notas, recuperación, boletas y promoción

### 11.1 Regla académica confirmada

Para una materia con cuatro bimestres válidos y pesos iguales:

~~~text
OrdinariaAnual = (Bimestre1 + Bimestre2 + Bimestre3 + Bimestre4) / 4
AprobadaOrdinaria = OrdinariaAnual >= 60

FinalEfectiva = recuperación aprobada/publicada si existe una resolución válida
                de reemplazo; en otro caso, OrdinariaAnual
ResultadoEfectivo = comparación con 60 conforme a la política de escala
~~~

La regla confirmada exige aprobar **cada materia**. Un 95 en Matemática no compensa un 50 en Lenguaje. El promedio general es informativo y no promueve por sí solo.

La frase «recuperación aprobada/publicada» se refiere a un intento validado como registro, no a que su nota sea necesariamente >= 60. Qué hacer si un intento fallido es inferior a la nota ordinaria se fija como política antes de producción; no deducirlo del verbo reemplazar.

### 11.2 Puntos por bimestre y flexibilidad controlada

Plantilla inicial propuesta, compatible con lo observado: Actividades 1..8 con máximo 5 cada una, Parcial 20 y Examen 40. El total es suma de puntos, máximo 100. Permitir otras plantillas por nivel/materia, con versión y vigencia; no cambiar la plantilla de un período publicado en silencio.

Si se ofrece evaluación por porcentaje y rúbrica, distinguir tipos: PointsBudget suma puntos de componentes; WeightedAverage normaliza cada actividad y aplica pesos. Un docente no puede mezclar ambos modelos sin configuración explícita.

El esquema debe definir decimales permitidos, criterio de redondeo, trato de exoneraciones, equivalencias y notas faltantes. **No está confirmado si 59.5 se aprueba por redondeo a 60.** Propuesta conservadora pendiente de aprobación: evaluar contra 60 antes de redondear para presentación, mostrando precisión suficiente para que no aparezca «60 reprobado».

### 11.3 Captura, revisión y publicación

1. Coordinación habilita curso, plantilla y período de captura.
2. El docente autorizado abre la libreta con lista derivada de matrículas vigentes y versión de datos.
3. Captura puntos o estado especial. El servidor verifica alumno, curso, máximo, esquema, permiso y vigencia.
4. El guardado de borrador informa éxito por lote y celdas inválidas; no confundir pulsar guardar con publicar.
5. La revisión identifica faltantes y excepciones. Se puede publicar parcialmente solo si una política explícita lo permite y cada resultado se rotula como parcial; nunca como cierre anual completo.
6. Publicación oficial crea PublicationBatch y resultados consistentes en una transacción, con versión del cálculo.
7. Padres/estudiantes ven solo lo publicado para ellos y habilitado por la solvencia de §13.1. Correo/push notifica después; no contiene calificaciones ni datos privados que eludan el bloqueo.
8. Cierre bloquea edición ordinaria. Rectificación usa comando específico, motivo y revisión de impacto.

El docente puede pegar una matriz desde hoja de cálculo con previsualización de alumnos/celdas. No subir un archivo y aplicar por coincidencia de nombres. Reconocer códigos estables y rechazar alumnos ajenos al curso.

La interfaz maneja tres estados claros: guardado en servidor, guardando y cambios sin guardar/error. Un indicador de borrador no puede prometer persistencia solo porque existe contenido en memoria del navegador.

### 11.4 Recuperación con reemplazo de nota final

Ejemplo de negocio confirmado:

~~~text
Materia: Lenguaje
Bimestres: 50, 55, 58, 57
Ordinaria anual: 55
Boleta versión 1: Reprobada. Debe recuperación.

Intento de recuperación: 72
Final efectiva tras validar y publicar recuperación: 72
Boleta versión 2: Aprobada en recuperación. Final 72.
La versión 1 sigue conservada; los cuatro bimestres no se modifican.
~~~

Flujo obligatorio:

1. Abrir RecoveryAttempt elegible para una materia reprobada, con alumno, ciclo, convocatoria e intento.
2. Docente autorizado captura la nota de recuperación y evidencia necesaria; puede guardarla en borrador.
3. Validar elegibilidad, rango, número de intentos y ventana; coordinación aprueba si la política lo exige.
4. Publicar resolución. Actualizar la proyección de FinalEfectiva y conservar OrdinariaAnual, intentos y procedencia.
5. Marcar obsoleta la previsualización de promoción que dependía de resultados previos.
6. Emitir nueva boleta con nueva revisión, fecha, resultado y referencia a emisión anterior.
7. Notificar al estudiante/encargado autorizado sin exponer el resultado a compañeros.

Si se capturó mal una recuperación, corregir mediante revisión auditada o anulación de ese intento según estado. No editar bimestres para ajustar el promedio ni sobrescribir el PDF anterior con la misma clave de almacenamiento.

Pendientes de política: número de oportunidades, plazo, topes, efecto de una recuperación menor, ausencia al examen y quién puede aprobar una excepción. El sistema debe poder configurarlos; una regla no confirmada no se presenta como legislación escolar.

### 11.5 Boletas reproducibles y verificables

ReportCardIssue guarda snapshot del alumno/colegio/matrícula, resultados ordinarios y efectivos, esquema/versión, redondeo aplicado, observaciones, emisor, fecha, hash y archivo generado. Una emisión oficial tiene estados PendingGeneration, Available, Failed y Superseded cuando corresponda.

Generar PDF en worker con un motor mantenido y licencia revisada; conservar plantilla y datos para trazabilidad. La descarga exige autorización. Para verificación pública usar un código aleatorio no enumerable y devolver datos mínimos; un QR no debe abrir todo el expediente a cualquiera.

El portal distingue «borrador», «boleta publicada» y «boleta revisada por recuperación». La emisión previa se puede consultar por personal autorizado; la vista familiar muestra claramente cuál es la vigente.

### 11.6 Promoción bien definida

1. **Calcular:** usar resultados efectivos publicados de todas las materias requeridas, asistencia u otros requisitos solo si fueron aprobados como política.
2. **Previsualizar:** por alumno, mostrar materias aprobadas/reprobadas, recuperaciones, faltantes, destino y razones.
3. **Resolver:** Promoted, Repeated, PendingRecovery, PendingEvidence, Graduated, Withdrawn o excepción documentada.
4. **Confirmar:** permiso promotions.confirm; volver a validar versiones de notas, ciclo y destino. Si cambió una recuperación, rechazar la previsualización vieja.
5. **Preparar siguiente ciclo:** crear propuesta de reinscripción/matrícula con el mismo StudentId. No asumir que aprobar académicamente implica que la familia confirmó la inscripción.
6. **Matricular:** secretaría confirma continuidad, documentación y cupo; crea Enrollment del nuevo año sin copiar persona/cuenta.
7. **Conciliar:** lote con resultado por alumno e idempotency key. Repetir el comando no duplica matrículas ni decisiones.

Una resolución excepcional requiere motivo y aprobador; no permitir que un checkbox ignore una materia reprobada sin dejar evidencia. La graduación termina un plan y no intenta buscar un grado inexistente. La repitencia conserva el mismo estudiante y crea matrícula de otro ciclo en el mismo grado.

### 11.7 Notas en ingreso tardío y traslados

**Caso A: ingreso en el tercer bimestre con notas oficiales del colegio anterior.** Registrar equivalencias del primero y segundo por materia, escala, origen, documento y aprobador. Las equivalencias aceptadas alimentan el cálculo anual como resultados válidos; no inventar ocho actividades antiguas con notas falsas.

**Caso B: ingreso sin notas previas.** Los períodos previos quedan UnderReview/NotEnrolled. El portal muestra por qué no hay promedio anual definitivo. Coordinación define un plan de nivelación o una excepción autorizada. No dividir automáticamente entre dos bimestres y presentar el resultado como si se hubieran cursado cuatro.

**Caso C: ingreso durante un bimestre.** Determinar qué actividades son exigibles desde ingreso y qué nivelación se requiere. La libreta muestra NotEnrolled para actividades anteriores que no correspondan. La política de escala determina cómo producir un total comparable; reponderar componentes requiere regla aprobada, no una exclusión silenciosa que aumente notas.

**Caso D: cambio de sección con cursos equivalentes.** Conservar scores de origen, publicar equivalencia o transferencia y computar una sola vez cada aprendizaje. Bloquear doble conteo por tener dos CourseEnrollment históricos.

Para el primer lanzamiento, **bloquear el cierre definitivo cuando falte resolver evidencia académica obligatoria**. La captura de períodos actuales debe continuar mientras se tramitan equivalencias anteriores.

## 12. Tareas, entregas y archivos

### 12.1 Creación y publicación profesional

1. El docente elige un curso donde tiene asignación vigente. El curso determina ciclo, grupo, materia y audiencia elegible.
2. Crea borrador con instrucciones, adjuntos, modalidad, puntos/rúbrica si aplica, fecha de disponibilidad y vencimiento con hora y zona.
3. Escoge audiencia: todo el curso, varios grupos autorizados, subgrupo o estudiantes concretos. «Todos» significa todos los destinatarios elegibles de ese curso; nunca todos los alumnos del colegio por defecto.
4. El sistema muestra cantidad de destinatarios, exclusiones, conflictos de fecha y efecto en libreta antes de publicar.
5. Publicar crea versión, audiencia y AssignmentRecipient de forma consistente o mediante un proceso de publicación con estado visible que no anuncia éxito antes de terminar.
6. Al quedar Published, la tarea aparece en agenda, lista y curso de cada destinatario. Esa aparición depende de datos del servidor, no de recibir push.
7. Se encolan notificaciones con deduplicación por tarea/versión/destinatario/canal. Los encargados reciben resumen solo si su facultad y preferencia lo permiten.

Si se publica para varios cursos, cada destino debe tener contexto claro de membresía y calificación. Una tarea compartida puede tener instrucciones comunes y obligaciones por curso; no reutilizar una entrega entre materias sin regla explícita.

### 12.2 Audiencia, matrículas nuevas y cambios de grupo

Usar una política de audiencia **híbrida**: definición de destinos + destinatarios materializados auditables. La definición permite evaluar nuevos integrantes; los destinatarios registran para quién fue exigible y con qué plazo. No resolver todo mediante una consulta dinámica que cambie el pasado cada vez que alguien se mueve.

| Situación | Comportamiento predeterminado propuesto |
|---|---|
| Alumno activo al publicarse | Crear obligación de entrega y mostrar la tarea al estar disponible. |
| Alumno inscrito después de publicación pero antes de vencimiento | Añadir como destinatario al activar su membresía, evaluar tiempo disponible y aplicar plazo individual si es necesario. |
| Alumno inscrito después de vencimiento | No crear entrega vencida ni cero automático. Puede consultar material previo autorizado como referencia, sin obligación. |
| Nivelación de tarea anterior | Docente/coordinación asigna explícitamente, con motivo y vencimiento individual nuevo. |
| Tarea para subgrupo cerrado o alumno específico | No añadir nuevos alumnos por el solo hecho de entrar al curso. |
| Alumno cambia de sección | Resolver tareas abiertas de origen/destino mediante política de traslado; conservar entregas previas y evitar doble obligación equivalente. |
| Alumno se retira | No crear tareas nuevas; detener recordatorios futuros y resolver pendientes con el estado de retiro. |
| Se agrega destinatario después de publicar | Registrar revisión de audiencia y notificar solo a quienes corresponda. |
| Se quita destinatario con entrega existente | Conservar evidencia y razón; no borrar la entrega ni exponerla a nuevos grupos. |

El plazo individual debe permitir un tiempo razonable mínimo configurable desde la asignación efectiva. No suponer que un alumno recién inscrito puede entregar en diez minutos porque la tarea global vence hoy.

EnrollmentActivated y CourseMembershipChanged disparan reconciliación idempotente de obligaciones. La autorización siempre consulta la membresía/facultad real; una proyección retrasada no concede acceso indebidamente. La pantalla administrativa debe mostrar asignación pendiente y los workers tienen un objetivo de demora y alerta. Si falla la notificación, la lista de tareas publicada debe seguir disponible.

### 12.3 Estados de tarea, entrega y calificación

~~~text
Tarea: Draft -> Scheduled -> Published -> Closed -> Archived
                    -> Cancelled mediante razón y revisión

Entrega: Draft -> Submitted -> Returned -> Resubmitted
                        -> Accepted / Graded

Obligación: Assigned / Submitted / Late / Missing / Excused / NotRequired
Calificación: Pending / DraftScore / PublishedScore / Corrected
~~~

Separar entrega y calificación evita que subir un archivo publique automáticamente una nota. El alumno puede ver que entregó y esperar revisión. El docente puede devolver para corregir con plazo individual.

### 12.4 Flujo de entrega

1. El estudiante abre una tarea donde es destinatario; el servidor valida identidad, matrícula, disponibilidad y plazo individual.
2. Prepara texto/archivos en borrador. Mostrar límites y estado de carga; guardar borrador en servidor cuando sea requisito del producto.
3. El archivo se recibe en cuarentena y se valida; hasta quedar aceptado no aparece como archivo seguro para el docente.
4. El alumno confirma «Entregar». El servidor registra instante, intento, archivos, versión de instrucciones y recibo de entrega.
5. Un doble clic o reintento con la misma clave devuelve la entrega existente.
6. La respuesta presenta comprobante, fecha local, estado puntual/tardío y contenido recibido; también accesible desde la app.
7. Reentregar crea otro intento. El docente ve el historial y cuál es vigente; el alumno no pierde prueba de la entrega original.

Regla temporal propuesta: el servidor decide puntualidad con el instante de envío confirmado; si el escaneo fue asíncrono y tarda, conservar el instante de recepción válida de bytes para no castigar una demora del sistema. Si el archivo resulta rechazado, mostrarlo y aplicar el procedimiento de subsanación; no fingir una entrega aceptada.

El estudiante sin conexión puede preparar un borrador local, pero **no aparece entregado hasta recibir confirmación del servidor**. No permitir que la hora manipulable del dispositivo reemplace la fecha de recepción.

El encargado consulta estado y retroalimentación autorizada. Entregar en nombre del hijo solo si la institución habilita esa facultad, con SubmittedByUserId y OnBehalfOfStudentId visibles para auditoría; nunca suplantando al estudiante sin registro.

### 12.5 Calificación y enlace a libreta

El docente revisa únicamente entregas de sus cursos autorizados, usa rúbrica/comentarios y guarda nota en borrador. Al publicar, Assessment valida el ítem destino y escribe la nota con SourceType = Assignment y referencia a la revisión de entrega.

La vinculación es explícita y única: una tarea no se suma dos veces porque el docente escribió además una calificación manual. Si un ítem agrupa varias tareas, la política de agregación define cómo se calculan sus puntos.

Cambiar la nota desde la libreta después de una tarea calificada genera revisión y evidencia del override. No dejar dos notas distintas sin indicar cuál es la oficial. La regla y proyección oficial vive en Assessment.

Un archivo no entregado se vuelve Missing después del vencimiento y la tolerancia aprobada; convertir Missing a 0 requiere política explícita y oportunidad de justificar, según la práctica del colegio. No asignar 0 a alumno que ingresó después de vencida la tarea.

### 12.6 Archivos seguros

Metadatos en SQL Server; contenido en almacenamiento privado. Separar imágenes públicas del colegio, expedientes privados, entregas, adjuntos y boletas emitidas por categoría, política de acceso y retención.

Flujo técnico: solicitud autorizada de carga -> clave aleatoria de objeto -> carga limitada -> confirmación con tamaño/checksum -> detección de MIME y firma -> escaneo -> estado Clean/Rejected -> asociación al recurso. Validar otra vez la autorización al completar, porque pudo cambiar durante la carga.

Valores iniciales propuestos, ajustables tras probar conectividad: 25 MB por archivo, 100 MB por entrega, 5 archivos y tipos permitidos por actividad. Mostrarlos en la UI. Permitir reanudación para archivos grandes; cuotas por alumno/curso/colegio y limpieza de cargas abandonadas.

No aceptar un MIME declarado por el navegador como prueba suficiente. Limitar archivos comprimidos, evitar ejecución/interpretación de contenido activo y generar vistas previas aisladas. Nombres originales solo para presentación y descarga segura; la clave del objeto no se deriva de rutas ingresadas por el usuario.

Descargar mediante endpoint autorizado o URL firmada de vigencia corta. Una URL firmada es transferible hasta que vence; para revocación estricta y archivos muy sensibles, usar descarga mediada y validar en cada petición. No prometer revocación instantánea de un enlace ya emitido ni de una copia ya descargada.

Estas decisiones siguen controles generales de almacenamiento, validación, límites y permisos recomendados por OWASP. [Guía de carga de archivos](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html).

## 13. Especificación funcional del resto de módulos

### 13.1 Finanzas y solvencia: parte obligatoria del portal

**CONFIRMADO:** no mostrar notas a estudiantes/encargados si no han cubierto las colegiaturas obligatorias hasta el corte correspondiente. Marzo fue un ejemplo del propietario, no un mes fijo ni una definición rígida del calendario.

#### A. Conceptos que no deben mezclarse

| Concepto | Ejemplo | Función |
|---|---|---|
| Tipo/categoría de cobro | Colegiatura, inscripción, transporte, material | Clasificar y establecer reglas; no equivale a dinero recibido |
| Plan de cobro | Básico, jornada matutina, ciclo 2027 | Definir obligaciones y calendario aplicable |
| Asignación de plan | Plan acordado para una matrícula concreta | Establecer qué debe pagar ese alumno y desde cuándo |
| Período de cobro | Cuota 03 de diez cuotas o mes de marzo | Identificar la obligación concreta sin depender de texto libre |
| Cargo/cuenta por cobrar | Colegiatura del período 03: Q500 | Obligación emitida con líneas, fecha, vencimiento y saldo |
| Pago | Transferencia validada de Q1,000 | Ingreso real registrado |
| Aplicación de pago | Q500 a cuota 02 y Q500 a cuota 03 | Indicar exactamente qué obligaciones quedaron cubiertas |
| Comprobante de pago recibido | Imagen de depósito pendiente de revisar | Evidencia presentada, que todavía no equivale a pago validado |
| Recibo | Documento que acredita pago confirmado | Numeración e instantánea histórica |
| Factura fiscal | Documento emitido según integración fiscal aprobada | Proceso distinto del cargo interno; no simular FEL |
| Beca/descuento/exoneración | 50% de colegiatura o cuota exonerada | Ajuste autorizado de obligación, con vigencia y motivo |
| Convenio | Calendario aprobado de regularización | Condiciones especiales y facultad explícita sobre solvencia |

La interfaz puede llamar al documento «cargo», «boleta de cobro» o «cuenta por cobrar». Reservar «factura fiscal» para el flujo realmente implementado y validado. Tener un PDF con ese título no acredita cumplimiento fiscal.

#### B. Planes configurables

Modelo: FeeConcept, BillingPlanVersion, BillingPlanAssignment, BillingSchedule, BillingPeriod, Invoice/Charge, InvoiceLine, Adjustment, Payment, PaymentAllocation, Receipt, Reversal, PaymentEvidence, PaymentAgreement y FinancialClearancePolicyVersion.

Cada plan define:

- Colegio, ciclo, sede/nivel/grado/jornada a los que puede asignarse, moneda y vigencia.
- Inscripción y otros cobros únicos, separados de colegiatura recurrente.
- Cuotas mensuales o calendario de cuotas personalizadas; inicio/fin, importe, emisión y vencimiento.
- Conceptos obligatorios, opcionales y los que cuentan para solvencia académica.
- Becas, descuentos por pronto pago/hermanos, exoneraciones y sus condiciones explícitas. Evitar acumulación no prevista de beneficios.
- Ingreso tardío, retiro, prorrateo o cobro completo según contrato/política del colegio; ningún prorrateo automático no acordado.
- Estado de cuotas futuras y criterio para adelantos.
- Versión y aprobación del plan; una nueva versión no reescribe cargos ya emitidos.

Ejemplos **propuestos**, sin fijar importes reales:

| Plan | Configuración posible | Uso |
|---|---|---|
| Mensual escolar | Inscripción + diez colegiaturas calendarizadas | Operación ordinaria del colegio |
| Cuotas personalizadas | Inscripción + ocho cuotas con fechas acordadas | Calendarios distintos por jornada/plan |
| Pago anual anticipado | Obligaciones del año con descuento aprobado al cubrirlas | El pago se aplica a las cuotas; no desaparece la trazabilidad mensual |
| Beca parcial/total | Calendario base con ajustes autorizados | Solvencia sobre importes netos; una cuota exonerada puede estar cubierta sin pago |
| Ingreso tardío | Asignación vigente desde ingreso y cuotas aplicables | Evitar exigir meses no contratados |
| Convenio de regularización | Acuerdo con cuotas y condiciones propias | Requiere regla explícita para acceso a notas |

Un convenio no declara solvente a alguien por existir como documento: debe decir si permite consultar notas mientras se cumple, cómo se controla cumplimiento y quién lo aprobó. Predeterminado propuesto: la deuda sigue bloqueando salvo una excepción de acceso expresamente autorizada y vigente.

#### C. Corte de solvencia por publicación de notas

Cada PublicationBatch de notas referencia un **FinancialReleaseRequirement** con ciclo, plan/política y conjunto de períodos de colegiatura que deben estar cubiertos. Puede configurarse «hasta cuota N», «hasta mes elegido» o una selección de obligaciones aprobada. El colegio escoge el corte por bimestre; no se codifica marzo ni una relación universal bimestre = dos meses.

Ejemplo ilustrativo configurable:

| Publicación | Corte que podría elegir el colegio | Efecto |
|---|---|---|
| Primer bimestre | Primeras 3 cuotas | Exigir todas las colegiaturas aplicables de ese conjunto |
| Segundo bimestre | Primeras 5 cuotas | Exigir las anteriores y las nuevas hasta ese corte |
| Tercer bimestre | Primeras 8 cuotas | Igual, según el calendario real |
| Cierre anual | Todas las cuotas contratadas del ciclo | Habilitar la boleta anual al cubrirlas |

Esta tabla no afirma que los bimestres o pagos reales del colegio tengan esas fechas. La UI debe permitir cambiar la selección y mostrar su efecto antes de publicar.

Fórmula de acceso propuesta:

~~~text
ObligacionesRequeridas = colegiaturas obligatorias de la matrícula,
                        aplicables por plan/vigencia,
                        incluidas en el corte de la publicación

SaldoCargo = ImporteEmitido + AjustesDeCargo - Descuentos/Becas/Exoneraciones
             - AplicacionesDePagosConfirmados + ReversionesAplicables

SolventeParaPublicacion = plan y calendario completos/validados
                         y todas las obligaciones requeridas cubiertas
                         o excepción de acceso aprobada y vigente

PuedeConsultarNota = identidad y relación autorizadas
                    y resultado publicado
                    y SolventeParaPublicacion
~~~

No contar «cantidad de pagos» ni «algún pago hecho». Un pago puede cubrir varios meses; varios abonos pueden cubrir uno; un adelanto de otro período no cubre una cuota que quedó pendiente si no se aplicó a ella. Una captura pendiente de verificación no desbloquea notas.

Tampoco basta buscar cargos vencidos: el corte puede exigir una cuota que aún no ha vencido. La decisión se toma contra los períodos requeridos de la publicación y las obligaciones contratadas.

Si no hay cargos porque falló la generación o la matrícula no tiene plan asignado, devolver **PendingFinancialSetup**, no «solvente» por saldo vacío. Una exoneración total formal sí puede habilitar con importe cero. Validar cobertura del calendario, no solo las filas de factura que casualmente existan.

Reglas adicionales:

- Solvencia por estudiante y matrícula/ciclo. Una deuda de un hermano no bloquea al otro sin una política familiar adicional explícita; no se propone esa política como predeterminada.
- Para ingreso tardío solo cuentan obligaciones realmente aplicables. Un alumno que entra en agosto no debe enero por una regla de software; depende del plan acordado.
- Primera boleta ya emitida conserva su corte. Nuevas publicaciones pueden tener otro. Si se desea bloquear también el historial por deuda posterior, configurar esa política expresamente; predeterminado propuesto: evaluar el corte asociado a cada publicación.
- Boleta consolidada exige el conjunto de períodos requeridos para todos los resultados que incluye. No permitir descargar un PDF consolidado para eludir el corte de un bimestre.
- Reemisión por recuperación conserva el requisito de solvencia del resultado/ciclo, salvo modificación expresa aprobada. Imprimir en otro mes no inventa automáticamente un mes adicional de deuda.
- Una reversión o anulación de pago vuelve a afectar elegibilidad en la siguiente consulta si deja obligaciones descubiertas.
- La obligación de emitir/entregar documentos especiales y cualquier límite jurídico al bloqueo se revisan antes de activar esa política en producción; no extender por software el bloqueo a todo documento oficial indiscriminadamente.

#### D. Control real del acceso y pantalla de bloqueo

Aplicar la regla en resultados, detalle de actividad **cuando revele una calificación**, boletín, PDF, historial, API móvil, paneles, promedios, exportaciones, enlaces compartidos y avisos. Decidir por política que la publicación de puntajes de tareas pertenece a GradesView y queda cubierta por el mismo control. Las instrucciones, entregas y mensajes sin calificación siguen disponibles para que el alumno pueda continuar estudiando.

El docente y personal académico con permiso interno siguen capturando/revisando notas; el bloqueo es para la consulta familiar/estudiantil. No filtrar deuda del alumno a otros alumnos ni bloquear tareas porque exista saldo pendiente.

Antes de devolver resultados, Assessment consulta síncronamente el contrato Finance.CheckClearance para el alumno/publicación. La decisión usa pagos/aplicaciones confirmados en SQL. Una proyección de saldo o caché atrasada no puede mantener abierto el acceso tras una reversión ni mantenerlo bloqueado después de pago.

No basta con ocultar la pestaña. La respuesta bloqueada no contiene notas, promedios, HTML oculto, adjuntos ni metadatos que permitan reconstruirlos. Para PDFs académicos con requisito estricto, usar descarga mediada; volver a comprobar solvencia al crear el trabajo y al descargarlo. No repartir URLs firmadas de larga duración que sigan funcionando después de una reversión.

Pantalla propuesta:

~~~text
Tus calificaciones están publicadas
Para consultarlas necesitas completar las colegiaturas requeridas
para esta entrega de notas.

Pendiente: [períodos y saldo, solo si la cuenta puede ver finanzas]
Estado de un comprobante: [En revisión, cuando corresponda]

[Ver estado de cuenta] [Registrar comprobante] [Contactar a caja]
Tu acceso a tareas y entregas continúa disponible.
~~~

Si el estudiante no tiene permiso de finanzas, mostrar «Consulta con tu encargado o con caja» sin detalles de convenios, bancos o información de otro adulto. Las notificaciones de notas anuncian disponibilidad condicionada y llevan al portal; no adjuntan boletas ni incluyen la nota en el push/correo cuando eso eludiría la solvencia.

Una boleta que ya fue descargada no puede retirarse del dispositivo. El bloqueo gobierna nuevas consultas y descargas; documentar esa limitación física con precisión.

#### E. Generación masiva de cobros: flujo de producto

1. **Elegir operación:** generar colegiaturas de un período, inscripción, cobro extraordinario o asignar un plan completo.
2. **Elegir destinatarios:** ciclo + sede + nivel + grados + jornadas + secciones opcionales o lista validada de matrículas. Permitir excluir alumnos con razón.
3. **Elegir concepto/plan:** concepto obligatorio/opcional, período, importe o regla, moneda, emisión y vencimiento.
4. **Previsualizar en servidor:** matrícula efectiva, plan aplicable, beneficio, neto, cargo equivalente existente y motivo de exclusión. Mostrar número de alumnos y totales por moneda.
5. **Resolver incidencias:** sin plan, matrícula retirada, duplicado, beca pendiente, fechas incompatibles o descuento sin permiso.
6. **Confirmar lote:** permisos finance.charges.generate y aprobación cuando exceda el umbral configurado. La confirmación se refiere a una versión de previsualización.
7. **Procesar:** para volumen pequeño puede ser transaccional; para lotes grandes, job con páginas, reserva y estados por alumno. Mostrar progreso real y fallos recuperables.
8. **Conciliar:** generados, ya existentes, omitidos, fallidos; totales y enlace a cada cargo. Reintentar solo fallidos; no duplicar exitosos.
9. **Notificar:** resumen de cobro por canal autorizado, sin exigir que el proveedor funcione para que el cargo exista.

Diseño de interfaz: asistente «Destinatarios -> Plan y período -> Revisión -> Resultado». Tabla con búsqueda, filtros, beneficios visibles y selección por alcance. Para «todos los alumnos filtrados» guardar selección del servidor y excepciones; no asumir que seleccionar los 25 de la página equivale a seleccionar los 400 del grado.

Un descuento no se guarda solamente como cambio del monto final: mantener regla aplicada, beneficio, aprobador y base de cálculo. Cambiar la planilla después de previsualizar invalida el preview si afecta el resultado.

**Unicidad de negocio:** una ObligationKey estable, formada por colegio + matrícula + concepto + período/cuota + ocurrencia, identifica el cargo automático. Mantener referencia a la asignación/versión de plan para explicar el cálculo, sin usar su cambio como permiso para repetir la obligación. No usar BatchId como única protección: dos lotes distintos no pueden cobrar dos veces la misma colegiatura. Cambiar de plan o de versión durante el mismo período requiere un ajuste explícito y tampoco crea por sí solo otra obligación de ese período.

Corregir un lote emitido mediante ajustes/anulaciones autorizadas por cargo y reporte de impacto. No borrar masivamente cargos pagados. Permitir una modificación de borrador antes de confirmar; después, conservar trazabilidad.

#### F. Ingreso tardío y generación recurrente

Al activar matrícula, el sistema exige asignación de plan o una exoneración autorizada. Previsualiza obligaciones aplicables desde la fecha acordada. Si el lote del grado ya se ejecutó, genera solo los cargos faltantes del alumno con la misma clave natural; no obliga a regenerar el grado completo.

Una tarea programada puede emitir la siguiente cuota del calendario. Antes de ejecutarse vuelve a validar retiro, plan, becas y cargo existente. La programación y el período son explícitos, sin derivarlos de «mes actual» después de un reintento tardío.

#### G. Registro de pagos, evidencia y recibos

1. Caja busca al estudiante o grupo familiar autorizado y ve cargos/meses pendientes por alumno.
2. Registra monto recibido, moneda, medio real, fecha efectiva, referencia, banco/caja y evidencia si aplica.
3. Previsualiza la distribución: por defecto, colegiaturas obligatorias más antiguas del alumno; el usuario puede modificarla si tiene permiso y ve el efecto sobre solvencia.
4. Confirma con idempotency key. Dentro de transacción se validan saldos con control de concurrencia, se guarda pago confirmado y sus aplicaciones, se asigna recibo y se registra auditoría/outbox.
5. Se devuelve un recibo y la situación actualizada: «Cubre cuotas 01–03; habilita publicación B1». No prometer disponibilidad si falta validar el pago.
6. Exceso se conserva como anticipo/saldo a favor con reglas explícitas; no aplicarlo a un hermano sin selección autorizada y distribución visible.

Los pagos pueden distribuirse entre varias facturas e incluso varios hijos si la caja lo autoriza; cada aplicación identifica alumno y obligación. No compartir estados globales «padre solvente» que oculten diferencias entre hijos.

Un comprobante presentado en portal entra PendingReview -> Accepted/Rejected. Aceptarlo crea o vincula el pago real, con prevención de duplicados por proveedor/referencia cuando corresponda. Rechazo explica el motivo y admite subsanación. El alumno no se vuelve solvente por subir la misma imagen repetidas veces.

Pasarelas futuras: webhook firmado, validación de importe/moneda/referencia del servidor, deduplicación y conciliación. Una redirección «pago exitoso» del navegador no confirma el pago. No declarar activos métodos cuyos proveedores no estén contratados/integrados.

Reversión: permiso separado, motivo y aprobador si procede; conservar pago/recibo original, generar movimiento inverso y recalcular aplicaciones/solvencia. Importe y estados usan decimal. Un cierre de caja y conciliación por medio de pago deben cuadrar con transacciones confirmadas y reversiones.

### 13.2 Admisiones, inscripción y expediente

Flujo: solicitud -> revisión documental -> aceptación/rechazo -> persona/estudiante -> matrícula borrador -> validación de cupo/plan -> activación. Permitir crear expediente y vincular padres antes de tener cuenta de portal. La activación académica y la activación de login son estados distintos.

Campos obligatorios según propósito: no exigir correo o padre a un adulto independiente. Para menores con encargado pendiente, mostrar incidencia administrativa con responsable; no inventar una relación. La política decide si eso bloquea activación de matrícula o solo portal familiar.

Cupos por grupo/jornada/sede con validación transaccional. Dos inscripciones concurrentes no pueden consumir la misma última plaza. Retiros, traslados y reincorporaciones requieren motivo, vigencia y reporte de impacto.

Expediente: historial de identidad, documentos y salud/emergencia solo con permiso específico. Avisos de documentos faltantes visibles a quien deba resolverlos, sin publicar datos sensibles a todos los docentes.

### 13.3 Organización académica y horarios

Configurar ciclo, períodos, niveles, grados/carreras, jornadas, secciones opcionales, plan versionado y materias. Crear CourseOffering para cada materia impartida y TeachingAssignment para sus docentes. Sustitutos/codocentes tienen vigencia y capacidades explícitas.

Horarios detectan superposición de intervalos de docente, aula y grupo, incluso si las horas de inicio/fin no coinciden exactamente. Considerar feriados, calendario de días hábiles y excepciones. Cambiar docente no duplica tareas ni notas; el nuevo docente autorizado hereda acceso al curso según su asignación, conservando autoría anterior.

Copiar configuración al siguiente ciclo crea borradores de planes/grupos, sin copiar matrículas, calificaciones ni credenciales de personas. Revisar cambios antes de activar.

### 13.4 Asistencia

Registrar sesiones por fecha real y ámbito: jornada o clase impartida. Cada AttendanceRecord se liga a matrícula vigente. Estados configurables como presente, ausente, tardío y justificado; conservar quién y cuándo corrigió.

No marcar ausente a quien no estaba inscrito ese día, se retiró o estaba en otra sección. «Marcar todos presentes» trabaja sobre la lista efectiva de la sesión y no sobre todos los alumnos actuales del grado. Un feriado no genera faltas automáticas.

Justificación por familia con documento privado y resolución del colegio. Alertas deduplicadas y respetuosas. Si se permite captura temporal sin conexión, sincronizar con versión y resolver conflictos; confirmar al docente qué filas quedaron registradas.

### 13.5 Comunicación, avisos y calendario

Comunicaciones iniciales: avisos institucionales, mensajes docente-encargado/estudiante y canales de curso moderados. La política decide quién puede iniciar contacto; no permitir chat libre de cualquier adulto con cualquier menor.

ConversationParticipant define acceso con vigencia. Autenticar cada conexión SignalR y verificar membresía también en cada acción; un nombre de grupo no concede permiso. Retiro o cambio de curso invalida autorizaciones y futuras entregas de mensajes según política.

Leer un aviso, entregar un mensaje y confirmar que una familia comprendió el contenido son hechos distintos. Guardar lectura/acuse cuando se requiera; no inferir lectura del correo por un pixel.

Notificaciones: bandeja interna persistente primero, correo/push después. Preferencias por categoría, horario razonable, traducción y deduplicación. SMS/WhatsApp como adaptadores opcionales con proveedor y reglas aprobadas. Un push no incluye nota bloqueada por insolvencia.

Calendario unificado por usuario con cursos, vencimientos individuales, eventos y sesiones relevantes. Cambiar fecha de una tarea actualiza el evento existente y comunica el cambio; no generar copias indefinidas.

### 13.6 Reportes y documentos

Paneles por función: coordinación ve captura pendiente, alumnos sin equivalencias y recuperaciones; caja ve cartera y conciliación; familia ve tareas, avisos y acceso condicionado a notas. No todos necesitan las mismas gráficas.

Reportes grandes se solicitan como trabajos con filtros, identidad solicitante y ámbito fijados; al descargar se revalida permiso. Exportaciones con acceso familiar aplican solvencia por cada alumno/publicación, no se autorizan solo por tener reportes.read.

Documentos emitidos conservan snapshot y versión. Antes de incluir una constancia o documento oficial en el bloqueo financiero, definir su política específica; las notas y boletas familiares sí siguen DEC-013.

### 13.7 Biblioteca, transporte, inventario y RR. HH.

| Extensión | Diseño mínimo correcto | Prioridad |
|---|---|---|
| Biblioteca | Título y ejemplar separados, préstamos, reservas, devoluciones, estado y multas aprobadas | Posterior al núcleo |
| Transporte | Rutas/paradas, vehículo, piloto, cupos, vigencia y autorización de retiro | Según operación confirmada |
| Inventario | Productos/activos, movimientos, almacenes, responsable y conciliación; no editar stock sin movimiento | Posterior |
| Gastos | Solicitud/aprobación, pago, documento y centro de costo | Después de estabilizar caja |
| RR. HH. | Expediente, función, asistencia y accesos separados | Posterior; nómina requiere alcance especializado |
| Residencia | Habitaciones/camas, capacidad y asignación con vigencia | Desactivada mientras no haya necesidad real |

Estas extensiones reutilizan personas; no crean otras tablas de contraseñas ni convierten sus banderas en el estado oficial del estudiante. No construirlas para igualar un menú antiguo que no se utiliza.

## 14. API compartida por web y app

### 14.1 Convenciones

REST JSON bajo /api/v1, OpenAPI generado y clientes tipados. Los endpoints invocan los mismos casos de uso que los trabajos del servidor. DTO explícitos; nunca serializar directamente entidades EF o datos de Identity.

- Colegio validado por membresía; no aceptar SchoolId enviado sin comprobarlo.
- Ciclo/recurso explícito; no usar «año de sesión» como parámetro oculto.
- Paginación con límites, filtros permitidos y orden estable; cursor para mensajes/auditoría.
- ProblemDetails con code y traceId; sin stack trace ni nombres internos de tablas.
- ETag/If-Match o versión explícita para ediciones y operaciones masivas.
- Idempotency-Key para pagos, publicaciones, entregas, promoción y lotes de cobro/importación.
- Timestamps ISO 8601; fechas calendario yyyy-MM-dd; importes decimales y moneda con convención consistente.
- Respuestas sensibles con Cache-Control privado/no-store según recurso. No guardar notas en service worker compartido.
- API devuelve 401/403; no HTML de login redirigido a una app móvil.

### 14.2 Recursos iniciales

| Método y recurso | Caso de uso / permiso |
|---|---|
| GET /me | Identidad, membresías y capacidades; sin lista global de personas |
| GET /me/students | Yo como estudiante y/o hijos con facultades verificadas |
| POST /invitations/consume | Activación de invitación de un uso |
| GET /students/{id}/timeline | Ciclos y eventos visibles del expediente |
| POST /students/{id}/guardians | Solicitud/aprobación de vínculo autorizado |
| POST /years/{yearId}/enrollments | Matrícula con fecha efectiva y plan requerido |
| POST /enrollments/{id}/placements | Cambio de ubicación con validación de impacto |
| GET /courses/{id}/roster?at=fecha | Lista efectiva de curso para actor autorizado |
| POST /courses/{id}/assignments | Borrador de tarea |
| POST /assignments/{id}/publish | Publicación con audiencia y versión |
| GET /me/assignments | Obligaciones de ese alumno y plazos individuales |
| POST /assignments/{id}/submissions | Entrega confirmada/idempotente |
| POST /files/upload-intents | Reserva de carga autorizada |
| POST /files/{id}/complete | Confirmación y cuarentena/escaneo |
| GET /files/{id}/content | Descarga con autorización del recurso; solvencia si contiene notas |
| PATCH /gradebooks/{id}/scores | Captura de borrador con concurrencia |
| POST /gradebooks/{id}/publications | Publicación académica y requisito financiero |
| GET /students/{id}/publications/{publicationId}/results | Resultado familiar con control de solvencia |
| POST /annual-results/{id}/recoveries | Intento de recuperación |
| POST /recoveries/{id}/publish | Reemplazo de resultado efectivo validado |
| POST /report-card-issues | Solicitud de emisión con snapshot |
| GET /report-card-issues/{id}/download | Autorización y solvencia actual para esa publicación |
| POST /promotions/preview | Simulación con versiones de origen |
| POST /promotions/{id}/confirm | Resolución y propuesta de siguiente matrícula |
| POST /billing-batches/preview | Destinatarios y cargos calculados sin emitir |
| POST /billing-batches/{id}/confirm | Emisión masiva revisada e idempotente |
| GET /students/{id}/statement | Obligaciones, aplicaciones y saldo autorizado |
| POST /payments | Ingreso validado y aplicaciones |
| POST /payment-evidence | Comprobante pendiente de revisión |
| POST /payments/{id}/reverse | Reversión autorizada |
| GET /students/{id}/clearance?publicationId=... | Estado y razones permitidas de solvencia |
| GET /jobs/{id} | Progreso/resultado del trabajo del solicitante autorizado |

No todos los endpoints de personal son accesibles a Student/Guardian. Exponer una operación en OpenAPI no concede el permiso. Usar políticas por caso de uso y por recurso.

### 14.3 Ejemplos de contratos

Ejemplo de respuesta bloqueada, sin calificaciones:

~~~json
{
  "type": "https://school.example/problems/financial-clearance-required",
  "title": "Debes completar las colegiaturas requeridas",
  "status": 403,
  "code": "GRADES_FINANCIAL_CLEARANCE_REQUIRED",
  "publicationId": "identificador-opaco",
  "clearanceStatus": "PendingTuition",
  "allowedActions": ["ViewStatement", "SubmitPaymentEvidence"],
  "traceId": "identificador-de-correlacion"
}
~~~

El dominio anterior es ilustrativo. Usar un dominio real del producto al implementar. allowedActions depende de permisos; no devolver datos financieros a quien no está autorizado.

Ejemplo conceptual de publicación de tarea:

~~~json
{
  "expectedVersion": "version-del-borrador",
  "audience": { "kind": "EntireCourse", "courseOfferingId": "curso" },
  "availableAt": "2027-02-01T13:00:00Z",
  "dueAt": "2027-02-08T00:00:00Z",
  "lateEnrollmentPolicy": "OpenAssignmentsWithIndividualDeadline",
  "lateSubmissionPolicy": "AllowAndFlag"
}
~~~

El servidor obtiene docente, colegio y derechos desde el actor autenticado. No confía en una lista de alumnos del navegador para ampliar audiencia fuera del curso.

### 14.4 Idempotencia y concurrencia reales

Asociar cada clave a actor/colegio/operación y hash del cuerpo. Misma clave y mismo contenido devuelve resultado previo; misma clave con otro contenido devuelve conflicto. Persistir resultado y efecto crítico en una transacción o máquina de estados recuperable. Una caché de cinco minutos en Redis no protege una emisión financiera durable.

Las respuestas masivas definen si son atómicas o por fila. No devolver 200 «todo bien» si solo se guardaron 18 de 25 notas. Indicar Accepted/Failed por alumno y versión; pedir al docente resolver los fallos antes de publicar.

Generar contratos web/móvil en CI y detectar cambios incompatibles. La app puede tardar en actualizarse: mantener ventana de compatibilidad declarada, migrar clientes antes de retirar API y poder exigir actualización por seguridad de forma controlada.

## 15. Experiencia y diseño de producto

### 15.1 Navegación y sistema visual

Crear un sistema propio con tokens de color, tipografía, espaciado, estados, elevación y densidad. Tema claro como base y oscuro si se mantiene con el mismo rigor. Estética limpia, jerarquía consistente, tablas legibles y mensajes breves. Una plataforma escolar debe ser fácil de usar en celular y conexión modesta.

Con Angular, utilizar componentes mantenidos y accesibles, por ejemplo Material/CDK, personalizados con tokens. Verificar licencia y compatibilidad antes de sumar una grilla comercial. No confundir animación o abundancia de tarjetas con claridad.

Cabecera: rol/vista actual, colegio/sede, ciclo de contexto cuando aplique, búsqueda autorizada, notificaciones y cuenta. Cambiar de perfil estudiante/docente no cambia identidad ni concede permisos extra; solo organiza la experiencia.

### 15.2 Pantallas por persona usuaria

| Persona | Inicio y acciones prioritarias |
|---|---|
| Encargado | Tarjetas de hijos, tareas pendientes, avisos, estado de cuenta autorizado y acceso a notas según solvencia |
| Estudiante | Mi día, próximas tareas, entregas, comentarios y notas publicadas/habilitadas |
| Docente | Cursos de hoy, entregas por revisar, libreta, asistencia y anuncios |
| Secretaría | Admisiones, matrícula, vínculos pendientes, faltantes y cambios de ubicación |
| Coordinación | Avance de captura/publicación, equivalencias, recuperaciones y promoción |
| Caja | Buscar alumno, cobrar/aplicar pago, generar lote, revisar comprobantes y conciliar |
| Dirección | Indicadores útiles y excepciones que requieren decisión |

La familia debe seleccionar al hijo antes de una acción específica y verlo siempre en encabezado. No guardar silenciosamente «último hijo» para operaciones sensibles sin mostrarlo. Si una persona tiene rol propio de estudiante y es encargado de otro, ambas vistas deben estar diferenciadas.

### 15.3 Flujos que merecen diseño explícito

- **Matrícula:** resumen de identidad -> ubicación efectiva -> plan financiero -> documentos -> validación -> activación con resultado por subsistema.
- **Tarea:** borrador -> audiencia y plazos -> previsualización -> publicación -> seguimiento; vista de alumno con un botón Entregar inequívoco.
- **Notas:** grilla rápida con estado de guardado -> validaciones -> revisión -> publicación; sin botón «Guardar» que también cierre bimestre.
- **Cobros masivos:** asistente con alcance y sumas -> incidencias -> confirmación -> progreso/reintento -> conciliación.
- **Pago:** búsqueda -> selección de obligaciones -> monto/medio -> distribución -> confirmación -> recibo y solvencia actualizada.
- **Recuperación:** materia reprobada -> intento -> revisión -> publicación -> nueva boleta; explicar el antes/después.
- **Promoción:** razones por estudiante -> decisiones -> confirmación de versiones -> resultado -> reinscripción independiente.

### 15.4 Accesibilidad y calidad de interacción

Objetivo de producto: WCAG 2.2 AA, validado con pruebas de teclado, lector de pantalla, contraste, foco, zoom y errores comprensibles. No mostrar reprobación solo en rojo ni solvencia solo como un candado. [WCAG 2.2 del W3C](https://www.w3.org/TR/WCAG22/).

Definir estados vacíos, carga, error, guardado parcial, permiso revocado, ciclo cerrado, falta de matrícula y sin conexión. En un teléfono pequeño, una grilla de 30 alumnos x 10 componentes debe tener una alternativa usable por alumno/actividad, no solo desplazamiento infinito.

Fechas locales dd/MM/yyyy y hora legible; API con ISO. Español del colegio, etiquetas claras como «encargado», «colegiatura», «bimestre» y «entrega». Evitar términos de implementación en la interfaz de familias.

### 15.5 Flutter y funcionamiento sin conexión

La primera web debe funcionar bien en móviles. Construir Flutter cuando API, cuentas, tareas y notificaciones estén estabilizadas. No duplicar prematuramente cada panel administrativo en móvil.

Prioridad móvil: estudiantes/encargados, agenda, tareas, carga con cámara/archivos, notificaciones y consulta de notas habilitadas; docente con asistencia y revisión ligera. Caja y configuración avanzada se mantienen inicialmente en web.

Caché local con alcance por usuario, expiración y borrado al cerrar sesión/cambiar cuenta. No cachear notas bloqueadas para consultarlas indefinidamente sin verificar solvencia. Propuesta inicial: calificaciones y boletas requieren conexión y autorización actual; offline se reserva para agenda y borradores de tareas. No es posible garantizar revocación de un documento que el usuario ya exportó.

Envío offline: cola local explícita, reintentos y confirmación del servidor. La UI distingue «pendiente de enviar» de «entregado». Tokens en almacenamiento seguro, nunca mezclados con preferencias comunes. Evaluar cliente OIDC y SignalR para Flutter según mantenimiento real; no asumir que cualquier paquete encontrado es apto para producción.

## 16. Migración limitada de personas y relaciones

### 16.1 Alcance preciso

**Se migra:** identidad/expediente mínimo de estudiantes, datos necesarios de padres/encargados ligados a ellos y relaciones verificables, con identificadores de origen y evidencia de conciliación.

**No se migra en esta primera operación:** hashes/contraseñas, sesiones, notas, recuperaciones antiguas, pagos, facturas, deudas históricas, tareas, entregas, chats, archivos históricos, docentes ni roles numéricos. Fotos/documentos son un subalcance optativo, no implícito; requieren clasificación y autorización de contenido.

Se leen schoolyear, classes y studentrelation como contexto de interpretación y deduplicación, no como garantía de que se importará su historial completo. Se configura el ciclo inicial y planes nuevos en destino. Cualquier saldo inicial que el colegio quiera exigir para solvencia se carga en un proyecto separado y conciliado: **la deuda histórica no aparece mágicamente por migrar un estudiante**.

### 16.2 Selección de población

Propuesta predeterminada: examinar las 261 filas student de 2025/2026 para construir un padrón de personas consolidado, conservando su condición activa/archivada según la evidencia. No descartar antiguos alumnos silenciosamente ni activar automáticamente a todos en el nuevo año.

Las 147 filas de 2026 son candidatos del último ciclo observado, no matrículas confirmadas del ciclo de lanzamiento. La institución debe validar continuidad antes de activar matrículas y planes nuevos. Si decide trasladar exclusivamente alumnos vigentes, esa selección se vuelve manifiesto aprobado y los demás quedan en archivo, no borrados.

Padres: incluir únicamente registros relacionados con estudiantes seleccionados, agrupar candidatos anuales y confirmar titular. Los 22 registros parents sin hijos en la copia no se habilitan como cuentas por defecto. No eliminar nada del origen.

No prometer un número final de personas deduplicadas restando simplemente los 80 códigos repetidos: existen una colisión de código/nacimiento y posibles fichas familiares ambiguas. La conciliación informa filas de origen, grupos propuestos, uniones aceptadas, exclusiones y personas finales.

### 16.3 Mapeo de campos y decisiones

| Origen | Destino | Transformación / revisión |
|---|---|---|
| student.studentID | LegacyIdentityMap | Conservar cada ID anual aunque varios apunten a la misma persona |
| student.name | Person.DisplayName / nombre fuente | Preservar original; no partir nombres compuestos automáticamente sin revisión |
| student.dob | Person.BirthDate | NULL si desconocido; fechas inválidas a revisión, nunca inventar nacimiento |
| student.dni | Identificador LegacyPersonalCode | No asumir documento legal; detectar colisiones |
| student.state | Identificador LegacyBalamCode | Código legado, no departamento; verificar estabilidad y unicidad |
| student.registerNO | Identificador LegacyRegisterNumber | Opcional y contextual; no asumir universal |
| student.sex | Campo de expediente según política | Tabla de correspondencia sin valores forzados |
| student.email/phone/address | Contactos/dirección | Normalizar conservando valor original y verificación pendiente |
| student.schoolyearID | Contexto fuente | Resolver por tabla schoolyear; no por texto de bloodgroup |
| student.bloodgroup | Atributo fuente de conciliación | Año textual auxiliar; nunca dato clínico |
| student.bloodgroup2/classesID | Contexto académico fuente | Revisar conflictos entre texto y grado estructurado |
| student.parentID | Relación candidata | Resolver referencia exacta del origen; huérfano a revisión |
| parents.parentsID | LegacyIdentityMap | Conservar los IDs de copias anuales |
| parents.dni | Identificador DPI declarado | Revisar calidad y titular antes de usarlo para unir personas |
| parents.name/father_name/mother_name | Ficha familiar de origen | Confirmar quién es titular; no crear dos cuentas por dos nombres |
| parents.username | LegacyUsername | Referencia de soporte, no username nuevo garantizado |
| parents.active/student.active | Estado fuente | No equivale a consentimiento de portal o matrícula nueva |
| create/modify metadata | Procedencia de importación | Datos de origen preservados según necesidad; actor nuevo será proceso de migración |

### 16.4 Deduplicación que no mezcla hermanos o adultos

1. Normalizar espacios, Unicode, fechas y códigos sin destruir valores originales. No quitar ceros iniciales de identificadores como si fueran números.
2. Formar candidatos por identificador fuerte **verificado**; la etiqueta «Código Personal» por sí sola no prueba su calidad.
3. Combinar señales: código personal, código Balam, nacimiento compatible y continuidad de ciclos. Si hay nombres/nacimientos incompatibles o repetición dentro del mismo ciclo, enviar a revisión.
4. No unir por mismo apellido, mismo teléfono familiar, misma fecha de nacimiento o mismo nombre solamente. Gemelos y homónimos existen.
5. Padres: DPI y username anual sirven como candidatos; confirmar persona titular y correspondencia de sus hijos. Un username reutilizado no concede automáticamente acceso a todos esos niños.
6. Cada unión/desunión deja decisión y motivo. El operador puede previsualizar estudiantes/relaciones afectados con acceso restringido.
7. Tras consolidar, asignar StudentCode nuevo estable o preservar uno validado con restricción. Mantener alias de códigos de origen para búsquedas de soporte.

### 16.5 Vínculos familiares

Una referencia válida student.parentID -> parents.parentsID es evidencia de asociación en el legado, no prueba automática de cada facultad legal. Importar como relación con fuente Legacy y estado PendingVerification cuando la identidad/facultad esté pendiente. Dar acceso solo después de verificación institucional o aceptación de un padrón revisado por el colegio.

Los 65 estudiantes con parentID vacío/0 se importan como estudiantes independientes con incidencia si se requiere encargado; el dato no impide que un adulto tenga su propia cuenta. El estudiante con parentID inexistente queda en revisión, sin inventar padre.

Las familias pueden tener varios hijos y un alumno varios encargados. Crear una relación por par validado. No enlazar automáticamente a todos los alumnos de apellido coincidente ni asumir patria potestad por figurar como contacto o responsable de pago.

### 16.6 Pipeline reproducible

~~~text
Exportación autorizada solo lectura
 -> staging restringido + manifiesto + checksum
 -> normalización sin pérdida de origen
 -> candidatos de identidad
 -> revisión de conflictos
 -> plan de importación / dry-run
 -> personas y perfiles
 -> relaciones verificadas o pendientes
 -> mapa de identificadores + conciliación
 -> activación institucional de cuentas
 -> matrícula y plan nuevos según padrón aprobado
~~~

La herramienta Migration.Cli debe tener modos inspect, plan, dry-run, apply y reconcile. Rechazar apply sin manifiesto de alcance y destino identificados. Staging y exportaciones no se publican en Git, logs ni entornos públicos; usar datos sintéticos para el desarrollo común.

Idempotencia por origen/entidad/ID; no regenerar usuarios en cada ejecución. Versionar las reglas de mapeo. Si cambian datos en origen entre ensayo y corte, obtener delta con watermark confiable y hash o hacer exportación completa comparada; las fechas modify_date del legado no bastan para asumir un CDC perfecto.

### 16.7 Conciliación y criterios del corte

| Control | Aceptación |
|---|---|
| Filas de estudiante seleccionadas | Todas mapeadas a persona, exclusión justificada o incidencia explícita; ninguna perdida silenciosamente |
| Personas consolidadas | Uniones aprobadas y sin conflictos críticos de identidad pendientes para activar cuenta |
| Padres seleccionados | Todos ligados a alumno seleccionado o excluidos con motivo |
| Relaciones | Ninguna cuenta recibe hijos distintos de los aprobados |
| Fechas y códigos | Sin fechas inventadas ni reinterpretación errónea de bloodgroup/state |
| Credenciales | Ningún hash viejo habilitado como PasswordHash nuevo |
| Matrícula inicial | Solo continuidad confirmada; ciclo, grupo y plan correctos |
| Finanzas iniciales | Solvencia no se infiere de la falta de facturas migradas; plan nuevo o saldo inicial separado y conciliado |
| Repetición del importador | Mismas personas/vínculos; sin duplicados |
| Datos excluidos | Permanecen archivados según política, sin aparecer como importados |

Corte: respaldo probado, ventana acordada, origen sin escrituras administrativas durante extracción final o delta controlado, importación, conciliación y validación de una muestra con secretaría. La aprobación de producción corresponde a un resultado concreto y revisable, no a una promesa abstracta de migración.

Reversión antes de abrir escrituras nuevas: volver al origen y retirar el lote destino en ambiente controlado según su manifiesto. Después de recibir pagos/notas/entregas nuevas, no restaurar simplemente una copia vieja: reconciliar operaciones posteriores, pausar el módulo afectado y usar plan de recuperación/fix-forward. No existe rollback trivial sin pérdida después de que ambos sistemas reciben escrituras.

### 16.8 Archivo del legado

Mantener una copia íntegra de BD y archivos con checksum, control de acceso y restauración ensayada. Evitar dejar el PHP viejo expuesto públicamente solo para consultar historia. Propuesta: acceso administrativo restringido o exportaciones documentales verificadas en un archivo protegido, con responsables y retención definidos.

Migrar personas no equivale a corregir las 101 notas huérfanas o las 12 facturas sin estudiante. Su conservación y eventual recuperación son un trabajo independiente que no debe contaminar identidades nuevas ni retrasar el lanzamiento sin necesidad.

## 17. Seguridad, privacidad y auditoría

### 17.1 Modelo de amenazas concreto

| Riesgo | Ejemplo aplicable | Control y prueba |
|---|---|---|
| Acceso horizontal | Un padre cambia studentId para ver otro niño | Política por relación/recurso en consultas, PDF y archivos; pruebas negativas con dos familias |
| Acceso por rol demasiado amplio | Docente califica materia no asignada | TeachingAssignment vigente y permiso de operación verificados en servidor |
| Filtración entre colegios | SchoolId manipulado o consulta sin filtro | Contexto validado, FK compuestas y prueba de aislamiento con dos colegios sintéticos |
| Fuga de notas por insolvencia | API bloqueada pero PDF/notification muestra promedio | Mismo contrato de solvencia en todas las salidas; sin payload parcial de notas |
| Secuestro de cuenta | Invitación adivinable o recuperación por correo compartido | Secretos de un uso, propósito, expiración, limitación de intentos y verificación de titular |
| CSRF | Pago o promoción con cookie desde otro sitio | Antiforgery en mutaciones y controles de origen; nada sensible por GET |
| XSS almacenado | Instrucciones HTML o nombre de archivo malicioso | Sanitización de contenido permitido, codificación de salida y CSP; no confiar solo en frontend |
| Inyección | Filtro, orden o columna enviados a SQL | Parametrización y lista permitida de campos; nunca SQL generado con una columna arbitraria del usuario |
| Archivo peligroso | Documento activo o ejecutable camuflado | Cuarentena, firma/MIME, escaneo, límites y almacenamiento fuera de ejecución web |
| Fraude financiero | Doble confirmación del mismo pago o comprobante | Idempotencia durable, restricciones por proveedor y conciliación |
| Carrera de notas | Dos docentes se sobrescriben | rowversion / versión de libreta y resolución visible de conflictos |
| Evento perdido | Nota publicada sin enviar aviso | Outbox en transacción y consumidores idempotentes |
| Vínculo revocado con caché | Encargado sigue viendo datos después de cambio | Revalidación autoritativa en datos sensibles y descarga; invalidación de caché |
| Exportación excesiva | Archivo descargable con todos los alumnos | Alcance explícito, permiso, auditoría y revalidación al descargar |

### 17.2 Controles de implementación

- HTTPS, encabezados de seguridad, HSTS tras validar dominio y CSP compatible con la UI sin permisos globales innecesarios.
- Identidad y permisos denegados por defecto; cada nuevo endpoint exige revisión de alcance.
- Esquemas de cookie/bearer configurados explícitamente y errores API consistentes.
- Rate limiting en login, invitaciones, recuperación, carga, exportaciones y operaciones de alto costo; incluir protección ante intentos distribuidos.
- Secretos en almacén administrado o variables protegidas, nunca appsettings versionado con valores reales.
- Claves de Data Protection persistentes, protegidas y compartidas cuando haya réplicas; rotación y respaldo definidos. Perderlas afecta cookies y tokens protegidos.
- Aplicación con usuario SQL de mínimos privilegios; identidad separada para desplegar migraciones de esquema.
- Proveedores externos detrás de adaptadores; timeouts, reintentos limitados y protección contra SSRF en URLs de recursos.
- Escaneo de dependencias, secretos, contenedores y código en CI; SBOM y política de parches.
- Validar autorización en WebSocket/SignalR, conexión, membresía y mensaje; no confiar en grupos calculados por cliente.
- En modo de soporte, acceso temporal, motivo, facultades y auditoría. No construir un botón de suplantación sin controles y trazabilidad.

### 17.3 Auditoría de negocio

Registrar: actor autenticado y actor en nombre de quien opera, colegio, recurso, operación, fecha del servidor, motivo, correlación y cambios mínimos necesarios. Eventos prioritarios: vínculo de encargado, activación, cambio de permiso, matrícula, nota, recuperación, publicación, emisión de boleta, generación de cargos, pago, aplicación, reversión y consulta/exportación sensible.

No guardar contraseñas, tokens, secretos de invitación, datos de tarjeta, contenido completo de chats ni documentos de identidad completos en logs técnicos. Si la revisión de una nota requiere antes/después, almacenar esa revisión en el dominio protegido; no esconderla dentro de un log de texto.

Auditoría no editable por usuarios ordinarios, retención controlada y acceso audit.read separado. Un administrador de infraestructura privilegiado aún puede tener capacidad sobre la BD; si el requisito exige evidencia resistente a manipulación de ese nivel, agregar almacenamiento externo inmutable, firma/verificación y separación operacional. No afirmar inmutabilidad absoluta de una tabla que el DBA puede modificar.

### 17.4 Privacidad y datos de menores

Minimizar recopilación. No pedir DPI del niño solo para tener un identificador, ni interpretar su código personal como documento legal. Información de salud, custodia y documentos requiere facultades específicas.

Definir responsable de datos, política de retención, permisos de familias, uso de proveedores, tratamiento de respaldos y procedimiento de exportación/corrección. Confirmar las obligaciones aplicables a Guatemala y al contrato del colegio antes de habilitar políticas con efectos legales; no declarar cumplimiento general basándose en este documento.

Una solicitud de eliminación no debe borrar silenciosamente registros financieros/académicos con retención obligatoria. Resolver por tipo de información y política aprobada, con anonimización cuando proceda. Desactivar cuenta no significa borrar expediente; retirar alumno no significa borrar historial.

Datos reales no se copian a entornos de pruebas comunes. Usar datasets sintéticos con familias, becas, ingresos tardíos y conflictos preparados para validar el producto.

## 18. Operación, infraestructura y rendimiento

### 18.1 Despliegue inicial propuesto

Por afinidad con .NET/SQL Server, usar un servicio administrado de aplicación/contenedores, SQL Server administrado o Azure SQL, almacenamiento privado de objetos y almacén de secretos. Azure es una opción coherente, no requisito para que funcionen los módulos. Mantener configuración y adaptadores que permitan otro hosting compatible.

Componentes iniciales:

~~~text
DNS/TLS y protección perimetral
  -> Host .NET: web estática, Identity UI y API
  -> worker durable del producto
  -> SQL Server / Azure SQL
  -> almacenamiento privado de objetos
  -> correo transaccional
  -> observabilidad y respaldos
  -> Redis cuando esté justificado y configurado
~~~

Ambientes separados: development, test, staging y production. Infraestructura reproducible y secretos distintos. El equipo debe poder compilar desde un entorno limpio sin XAMPP ni la base real.

Para desarrollo local: SDK fijado con global.json, dependencias con lockfiles, SQL Server de desarrollo y object storage compatible/servicio de pruebas; Redis opcional según funcionalidades habilitadas. Verificar licencia de edición SQL y otros componentes; no usar una edición de desarrollo como producción.

### 18.2 Publicación de software

CI: restaurar -> analizar -> compilar -> probar dominio/integración/contratos -> generar clientes -> construir web -> E2E críticos -> escanear -> empaquetar artefactos. Flutter tiene pipeline propio cuando exista; una actualización de API compatible no exige publicar otra app cada vez.

CD: desplegar staging con datos sintéticos, aplicar migraciones revisadas, smoke test, revisar evidencia y promover versión aprobada. Producción recibe cambios de esquema como scripts/bundles controlados; la app no ejecuta migraciones arbitrarias al arrancar.

Usar estrategia expand/contract para cambios incompatibles: agregar estructura compatible, desplegar código, migrar datos con seguimiento y retirar campos viejos después. Desplegar código antiguo contra esquema nuevo solo si se diseñó compatibilidad; una migración destructiva no se revierte con cambiar la imagen.

Feature flags para portales, pagos externos y extensiones. La bandera puede controlar disponibilidad, pero no sustituye permisos. Tener un modo operativo para dejar temporalmente captura en solo lectura mientras se resuelve una incidencia, sin devolver datos a usuarios sin autorización.

### 18.3 Respaldos y recuperación

Objetivos iniciales **de diseño por validar**, no garantías ya logradas:

| Métrica | Objetivo inicial propuesto |
|---|---|
| RPO | Hasta 15 minutos para datos críticos, si el servicio/plan y pruebas lo permiten |
| RTO | Recuperación operativa en hasta 4 horas bajo un escenario documentado |
| Disponibilidad | 99.5% mensual como punto de partida a acordar con operación y presupuesto |
| Ensayo de restauración | Antes de producción y periódicamente con evidencia verificable |

Respaldo de SQL, objetos, configuración y claves necesarias. PITR donde el plan lo permita. Probar consistencia entre referencias de BD y versiones de archivos. Un backup sin prueba de lectura y restauración no completa la aceptación.

Runbooks: caída de BD, error de despliegue, cuota de almacenamiento agotada, correo/push caído, Redis caído, virus detectado, pago duplicado, vínculo incorrecto y nota publicada por error.

### 18.4 Telemetría útil

OpenTelemetry/logs estructurados con traceId y correlación por trabajo. Métricas de latencia/error, consultas SQL lentas, saturación de conexiones, cola/outbox, escaneo de archivos, generación PDF, cobros masivos y fallos de notificación.

Alertas de negocio: publicación incompleta, lote de cargos atascado, matrícula sin plan, comprobante sin revisión dentro del plazo, fallos de cálculo y discrepancias de conciliación. Evitar alarmas por cada correo individual fallido si ya existe reintento; notificar cuando haya impacto accionable.

Dashboard operacional restringido. No exponer connection strings, datos personales o payloads de notas como etiquetas de métricas de alta cardinalidad.

### 18.5 Rendimiento y volumen

La copia actual tiene 147 filas de estudiantes del último ciclo y no justifica arquitectura distribuida compleja. El diseño debe poder crecer sin cargar toda la tabla en cada pantalla.

Perfiles sintéticos propuestos:

| Perfil | Datos / carga a probar | Propósito |
|---|---|---|
| Arranque | 500 estudiantes, 50 cursos, 100 sesiones concurrentes | Lanzamiento inicial con margen sobre la copia local |
| Crecimiento | 3,000 estudiantes, varios ciclos, 300 sesiones concurrentes | Medir límites y capacidad real del colegio en crecimiento |
| Estrés | Volumen mayor y picos sostenidos definidos en prueba | Encontrar saturación; no prometer capacidad sin resultados |

Objetivos iniciales: p95 < 500 ms en lecturas comunes y < 1 s en escrituras simples bajo perfil acordado; carga de archivos, informes y lotes se miden aparte. Publicación de tarea visible a su audiencia en segundos bajo carga normal, con métrica de demoras si hay job. Pago confirmado cambia la decisión de solvencia en la siguiente consulta autoritativa.

Paginación, proyecciones a DTO, AsNoTracking para lecturas, índices de consultas reales, límites de lote y eliminación de N+1. PDF en workers con concurrencia acotada para no agotar memoria. Captura masiva de notas envía cambios y versiones, no todo el expediente de los alumnos.

Caché solo con propósito, alcance e invalidación documentados. Medir antes de agregar Redis o índices adicionales; revisar el plan SQL cuando una consulta se vuelva lenta.

### 18.6 Costos que deben presupuestarse

Desarrollo, QA, revisión académica, soporte inicial, hosting/BD, object storage y transferencia, copias, correo/push, observabilidad, certificados/dominios y licencias de componentes/proveedores elegidos. Comparar costo de operación y mantenimiento, no solo factura de servidor.

No fijar aquí precios de nube o licencias que cambian. Antes de contratación, cotizar con volumen, región, retención, disponibilidad y edición exactos. Lanzar con recursos razonables y métricas; escalar por necesidad demostrada.

## 19. Pruebas y aceptación

### 19.1 Estrategia

- Dominio: cálculo, recuperación, estados, solvencia, obligaciones y elegibilidad.
- Integración con SQL Server real: restricciones, transacciones, concurrencia, importación, idempotencia y consultas. No reemplazar SQL por SQLite/InMemory y asumir la misma semántica.
- API: permisos por recurso, esquemas de autenticación, contratos y errores.
- E2E web: invitación, familia, matrícula tardía, tarea, pago, bloqueo/desbloqueo, libreta, recuperación y promoción.
- Flutter cuando exista: widgets, autenticación externa, carga/reintentos y compatibilidad API.
- Arquitectura: impedir referencias a internals y acceso a tablas ajenas fuera de contratos aprobados.
- Seguridad: pruebas negativas, sesión, CSRF, IDOR, archivos y exposición de datos.
- Operación: carga sintética, caídas de proveedores, restauración y despliegue reproducible.

No perseguir un porcentaje de cobertura vacío. Las reglas de notas, vínculos, solvencia, dinero y migración deben tener ejemplos calculados y pruebas de casos límite.

### 19.2 Escenarios obligatorios de negocio

| ID | Dado / cuando | Resultado esperado |
|---|---|---|
| AT-001 | Padre con dos hijos en grados distintos inicia sesión | Ve ambos según sus facultades, con contexto claro para cada acción |
| AT-002 | Padre cambia el ID a un alumno no vinculado | API, PDF, archivo y exportación rechazan sin filtrar datos |
| AT-003 | Estudiante adulto sin encargado activa invitación | Cuenta propia y matrícula accesibles; no exige crear un padre ficticio |
| AT-004 | Persona es docente y encargado | Una cuenta, vistas separadas y permisos aplicados al recurso |
| AT-005 | Se intenta consumir dos veces una invitación | Solo una activación; segundo intento no crea otra cuenta |
| AT-006 | Usuario abre 2025 y 2026 en pestañas distintas | Consultas/escrituras conservan su contexto explícito |
| AT-007 | Alumno tiene bimestres 60/60/60/60 en una materia | Promedio ordinario 60; aprobada conforme a política de precisión |
| AT-008 | Alumno tiene promedio general alto y una materia en 55 | Esa materia sigue reprobada; no promueve por promedio general |
| AT-009 | Falta una nota bimestral obligatoria | Resultado anual incompleto; no divide silenciosamente entre tres |
| AT-010 | Recuperación de materia en 55 se publica con 72 | Final efectiva 72, ordinaria 55 y bimestres conservados |
| AT-011 | Se emite boleta después de recuperación | Nueva revisión; original intacta y correctamente marcada como sustituida |
| AT-012 | Dos docentes editan la misma celda/versión | Conflicto visible; no sobrescritura silenciosa |
| AT-013 | Docente intenta capturar 8 puntos en actividad de máximo 5 | Servidor rechaza aunque se haya eliminado validación del navegador |
| AT-014 | Se confirma promoción dos veces | Una decisión/matrícula por alcance; mismo StudentId y sin duplicar padre |
| AT-015 | Cambió una recuperación después del preview de promoción | Confirmación vieja rechazada y nuevo cálculo requerido |
| AT-016 | Docente publica tarea para todo su curso | Aparece a todos los destinatarios elegibles; no aparece en cursos ajenos |
| AT-017 | Correo/push falla al publicar tarea | Tarea visible en portal y notificación en reintento; no se duplica publicación |
| AT-018 | Alumno ingresa después de publicar tarea aún abierta | Se incorpora según política con plazo individual razonable y trazabilidad |
| AT-019 | Alumno ingresa después de una tarea vencida | No recibe cero ni deuda de entrega previa; nivelación requiere asignación explícita |
| AT-020 | Alumno entra en B3 con notas oficiales previas | Equivalencias aprobadas alimentan B1/B2 sin inventar actividades |
| AT-021 | Alumno entra en B3 sin notas previas | B1/B2 pendientes de resolución; captura actual sigue funcionando |
| AT-022 | Cambia de sección con entregas existentes | Evidencia conservada y sin doble obligación equivalente |
| AT-023 | Alumno se retira antes de nueva tarea/asistencia | No se generan nuevas obligaciones o faltas fuera de vigencia |
| AT-024 | Alumno confirma entrega dos veces por mala conexión | Un intento idempotente y comprobante con fecha de servidor |
| AT-025 | Alumno reentrega tarea devuelta | Nuevo intento, anterior conservado y vigente identificado |
| AT-026 | Archivo tiene extensión permitida y contenido peligroso | Cuarentena/rechazo; no disponible para descarga ordinaria |
| AT-027 | Plan exige cuotas 01–03 para ver B1 y solo se pagó 01 | Notas bloqueadas en todos los canales familiares/estudiantiles |
| AT-028 | Una transferencia confirmada cubre cuotas 01–03 | Se habilita B1 aunque exista un solo pago |
| AT-029 | Se presenta imagen de depósito, aún sin validar | El estado es En revisión; notas continúan bloqueadas |
| AT-030 | No existen cargos por configuración incompleta | PendingFinancialSetup; no declarar solvencia por lista vacía |
| AT-031 | Cuota incluida en corte aún no vence y sigue pendiente | Bloqueo según corte; no se limita la regla a cargos vencidos |
| AT-032 | Alumno con beca total válida cubre obligación a importe cero | No exigir pago ficticio; solvencia sustentada en ajuste autorizado |
| AT-033 | Alumno ingresa en agosto con plan desde agosto | No se exige enero–julio salvo obligación contractual explícita |
| AT-034 | Se paga una cuota fuera del corte y otra requerida queda pendiente | No se habilitan notas solo por monto total o número de pagos |
| AT-035 | Pago confirmado que daba solvencia es revertido | Nueva consulta/descarga vuelve a bloquear; auditoría y recibo original conservados |
| AT-036 | Hermano A debe y hermano B está solvente | B habilitado; A bloqueado; cuenta familiar no mezcla sus obligaciones |
| AT-037 | Usuario intenta ver nota de tarea para eludir boleta bloqueada | Puntaje/proyección académica también protege GradesView |
| AT-038 | Se publica aviso de notas mientras el alumno debe | Aviso sin puntajes ni PDF adjunto que eludan el bloqueo |
| AT-039 | Lote masivo se reintenta o se crea otro lote de la misma cuota | Restricción natural evita doble cargo |
| AT-040 | Dos cajeros aplican el último saldo de la misma factura | Transacción/concurrencia impiden doble aplicación o saldo inconsistente |
| AT-041 | Pago mayor que deuda seleccionada | Saldo a favor explícito, sin aplicación inventada |
| AT-042 | Lote de cargos terminó parcialmente | Reporte por fila y reintento de fallidos; exitosos intactos |
| AT-043 | Nueva matrícula después del lote del grado | Genera únicamente obligaciones faltantes de esa matrícula |
| AT-044 | Se revoca relación familiar con boleta ya en cola | Descarga revalida y rechaza al actor revocado |
| AT-045 | Importación se ejecuta dos veces | Mismas personas y mapas, sin duplicar vínculos |
| AT-046 | Dos alumnos comparten nombre o un código inconsistente | Cola de revisión, sin unión automática de identidades |
| AT-047 | Campo bloodgroup contiene 2026 y state contiene Código Balam | Mapeo correcto; no registrar salud ni geografía falsas |
| AT-048 | Se restaura respaldo de prueba | BD, objetos y claves requeridas recuperables; evidencia de RPO/RTO |
| AT-049 | Redis está caído | Comportamiento definido y probado; SQL conserva verdad de notas/pagos |
| AT-050 | Usuario de otro colegio manipula SchoolId en escenario sintético | Sin acceso ni asociación entre instituciones |
| AT-051 | Tarea calificada se vincula a componente y luego se corrige | Una sola nota efectiva y revisión, sin doble suma |
| AT-052 | Un cambio de plan intenta reemitir cuota ya existente | Ajuste/versión revisable; no nueva deuda duplicada |

### 19.3 Pruebas de frontera que requieren política aprobada

59.499/59.5/59.999 frente al mínimo de 60; recuperación menor que ordinaria; varios intentos; exoneración parcial; beca que vence; convenio incumplido; pago con fecha efectiva retroactiva; alumno retirado/reingresado; inscripción retroactiva; cambio de curso con una actividad ya calificada; publicación programada en cambio de día/zona.

Los ejemplos de referencia se aprueban con coordinación/caja y se convierten en pruebas del servidor antes de implementar su UI definitiva. No elegir respuestas arbitrarias solo para que pase un test.

### 19.4 Aceptación visual y operacional

Probar escritorio, tablet y teléfono; teclado y lector de pantalla en flujos críticos. Simular conexión lenta, archivo grande, sesión vencida y fallo de proveedor. Revisar boletas/recibos como documentos reales con nombres largos, varias páginas y estados de recuperación.

Hacer piloto con personal y un grupo acotado de familias/estudiantes, puesto que el legado no tiene ese portal lanzado. Recoger problemas de comprensión, soporte y permisos antes de apertura general. Capacitar a secretaría/caja/docentes con datos sintéticos y guías de sus flujos.

## 20. Plan de ejecución e instrucciones para construir

### 20.1 Orden de trabajo y dependencias

El producto se construye por incrementos utilizables. No intentar completar todo el menú legado antes de validar inscripción, pagos, tareas y notas. Las fases no son una estimación contractual; duración depende del equipo, calidad de datos, alcance y disponibilidad del colegio.

| Fase | Entregable | Puerta de salida |
|---|---|---|
| 0. Definición | ADR, reglas académicas/financieras pendientes, mapa de flujos y prototipos | Ejemplos de notas/solvencia y alcance de migración revisados |
| 1. Base operativa | Monolito, SQL, identidad, invitaciones, permisos, auditoría, CI y ambientes | Login/recuperación y aislamiento probados; build limpio |
| 2. Personas y matrícula | Directorio, vínculos, ciclos, cursos, fechas efectivas y plan asignable | Alumno independiente y familia de varios hijos funcionando; ensayo de migración |
| 3. Finanzas esencial | Planes, cobros masivos, pagos/aplicaciones, recibos, evidencia y solvencia | Conciliación e idempotencia; pruebas de corte por publicación |
| 4. Portal y tareas | Portal web real, audiencia, archivos, entregas, revisión y avisos | Casos de inscripción tardía, reentrega y fallos de notificación validados |
| 5. Evaluación completa | Libreta, publicación, bloqueo por solvencia, recuperación y boletas | Misma regla en API/web/PDF; pruebas académicas y financieras integradas |
| 6. Cierre académico | Equivalencias, promoción, reinscripción y asistencia operativa | Decisiones trazables y sin duplicar identidad |
| 7. Piloto y corte | Migración final acotada, restauración, capacitación y lanzamiento | Acta de conciliación, aceptación por función y plan de soporte |
| 8. Flutter | App centrada en familia/estudiante, OIDC, push y cargas | Contratos estables y E2E móvil con servidor real de pruebas |
| 9. Extensiones | Biblioteca/transporte/inventario/RR. HH. según necesidad | Caso de negocio, responsable y aceptación propios |

Asistencia puede desarrollarse en paralelo funcional con notas si el equipo lo permite, pero no depende de lanzar Flutter. Finanzas debe estar lista antes de liberar consulta de notas porque la solvencia es requisito confirmado del portal.

### 20.2 Backlog inicial trazable

Must = indispensable para ese lanzamiento; Should = mejora importante; Could = posterior y optativa. Cada ticket enlaza implementación, prueba de aceptación y evidencia de revisión.

| ID | Prioridad | Requisito | Aceptación mínima relacionada |
|---|---|---|---|
| IAM-001 | Must | Identity único y activación segura | AT-003/005 |
| IAM-002 | Must | Permisos por recurso y colegio | AT-002/004/050 |
| IAM-003 | Must | Recuperación asistida sin depender de correos inexistentes | AT-005 y verificación de titular |
| IAM-004 | Must | Revocación de sesiones y facultades | AT-044 |
| PEO-001 | Must | Persona estable y perfiles independientes de ciclo | AT-003/014 |
| PEO-002 | Must | Varios hijos y varios encargados | AT-001/002 |
| ENR-001 | Must | Matrícula y membresías con vigencia | AT-018/019/022/023 |
| ENR-002 | Must | Ingreso tardío y equivalencias | AT-020/021/033 |
| ACA-001 | Must | Ciclo/período explícitos y sección opcional | AT-006 y alta sin sección |
| ACA-002 | Must | Curso y asignación docente vigentes | AT-013/016 |
| FIN-001 | Must | Conceptos, planes y calendario versionado | AT-030/031/033/052 |
| FIN-002 | Must | Generación masiva con preview y deduplicación | AT-039/042/043 |
| FIN-003 | Must | Pago real, aplicaciones y recibo | AT-028/034/040/041 |
| FIN-004 | Must | Evidencia pendiente y revisión de pago | AT-029 |
| FIN-005 | Must | Solvencia por publicación y alumno | AT-027/028/031/036/038 |
| FIN-006 | Must | Becas, ajustes y reversión auditada | AT-032/035 |
| FIN-007 | Should | Convenios y conciliación externa avanzada | Casos aprobados de convenio y proveedor |
| LRN-001 | Must | Tarea, audiencia y calendario individual | AT-016/017/018/019 |
| LRN-002 | Must | Entregas con comprobante y versiones | AT-024/025 |
| LRN-003 | Must | Archivos privados y escaneo | AT-026 |
| LRN-004 | Must | Retroalimentación y enlace a libreta | AT-037/051 |
| ASM-001 | Must | Cuatro bimestres y aprobación por materia | AT-007/008/009 |
| ASM-002 | Must | Borrador, publicación y concurrencia | AT-012/013 |
| ASM-003 | Must | Recuperación reemplaza final | AT-010/011 |
| ASM-004 | Must | Boletas reproducibles y protección de descarga | AT-011/027/035/044 |
| ASM-005 | Must | Promoción con preview y resolución | AT-014/015 |
| ATT-001 | Should | Asistencia con vigencia y justificación | AT-023 |
| COM-001 | Must | Bandeja/avisos durables y entrega por outbox | AT-017/038 |
| COM-002 | Should | Conversaciones seguras y moderadas | Autorización en lectura/escritura/hub |
| UX-001 | Must | Portal accesible, responsivo y estados claros | §19.4 |
| MIG-001 | Must | Migración solo personas y relaciones trazables | AT-045/046/047 |
| OPS-001 | Must | CI, despliegue, observabilidad y restauración | AT-048/049 |
| MOB-001 | Should | Flutter con mismos contratos y OIDC | E2E de acceso/entrega/solvencia |
| EXT-001 | Could | Módulos no esenciales a la operación inicial | Alcance aprobado por módulo |

Must en recuperación/promoción significa obligatorio para declarar el núcleo académico completo, no que deba aparecer en el primer sprint antes de matrícula. No esconder módulos incompletos detrás de botones que aparentan estar listos.

### 20.3 Documentación que debe existir en el repositorio nuevo

README.md con preparación y comandos reproducibles; docs/PROJECT_DEFINITION.md con requisitos; docs/ARCHITECTURE.md; docs/SECURITY.md; docs/ACADEMIC_RULES.md; docs/BILLING_AND_CLEARANCE.md; docs/MIGRATION.md; docs/ROADMAP.md; docs/PROGRESS.md y docs/RUNBOOKS.md.

ADR mínimos:

- ADR-001: monolito modular, propiedad de tablas y transacciones.
- ADR-002: Angular frente a MVC; decisión de presentación.
- ADR-003: SQL Server/hosting, edición y operación.
- ADR-004: Identity, cookie web, OpenIddict y móvil.
- ADR-005: identidad estable, matrícula y vigencias.
- ADR-006: notas, redondeo, recuperación y publicación.
- ADR-007: obligaciones de colegiatura, corte y solvencia.
- ADR-008: archivos, cuarentena, retención y descargas.
- ADR-009: outbox, workers y uso/degradación de Redis.
- ADR-010: migración acotada y archivo histórico.

### 20.4 Instrucción de arranque para quien implemente

~~~text
Construye una plataforma nueva basada en este documento. Trabaja primero en
los casos de uso completos, no en copiar pantallas del PHP.

1. Lee las decisiones CONFIRMADAS y registra las PROPUESTAS como ADR.
2. No uses credenciales, contraseñas ni datos personales del legado en demos.
3. Crea la solución .NET con límites de módulos y SQL Server reproducible.
4. Implementa Identity, invitaciones, permisos y auditoría antes del portal.
5. Separa persona, cuenta, vínculo familiar y matrícula anual.
6. Implementa fechas efectivas de curso y matrícula antes de tareas/notas.
7. Implementa planes/cargos/pagos/aplicaciones y solvencia antes de consultar
   notas desde el portal. El corte de cada publicación es configurable.
8. Implementa tareas con audiencia verificable y reglas de ingreso tardío.
9. Implementa libreta y publicación usando una sola política académica:
   cuatro bimestres, mínimo 60 por materia y recuperación que reemplaza final.
10. Conserva notas ordinarias, revisiones e informes emitidos anteriores.
11. Construye UI accesible y útil para cada rol; no dupliques reglas en clientes.
12. Demuestra los escenarios de aceptación con SQL Server y datos sintéticos.
13. Migra únicamente personas y vínculos según el manifiesto aprobado.
14. Prepara el corte con conciliación, restauración y capacitación verificadas.
15. Luego implementa Flutter contra la misma API; las extensiones siguen
    a los flujos esenciales y no retrasan el colegio sin necesidad.

No declares terminada una función que solo tiene pantallas, stubs o datos mock.
No ejecutes el PHP como especificación de resultados cuando contradiga una
regla de negocio confirmada o una validación de seguridad.
No solicites aprobación para cada decisión técnica reversible; documenta y
avanza. Para activar políticas de negocio pendientes o hacer un corte real,
presenta primero el resultado y sus efectos para la revisión del responsable.
~~~

### 20.5 Definición de terminado

Un incremento está terminado cuando el caso de uso guarda/consulta datos reales del ambiente de prueba, autoriza correctamente, maneja errores y concurrencia relevantes, tiene UI funcional y accesible, pruebas de riesgo acordes, documentación y telemetría. No hay botones vacíos, métodos simulados escondidos ni reglas pendientes presentadas como aprobadas.

Una entrega está lista para producción cuando además tiene migración/configuración revisada, respaldo/restauración comprobados, aceptación del área responsable, datos conciliados y soporte para el flujo que se abre a usuarios. El portal nuevo requiere acompañamiento porque no había uso operativo previo de alumnos en el PHP.

## 21. Decisiones pendientes y riesgos

### 21.1 Lo que ya está resuelto

Un colegio preparado para crecer; backend .NET propuesto; comparación MVC/Angular documentada; estudiantes con cuenta propia; ciclos desacoplados de identidad; portal nuevo real; notas por materia con cuatro bimestres y mínimo 60; recuperación reemplaza final; boletas revisadas; solvencia obligatoria para consulta; planes y cortes configurables; cobros masivos; migración de personas/vínculos sin transacciones antiguas.

### 21.2 Pendientes concretos, sin bloquear el documento

| ID | Decisión | Propuesta predeterminada / responsable |
|---|---|---|
| PND-001 | Redondeo y precisión de aprobación | Comparar nota sin redondeo de presentación; coordinación debe aprobar ejemplos 59.5/59.999 |
| PND-002 | Máximo de intentos y nota de recuperación menor | Registrar todos; publicar resolución según política aprobada, sin borrar ordinaria |
| PND-003 | Calendario exacto, cuotas e importes | Parametrizar por plan/ciclo; caja/dirección configura antes de emitir |
| PND-004 | Corte de solvencia de cada publicación | Selección explícita de períodos, revisada al publicar; sin mes fijo |
| PND-005 | Historial bloqueado por deuda posterior | Predeterminado propuesto: corte propio de la publicación; confirmar si se quiere regla más amplia |
| PND-006 | Convenios y excepciones de acceso | Solo autorización explícita, vigente y auditada |
| PND-007 | Prorrateo/retiro y cobros por ingreso tardío | Según plan acordado; no cobrar meses anteriores por defecto |
| PND-008 | Notas previas de traslado sin evidencia | Mantener resultado incompleto y permitir nivelación/equivalencia aprobada |
| PND-009 | Facultades de estudiantes menores y de encargados | Política institucional y revisión legal aplicable |
| PND-010 | Identidad real detrás de fichas parents ambiguas | Revisión asistida antes de activar cuentas y relaciones |
| PND-011 | Padrón final a trasladar | Propuesta: consolidar todas las filas y activar solo continuidad confirmada |
| PND-012 | Deuda inicial del nuevo ciclo | No migrada; si se necesita, importación separada y conciliada |
| PND-013 | Hosting, edición SQL, presupuesto y equipo | Definir antes de comprometer capacidad/fechas |
| PND-014 | Angular o MVC en ejecución | Plan base Angular; MVC si equipo/costo lo justifica, sin cambiar dominio |
| PND-015 | Proveedores de pagos, correo, push y fiscalidad | Adaptadores; no anunciar disponibilidad ni cumplimiento antes de integrarlos |
| PND-016 | Retención, acceso a legado y archivos optativos | Política documentada por tipo de información |
| PND-017 | Entregas por encargado en nombre del hijo | Desactivadas por defecto hasta habilitación explícita |
| PND-018 | Cuotas máximas de archivos y conexión objetivo | Valores propuestos en §12; validar con usuarios/equipo |
| PND-019 | Asistencia como requisito de promoción | No bloquear promoción por asistencia sin regla aprobada |

### 21.3 Riesgos de ejecución y medidas

- **Alcance excesivo:** el menú viejo contiene módulos incompletos o vacíos. Implementar primero el circuito matrícula -> plan -> cobro -> tarea -> nota -> recuperación/promoción.
- **Elegir frontend por prestigio:** Angular aporta estructura de UI compleja, pero necesita mantenimiento. Si el equipo no lo sostiene, MVC es preferible a una SPA abandonada.
- **Solvencia mal calculada:** distinguir obligación, aplicación, comprobante y plan completo; probar cada salida de notas.
- **Migración que une personas erróneas:** preferir revisión a unir por nombre/código no confiable; ningún vínculo dudoso habilita portal.
- **Puertas traseras de información:** boleta, tarea calificada, caché offline, push y reporte deben obedecer el mismo acceso.
- **Entrada tardía tratada como incumplimiento:** fechas efectivas y estados explícitos; no generar ceros/faltas/deudas retroactivas sin política.
- **Dependencias operacionales innecesarias:** Redis, apps y servicios externos se incorporan con objetivos claros y degradación probada.
- **Lanzamiento sin adopción:** pilotos y capacitación con secretaría/caja/docentes y familias; el código de un portal sin uso previo no valida experiencia.

## 22. Fuentes y referencias

### 22.1 Evidencia local

Repositorio analizado: C:/xampp/htdocs/sys/sys-escolar. El anexo A identifica todos los controladores y sus métodos con líneas de declaración verificadas. El anexo B contiene el esquema observado, tipos, nullabilidad, claves y conteos exactos. El anexo C ofrece consultas de diagnóstico de solo lectura.

Rutas principales: index.php; mvc/core/MY_Model.php; mvc/libraries/Admin_Controller.php; mvc/models/Signin_m.php; mvc/controllers/Student.php; Parents.php; Schoolyear.php; Mark.php; Promotion.php; Assignment.php; Invoice.php; Paymenthistory.php; mvc/views/mark; mvc/views/promotion; mvc/config/config.php; mvc/language/spanish; maintenance/reparar_comunicacion_bimestre4_2026.sql.

No se incorporaron datos de identidad de alumnos/padres, contraseñas, hashes de credenciales ni secretos de configuración al documento. Los conteos y anomalías son agregados locales.

### 22.2 Documentación primaria consultada

Consultada para esta propuesta con fecha de referencia 4 de noviembre de 2026. Confirmar versiones exactas y soporte al iniciar implementación; las decisiones de negocio de este documento provienen del propietario o están marcadas como propuestas.

| Fuente | Para qué se usó |
|---|---|
| [Política de soporte .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) | Elección de línea LTS y mantenimiento |
| [Scaffolding de Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/scaffold-identity?view=aspnetcore-10.0) | UI de autenticación sobre componentes del framework |
| [Identity para API de SPA](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0) | Cookies y diferencia respecto de tokens/OIDC |
| [OpenIddict: elección de flujo](https://documentation.openiddict.com/guides/choosing-the-right-flow.html) | Authorization Code y PKCE |
| [OpenIddict: ASP.NET Core](https://documentation.openiddict.com/integrations/aspnet-core.html) | Integración del servidor de autorización |
| [Angular: releases](https://angular.dev/reference/releases) | Línea activa y política de soporte |
| [Angular: compatibilidad](https://angular.dev/reference/versions) | Compatibilidad de herramientas del frontend |
| [Flutter: archivo de SDK](https://docs.flutter.dev/install/archive) | Selección del canal stable y verificación de lanzamiento |
| [EF Core: concurrencia](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) | rowversion y conflictos de edición |
| [ASP.NET Core: HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0) | Caché opcional y mantenible |
| [OWASP: autorización](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html) | Denegación por defecto y control por recurso |
| [OWASP: carga de archivos](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) | Validación, límites, ubicación y permisos de archivos |
| [W3C: WCAG 2.2](https://www.w3.org/TR/WCAG22/) | Objetivo de accesibilidad de la web |

La arquitectura y los flujos detallados son una propuesta específica para el colegio basada en la investigación. Las fuentes técnicas no establecen las reglas de promoción, colegiatura, recuperación, custodia o retención del colegio.


## 23. Anexo A: inventario completo de controladores

Inventario estructural de los 62 controladores presentes. Las líneas enlazan declaraciones de métodos de esta copia; no equivalen a validación operativa de cada acción. El uso actual es administrativo, como confirmó el propietario.

### 23.1 Controladores y métodos

| Archivo / responsabilidad | Métodos declarados y línea |
|---|---|
| **Assignment.php** — Tareas, audiencia, archivos y entregas de alumnos; portal previsto, no lanzado. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:18), [rules_fileupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:59), [fileuploadans](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:70), [fileupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:107), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:157), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:250), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:309), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:383), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:409), [assignmentanswer](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:488), [unique_classes](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:546), [unique_section](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:554), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:577), [pastdate_check](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:595), [unique_subject](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:608), [subjectcall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:616), [sectioncall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:627), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:637), [download](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:647), [answerdownload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Assignment.php:672) |
| **Backup.php** — Descarga administrativa de respaldo de BD. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Backup.php:6), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Backup.php:17) |
| **Book.php** — Catálogo y cantidades de biblioteca. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Book.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Book.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Book.php:18), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Book.php:34), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Book.php:61), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Book.php:100), [unique_book](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Book.php:116), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Book.php:136), [valid_number_for_quantity](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Book.php:144) |
| **Category.php** — Categorías de residencia/alojamiento y capacidad; no categorías de factura. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:16), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:22), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:38), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:81), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:151), [unique_class_type](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:170), [updateHostelCapacityForCategory](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:190), [restorePreviousHostelCapacity](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:206), [allhostel](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:215), [allclasses](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:223), [allteacher](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:231), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:239), [userscall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Category.php:246) |
| **Classes.php** — Grados/clases y docente responsable. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:14), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:25), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:31), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:73), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:133), [unique_classes](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:144), [unique_classes_numeric](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:164), [allteacher](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:184), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Classes.php:192) |
| **Conversation.php** — Bandejas, destinatarios, mensajes y adjuntos. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:6), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:24), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:50), [draft](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:90), [sent](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:129), [trash_msg](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:168), [draft_send](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:208), [create](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:221), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:581), [classCall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:679), [adminCall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:687), [teacherCall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:695), [parentCall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:703), [userCall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:711), [call_all_student](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:720), [checkUserAuth](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:733), [fav_status](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:738), [delete_conversation](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:754), [delete_trash_to_trash](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Conversation.php:772) |
| **Dashboard.php** — Panel y resúmenes según tipo de usuario. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Dashboard.php:6), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Dashboard.php:214), [getDayWiseAttendance](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Dashboard.php:486), [dayWiseExpenseOrIncome](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Dashboard.php:497), [getSubjectWiseAttendance](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Dashboard.php:516), [getAllRec](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Dashboard.php:544) |
| **Eattendance.php** — Asistencia a exámenes. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:18), [rulessearch](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:44), [rulessearchparents](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:65), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:82), [exam_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:232), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:242), [check_exam](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:334), [check_classes](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:343), [check_subject](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:352), [check_section](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:361), [check_student](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:370), [subjectcall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:379), [sectioncall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:390), [single_add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:402), [all_add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Eattendance.php:425) |
| **Event.php** — Eventos y participación. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:14), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:20), [photoupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:46), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:91), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:131), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:190), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:210), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:231), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:252), [eventcounter](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Event.php:271) |
| **Exam.php** — Bimestres/exámenes del catálogo. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam.php:18), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam.php:39), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam.php:76), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam.php:121), [unique_exam](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam.php:132), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam.php:152) |
| **Exam_type.php** — Tipos de evaluación en línea; modelo apunta a tabla ausente. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam_type.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam_type.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam_type.php:18), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam_type.php:39), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam_type.php:73), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exam_type.php:118) |
| **Examschedule.php** — Calendario de exámenes por curso/grupo. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:17), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:101), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:147), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:206), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:269), [examschedule_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:287), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:297), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:307), [allsubject](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:325), [subjectcall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:333), [sectioncall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:344), [allexam](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:355), [allclasses](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:364), [allsection](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:373), [pastdate_check](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Examschedule.php:382) |
| **Exceptionpage.php** — Pantallas de error/acceso. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exceptionpage.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exceptionpage.php:11), [error](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Exceptionpage.php:16) |
| **Expense.php** — Gastos administrativos. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Expense.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Expense.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Expense.php:19), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Expense.php:45), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Expense.php:85), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Expense.php:139), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Expense.php:150), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Expense.php:168) |
| **Feetypes.php** — Conceptos de cobro. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Feetypes.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Feetypes.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Feetypes.php:18), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Feetypes.php:30), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Feetypes.php:53), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Feetypes.php:88), [unique_feetypes](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Feetypes.php:99) |
| **Grade.php** — Escalas de calificación, no grado escolar. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:18), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:49), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:75), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:113), [unique_grade](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:124), [unique_point](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:144), [unique_gradefrom](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:164), [unique_gradeupto](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Grade.php:184) |
| **Hmember.php** — Membresía/alojamiento de estudiantes. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:19), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:35), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:129), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:202), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:288), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:312), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:460), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:616), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:649), [categorycall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:659), [unique_select](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:670), [unique_gender](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:678), [unique_category](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hmember.php:712) |
| **Holiday.php** — Feriados y calendario. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:13), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:20), [photoupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:51), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:96), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:132), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:181), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:199), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:221), [todate_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:239), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:265), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Holiday.php:288) |
| **Hostel.php** — Catálogo de residencias. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hostel.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hostel.php:14), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hostel.php:20), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hostel.php:38), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hostel.php:72), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hostel.php:118), [unique_name](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hostel.php:134), [unique_htype](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Hostel.php:153) |
| **Instruction.php** — Instrucciones; tabla del modelo ausente. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Instruction.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Instruction.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Instruction.php:18), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Instruction.php:34), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Instruction.php:67), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Instruction.php:112), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Instruction.php:129), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Instruction.php:145), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Instruction.php:161) |
| **Invoice.php** — Cargos, consulta de deuda e ingreso de pagos. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:9), [loadFeetypeData](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:25), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:41), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:255), [payment_rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:288), [payumoney_rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:305), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:326), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:422), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:511), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:548), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:637), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:724), [payment](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:745), [generateHash](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1096), [getCancelPayment](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1128), [stripe](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1134), [unique_amount](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1221), [call_all_student](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1245), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1258), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1268), [unique_classID](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1286), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1294), [unique_paymentmethod](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Invoice.php:1302) |
| **Issue.php** — Préstamos/devoluciones de biblioteca y multas. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:18), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:105), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:146), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:189), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:245), [returnbook](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:263), [unique_quantity](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:291), [unique_lID](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:326), [unique_book](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:335), [unique_book_call](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:355), [wrong_date](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:363), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:374), [bookIDcall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:392), [match_bookauthor](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:405), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:428), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:436), [add_invoice](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Issue.php:446) |
| **Language.php** — Cambio de idioma de sesión. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Language.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Language.php:10) |
| **Lmember.php** — Membresías bibliotecarias de alumnos. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:18), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:34), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:130), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:190), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:234), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:254), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:397), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:546), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:578), [unique_lID](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:588), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Lmember.php:609) |
| **Mailandsms.php** — Mensajería segmentada y envío de correo/SMS según proveedor. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:5), [rules_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:27), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:69), [getMark](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:99), [getMark2](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:114), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:128), [userConfigEmail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:961), [userConfigSMS](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1127), [alltemplate](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1288), [allusers](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1305), [allstudent](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1366), [check_email_usertypeID](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1385), [alltemplatedesign](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1394), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1407), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1418), [attachSenderNamesToRecords](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1436), [buildSenderDetails](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1481), [getClassesMap](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1525), [getSectionsMap](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsms.php:1541) |
| **Mailandsmstemplate.php** — Plantillas de comunicación. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:14), [rules_email](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:20), [rules_sms](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:46), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:72), [email_user_check](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:191), [sms_user_check](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:199), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:207), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:345), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mailandsmstemplate.php:362) |
| **Make_payment.php** — Pago de personal; dependencias y tablas incompletas. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Make_payment.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Make_payment.php:21), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Make_payment.php:56), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Make_payment.php:92), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Make_payment.php:237), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Make_payment.php:363), [role_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Make_payment.php:383), [unique_payment_method](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Make_payment.php:393), [month_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Make_payment.php:401) |
| **Manage_salary.php** — Asignación salarial; dependencias y tablas incompletas. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:18), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:54), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:70), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:144), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:217), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:307), [role_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:337), [templatecall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:347), [unique_salary](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:373), [unique_template](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Manage_salary.php:381) |
| **Mark.php** — Captura, actualización, consulta y boletas de notas. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:23), [getMark](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:45), [getMarkSendToMail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:156), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:224), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:300), [get_setting_mark_percentage](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:460), [getMarkRelationArray](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:482), [mark_send](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:492), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:524), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:587), [print_nueva](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:651), [print_all](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:712), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:740), [mark_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:761), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:771), [subjectcall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:781), [sectioncall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:794), [check_exam](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:807), [check_classes](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:815), [check_subject](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Mark.php:823) |
| **Markpercentage.php** — Componentes/máximos de calificación. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Markpercentage.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Markpercentage.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Markpercentage.php:20), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Markpercentage.php:36), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Markpercentage.php:64), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Markpercentage.php:113), [unique_markpercentage](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Markpercentage.php:143) |
| **Media.php** — Carpetas, archivos y compartición. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:5), [userTableCall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:19), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:65), [create_folder](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:121), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:142), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:214), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:225), [media_share](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:273), [deletef](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:322), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:369), [classcall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Media.php:397) |
| **Menu.php** — Navegación configurable; no sustituye autorización. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Menu.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Menu.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Menu.php:18), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Menu.php:59), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Menu.php:90), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Menu.php:135), [menuList](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Menu.php:146), [menuTrees](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Menu.php:153) |
| **Migration.php** — Invocación por web de migraciones CodeIgniter. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Migration.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Migration.php:9) |
| **Notice.php** — Avisos institucionales. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:13), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:21), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:42), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:77), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:124), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:154), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:171), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:189), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Notice.php:206) |
| **Parents.php** — Fichas/cuentas familiares anuales. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:14), [photoupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:75), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:122), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:131), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:171), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:214), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:233), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:257), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:280), [unique_email](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:299), [unique_dni](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:348), [lol_username](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:395), [active](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Parents.php:440) |
| **Paymenthistory.php** — Consulta, edición y borrado de pagos manuales. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:5), [isEditablePayment](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:16), [paidStatusFor](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:20), [payment_rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:29), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:48), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:115), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:189), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:228), [unique_amount](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:236), [unique_paymentmethod](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:262), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymenthistory.php:271) |
| **Paymentsettings.php** — Parámetros de proveedores de pago; valores no reproducidos. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymentsettings.php:6), [rules_paypal](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymentsettings.php:18), [rules_stripe](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymentsettings.php:49), [rules_voguepay](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymentsettings.php:65), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Paymentsettings.php:86) |
| **Permission.php** — Asignación de permisos a tipos de usuario. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permission.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permission.php:13), [permission_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permission.php:38), [save](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permission.php:48) |
| **Permissionlog.php** — Solicitudes/leaveapp; no auditoría completa de seguridad. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permissionlog.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permissionlog.php:13), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permissionlog.php:19), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permissionlog.php:40), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permissionlog.php:74), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Permissionlog.php:120) |
| **Profile.php** — Perfil del usuario autenticado. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Profile.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Profile.php:19), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Profile.php:48), [photoupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Profile.php:143), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Profile.php:197), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Profile.php:281), [unique_email](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Profile.php:302), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Profile.php:329), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Profile.php:356) |
| **Promotion.php** — Cálculo y copias anuales de estudiantes/encargados. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:11), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:31), [get_setting_mark_percentage](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:99), [promotion_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:121), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:133), [studentPromotionCalculation](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:166), [summary](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:315), [promotion_to_next_class](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:352), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:504), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Promotion.php:539) |
| **Report.php** — Reportes y certificados; parte de certificados referencia modelo ausente. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:5), [student](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:33), [classreport](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:39), [getClassReport](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:46), [attendancereport](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:118), [getAttendacneReport](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:140), [studentreport](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:196), [getStudentReport](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:220), [getCondition](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:276), [getSection](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:315), [getSubject](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:330), [certificate](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:347), [getStudentList](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:358), [generate_certificate](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:386), [tagConvertForTemplate](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Report.php:482) |
| **Reset.php** — Recuperación de contraseña por correo. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Reset.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Reset.php:13), [rules_email](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Reset.php:29), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Reset.php:40), [password](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Reset.php:115) |
| **Resetpassword.php** — Restablecimiento administrativo de credenciales. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Resetpassword.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Resetpassword.php:19), [unique_users](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Resetpassword.php:45), [unique_username](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Resetpassword.php:53), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Resetpassword.php:61), [userscall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Resetpassword.php:128) |
| **Routine.php** — Horarios académicos. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:21), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:90), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:141), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:198), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:262), [routine_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:280), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:290), [exist_teacher](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:300), [allteacher](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:320), [allschoolyear](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:328), [allsubject](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:336), [allsection](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:344), [subjectcall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:352), [sectioncall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:364), [unique_room](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:376), [allclasses](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Routine.php:396) |
| **Sattendance.php** — Asistencia de estudiantes diaria/por materia. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:10), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:30), [subject_rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:51), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:77), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:170), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:320), [singl_add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:330), [all_add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:377), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:402), [pstudent_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:548), [check_classes](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:558), [check_section](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:566), [check_subject](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:574), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:582), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:600), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:744), [valid_future_date](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:780), [subjectall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:789), [sectionall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Sattendance.php:800) |
| **Schoolyear.php** — Catálogo y selección de ciclos. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:12), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:33), [schoolyear_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:53), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:63), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:96), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:138), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:159), [unique_schoolyear](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:167), [unique_schoolyeartitle](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:190), [toggleschoolyear](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Schoolyear.php:215) |
| **Section.php** — Secciones por grado. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:14), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:30), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:54), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:97), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:153), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:165), [allclasses](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:174), [allteacher](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:182), [section_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:190), [unique_section](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Section.php:200) |
| **Setting.php** — Configuración institucional y componentes activos. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Setting.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Setting.php:15), [photoupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Setting.php:108), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Setting.php:148), [carneupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Setting.php:216), [get_setting_mark_percentage](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Setting.php:257), [unique_day](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Setting.php:273), [unique_attendance](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Setting.php:287), [unique_schoolyear](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Setting.php:295) |
| **Signin.php** — Login, logout y cambio de contraseña. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Signin.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Signin.php:16), [rules_cpassword](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Signin.php:33), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Signin.php:54), [cpassword](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Signin.php:83), [old_password_unique](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Signin.php:100), [signout](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Signin.php:109) |
| **Student.php** — Expediente, datos académicos anuales y carnés. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:21), [photoupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:83), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:128), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:224), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:352), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:480), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:620), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:773), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:800), [unique_roll](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:850), [lol_username](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:871), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:916), [unique_classesID](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:937), [unique_sectionID](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:945), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:953), [print_carne_grado](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:963), [unique_email](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:986), [sectioncall](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:1034), [unique_capacity](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:1045), [unique_registerNO](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:1085), [unique_dni](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:1106), [print_carne](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:1150), [active](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Student.php:1178) |
| **Studentgroup.php** — Agrupación de alumnos; esquema observado incompleto. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Studentgroup.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Studentgroup.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Studentgroup.php:18), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Studentgroup.php:29), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Studentgroup.php:62), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Studentgroup.php:106), [unique_group](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Studentgroup.php:122) |
| **Subject.php** — Materias por grado/docente. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:18), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:76), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:102), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:148), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:206), [unique_subject](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:223), [unique_subject_code](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:243), [subject_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:263), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:273), [allclasses](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:283), [allteacher](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:291), [alltype](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Subject.php:300) |
| **Syllabus.php** — Documentos/recursos por grado y ciclo. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:15), [fileupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:41), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:96), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:186), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:228), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:281), [unique_classes](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:304), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:312), [download](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Syllabus.php:322) |
| **Systemadmin.php** — Cuentas de administración en tabla separada. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:12), [photoupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:73), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:118), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:124), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:175), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:235), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:258), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:281), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:303), [lol_username](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:333), [unique_dni](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:378), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:423), [unique_email](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:441), [active](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Systemadmin.php:486) |
| **Tattendance.php** — Asistencia de docentes. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:13), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:24), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:30), [singl_add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:97), [all_add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:118), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:136), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:155), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:173), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:198), [valid_future_date](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tattendance.php:219) |
| **Teacher.php** — Perfiles/cuentas de docentes. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:12), [photoupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:73), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:118), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:126), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:181), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:235), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:256), [lol_username](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:279), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:324), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:342), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:365), [unique_email](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:387), [unique_dni](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:433), [active](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Teacher.php:480) |
| **Tmember.php** — Asignación de transporte a estudiantes. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:18), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:34), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:129), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:186), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:240), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:259), [student_list](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:397), [transport_fare](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:407), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:417), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:562), [alltransport](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:594), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Tmember.php:602) |
| **Transport.php** — Rutas, vehículos y tarifas. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Transport.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Transport.php:14), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Transport.php:20), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Transport.php:46), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Transport.php:72), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Transport.php:109), [unique_route](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Transport.php:125), [valid_number](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Transport.php:145), [valid_number_for_fare](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Transport.php:153) |
| **Uattendance.php** — Asistencia del personal; uattendance no existe en el esquema observado. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:15), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:26), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:43), [singl_add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:111), [all_add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:141), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:164), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:185), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:203), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:227), [valid_future_date](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Uattendance.php:249) |
| **User.php** — Cuentas de personal en tabla separada. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:5), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:13), [photoupload](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:79), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:124), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:130), [unique_usertypeID](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:179), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:194), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:244), [view](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:265), [print_preview](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:282), [send_mail](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:303), [lol_username](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:325), [unique_dni](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:370), [date_valid](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:415), [unique_email](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:433), [active](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/User.php:478) |
| **Usertype.php** — Tipos/roles numéricos del legado. | [__construct](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Usertype.php:5), [index](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Usertype.php:12), [rules](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Usertype.php:20), [add](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Usertype.php:31), [edit](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Usertype.php:58), [delete](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Usertype.php:94), [unique_usertype](C:/xampp/htdocs/sys/sys-escolar/mvc/controllers/Usertype.php:117) |

### 23.2 Modelos y tabla declarada

Este inventario también muestra discrepancias entre código y esquema. Modelos de consulta o configuración pueden usar varias tablas; la columna indica solo _table_name cuando existe. No se extrajeron valores de configuración ni credenciales.

| Modelo | Tabla declarada | Existe en esta BD |
|---|---|---|
| [Alert_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Alert_m.php) | alert | Sí |
| [Assignment_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Assignment_m.php) | assignment | Sí |
| [Assignmentanswer_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Assignmentanswer_m.php) | assignmentanswer | Sí |
| [Automation_rec_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Automation_rec_m.php) | automation_rec | Sí |
| [Automation_shudulu_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Automation_shudulu_m.php) | automation_shudulu | Sí |
| [Book_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Book_m.php) | book | Sí |
| [Category_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Category_m.php) | category | Sí |
| [Classes_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Classes_m.php) | classes | Sí |
| [Conversation_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Conversation_m.php) | conversations | Sí |
| [Dashboard_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Dashboard_m.php) | notice | Sí |
| [Eattendance_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Eattendance_m.php) | eattendance | Sí |
| [Event_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Event_m.php) | event | Sí |
| [Eventcounter_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Eventcounter_m.php) | eventcounter | Sí |
| [Exam_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Exam_m.php) | exam | Sí |
| [Exam_type_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Exam_type_m.php) | online_exam_type | **No** |
| [Examschedule_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Examschedule_m.php) | examschedule | Sí |
| [Expense_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Expense_m.php) | expense | Sí |
| [Feetypes_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Feetypes_m.php) | feetypes | Sí |
| [Grade_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Grade_m.php) | grade | Sí |
| [Hmember_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Hmember_m.php) | hmember | Sí |
| [Holiday_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Holiday_m.php) | holiday | Sí |
| [Hostel_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Hostel_m.php) | hostel | Sí |
| [Idmanager_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Idmanager_m.php) | idmanager | Sí |
| [Instruction_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Instruction_m.php) | instruction | **No** |
| [Invoice_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Invoice_m.php) | invoice | Sí |
| [Issue_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Issue_m.php) | issue | Sí |
| [Lmember_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Lmember_m.php) | lmember | Sí |
| [Loginlog_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Loginlog_m.php) | loginlog | Sí |
| [Mailandsms_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Mailandsms_m.php) | mailandsms | Sí |
| [Mailandsmstemplate_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Mailandsmstemplate_m.php) | mailandsmstemplate | Sí |
| [Mailandsmstemplatetag_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Mailandsmstemplatetag_m.php) | mailandsmstemplatetag | Sí |
| [Make_payment_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Make_payment_m.php) | make_payment | **No** |
| [Manage_salary_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Manage_salary_m.php) | manage_salary | **No** |
| [Mark_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Mark_m.php) | mark | Sí |
| [Markpercentage_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Markpercentage_m.php) | markpercentage | Sí |
| [Markrelation_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Markrelation_m.php) | markrelation | Sí |
| [Media_category_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Media_category_m.php) | media_category | Sí |
| [Media_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Media_m.php) | media | Sí |
| [Media_share_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Media_share_m.php) | media_share | Sí |
| [Menu_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Menu_m.php) | menu | Sí |
| [Message_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Message_m.php) | message | Sí |
| [Notice_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Notice_m.php) | notice | Sí |
| [Parents_info_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Parents_info_m.php) | Sin tabla base declarada | Modelo de consulta/configuración |
| [Parents_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Parents_m.php) | parents | Sí |
| [Payment_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Payment_m.php) | payment | Sí |
| [Payment_settings_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Payment_settings_m.php) | Sin tabla base declarada | Modelo de consulta/configuración |
| [Permission_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Permission_m.php) | permissions | Sí |
| [Permissionlog_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Permissionlog_m.php) | permissions | Sí |
| [Promotionlog_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Promotionlog_m.php) | promotionlog | Sí |
| [Reply_msg_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Reply_msg_m.php) | reply_msg | Sí |
| [Reset_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Reset_m.php) | reset | Sí |
| [Resetpassword_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Resetpassword_m.php) | Sin tabla base declarada | Modelo de consulta/configuración |
| [Routine_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Routine_m.php) | routine | Sí |
| [Sattendance_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Sattendance_m.php) | attendance | Sí |
| [Schoolyear_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Schoolyear_m.php) | schoolyear | Sí |
| [Section_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Section_m.php) | section | Sí |
| [Setting_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Setting_m.php) | setting | Sí |
| [Signin_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Signin_m.php) | Sin tabla base declarada | Modelo de consulta/configuración |
| [Site_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Site_m.php) | setting | Sí |
| [Student_info_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Student_info_m.php) | Sin tabla base declarada | Modelo de consulta/configuración |
| [Student_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Student_m.php) | student | Sí |
| [Studentextend_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Studentextend_m.php) | studentextend | Sí |
| [Studentgroup_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Studentgroup_m.php) | studentgroup | Sí |
| [Studentrelation_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Studentrelation_m.php) | studentrelation | Sí |
| [Subject_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Subject_m.php) | subject | Sí |
| [Subjectattendance_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Subjectattendance_m.php) | sub_attendance | Sí |
| [Syllabus_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Syllabus_m.php) | syllabus | Sí |
| [Systemadmin_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Systemadmin_m.php) | systemadmin | Sí |
| [Tattendance_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Tattendance_m.php) | tattendance | Sí |
| [Teacher_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Teacher_m.php) | teacher | Sí |
| [Tmember_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Tmember_m.php) | tmember | Sí |
| [Transport_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Transport_m.php) | transport | Sí |
| [Uattendance_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Uattendance_m.php) | uattendance | **No** |
| [User_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/User_m.php) | user | Sí |
| [Usertype_m.php](C:/xampp/htdocs/sys/sys-escolar/mvc/models/Usertype_m.php) | usertype | Sí |

### 23.3 Discrepancias adicionales de dependencias

- Make_payment y Manage_salary cargan salary_template_m, salaryoption_m y hourly_template_m, ausentes del directorio de modelos.
- Report utiliza certificate_template_m en la generación de certificados, ausente de los modelos disponibles.
- Rutas y nombres heredados no prueban contratos de API, módulos activos o funcionalidades listas para familias.
- Hay modelos con referencias a columnas antiguas, como mark.mark, que no existe en el esquema actual; los puntajes están en markrelation.mark.

## 24. Anexo B: diccionario físico completo de la base local

**Base:** leadership. **Motor:** MariaDB 10.1.29. **Tablas:** 72. **Columnas:** 706. Todas las tablas observadas usan InnoDB. Conteos obtenidos con COUNT(*), no estimaciones. Este anexo describe el origen y no es un esquema recomendado para reproducir.

No hay FK ni restricciones UNIQUE de negocio declaradas en los metadatos consultados. PK señala clave primaria; MUL señala índice no único. Además de las PK, se observó el índice de timestamp de sesiones. Se muestran nombres y tipos de campos de credenciales porque forman parte del esquema, pero nunca sus valores.

### 24.1 Conteos exactos de todas las tablas

| Tabla | Filas | Motor |
|---|---:|---|
| alert | 77 | InnoDB |
| assignment | 4 | InnoDB |
| assignmentanswer | 0 | InnoDB |
| attendance | 0 | InnoDB |
| automation_rec | 340 | InnoDB |
| automation_shudulu | 64 | InnoDB |
| backup_comunicacion_b4_2026_mark | 23 | InnoDB |
| backup_comunicacion_b4_2026_markrelation | 460 | InnoDB |
| book | 1 | InnoDB |
| category | 0 | InnoDB |
| classes | 17 | InnoDB |
| conversations | 10 | InnoDB |
| conversation_msg | 0 | InnoDB |
| conversation_user | 46 | InnoDB |
| eattendance | 0 | InnoDB |
| event | 2 | InnoDB |
| eventcounter | 2 | InnoDB |
| exam | 4 | InnoDB |
| examschedule | 0 | InnoDB |
| expense | 0 | InnoDB |
| feetypes | 27 | InnoDB |
| grade | 0 | InnoDB |
| hmember | 0 | InnoDB |
| holiday | 1 | InnoDB |
| hostel | 0 | InnoDB |
| idmanager | 20 | InnoDB |
| ini_config | 3 | InnoDB |
| invoice | 564 | InnoDB |
| issue | 2 | InnoDB |
| leaveapp | 0 | InnoDB |
| lmember | 4 | InnoDB |
| loginlog | 3,304 | InnoDB |
| mailandsms | 0 | InnoDB |
| mailandsmstemplate | 0 | InnoDB |
| mailandsmstemplatetag | 0 | InnoDB |
| mark | 13,834 | InnoDB |
| markpercentage | 10 | InnoDB |
| markrelation | 138,830 | InnoDB |
| media | 2 | InnoDB |
| media_category | 5 | InnoDB |
| media_share | 0 | InnoDB |
| menu | 66 | InnoDB |
| message | 0 | InnoDB |
| migrations | 1 | InnoDB |
| notice | 5 | InnoDB |
| parents | 211 | InnoDB |
| payment | 10 | InnoDB |
| permissions | 179 | InnoDB |
| permission_relationships | 1,184 | InnoDB |
| ppp | 0 | InnoDB |
| promotionlog | 33 | InnoDB |
| reply_msg | 0 | InnoDB |
| reset | 0 | InnoDB |
| routine | 0 | InnoDB |
| schoolyear | 2 | InnoDB |
| school_sessions | 53 | InnoDB |
| section | 1 | InnoDB |
| setting | 33 | InnoDB |
| student | 261 | InnoDB |
| studentextend | 0 | InnoDB |
| studentgroup | 0 | InnoDB |
| studentrelation | 293 | InnoDB |
| subject | 225 | InnoDB |
| sub_attendance | 0 | InnoDB |
| syllabus | 5 | InnoDB |
| systemadmin | 1 | InnoDB |
| tattendance | 0 | InnoDB |
| teacher | 9 | InnoDB |
| tmember | 0 | InnoDB |
| transport | 0 | InnoDB |
| user | 0 | InnoDB |
| usertype | 8 | InnoDB |

### 24.2 Columnas, tipos y nullabilidad

La nullabilidad es física. NOT NULL no significa que el dato esté verificado o tenga significado correcto: el legado también usa cadenas vacías, 0 y fechas inválidas. Los nombres con significado reutilizado se explican en §4.4 y §16.3.

#### alert

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| alertID | int(11) unsigned | No | PRI |
| noticeID | int(128) | No | — |
| username | varchar(128) | No | — |
| usertype | varchar(128) | No | — |

#### assignment

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| assignmentID | int(11) | No | PRI |
| title | varchar(128) | No | — |
| description | text | No | — |
| deadlinedate | date | No | — |
| usertypeID | int(11) | No | — |
| userID | int(11) | No | — |
| originalfile | text | No | — |
| file | text | No | — |
| classesID | longtext | No | — |
| schoolyearID | int(11) | No | — |
| sectionID | longtext | Sí | — |
| subjectID | longtext | Sí | — |
| assignusertypeID | int(11) | Sí | — |
| assignuserID | int(11) | Sí | — |

#### assignmentanswer

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| assignmentanswerID | int(11) | No | PRI |
| assignmentID | int(11) | No | — |
| schoolyearID | int(11) | No | — |
| uploaderID | int(11) | No | — |
| uploadertypeID | int(11) | No | — |
| answerfile | text | No | — |
| answerfileoriginal | text | No | — |
| answerdate | date | No | — |

#### attendance

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| attendanceID | int(200) unsigned | No | PRI |
| schoolyearID | int(11) | No | — |
| studentID | int(11) | No | — |
| classesID | int(11) | No | — |
| sectionID | int(11) | No | — |
| userID | int(11) | No | — |
| usertype | varchar(20) | No | — |
| monthyear | varchar(10) | No | — |
| a1 | varchar(3) | Sí | — |
| a2 | varchar(3) | Sí | — |
| a3 | varchar(3) | Sí | — |
| a4 | varchar(3) | Sí | — |
| a5 | varchar(3) | Sí | — |
| a6 | varchar(3) | Sí | — |
| a7 | varchar(3) | Sí | — |
| a8 | varchar(3) | Sí | — |
| a9 | varchar(3) | Sí | — |
| a10 | varchar(3) | Sí | — |
| a11 | varchar(3) | Sí | — |
| a12 | varchar(3) | Sí | — |
| a13 | varchar(3) | Sí | — |
| a14 | varchar(3) | Sí | — |
| a15 | varchar(3) | Sí | — |
| a16 | varchar(3) | Sí | — |
| a17 | varchar(3) | Sí | — |
| a18 | varchar(3) | Sí | — |
| a19 | varchar(3) | Sí | — |
| a20 | varchar(3) | Sí | — |
| a21 | varchar(3) | Sí | — |
| a22 | varchar(3) | Sí | — |
| a23 | varchar(3) | Sí | — |
| a24 | varchar(3) | Sí | — |
| a25 | varchar(3) | Sí | — |
| a26 | varchar(3) | Sí | — |
| a27 | varchar(3) | Sí | — |
| a28 | varchar(3) | Sí | — |
| a29 | varchar(3) | Sí | — |
| a30 | varchar(3) | Sí | — |
| a31 | varchar(3) | Sí | — |

#### automation_rec

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| automation_recID | int(11) unsigned | No | PRI |
| studentID | int(11) | No | — |
| date | date | No | — |
| day | varchar(3) | No | — |
| month | varchar(3) | No | — |
| year | year(4) | No | — |
| nofmodule | int(11) | No | — |

#### automation_shudulu

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| automation_shuduluID | int(11) unsigned | No | PRI |
| date | date | No | — |
| day | varchar(3) | No | — |
| month | varchar(3) | No | — |
| year | year(4) | No | — |

#### backup_comunicacion_b4_2026_mark

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| markID | int(200) unsigned | No | PRI |
| schoolyearID | int(11) | No | — |
| examID | int(11) | No | — |
| exam | varchar(60) | No | — |
| studentID | int(11) | No | — |
| classesID | int(11) | No | — |
| subjectID | int(11) | No | — |
| subject | varchar(60) | No | — |
| year | year(4) | No | — |

#### backup_comunicacion_b4_2026_markrelation

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| markrelationID | int(11) unsigned | No | PRI |
| markID | int(11) | No | — |
| markpercentageID | int(11) | No | — |
| mark | varchar(128) | Sí | — |

#### book

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| bookID | int(11) unsigned | No | PRI |
| book | varchar(60) | No | — |
| subject_code | tinytext | Sí | — |
| author | varchar(100) | Sí | — |
| price | int(11) | Sí | — |
| quantity | int(11) | No | — |
| due_quantity | int(11) | No | — |
| rack | tinytext | Sí | — |

#### category

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| categoryID | int(11) unsigned | No | PRI |
| hostelID | int(11) | Sí | — |
| usertypeID | int(60) | Sí | — |
| teacherID | varchar(50) | Sí | — |
| note | int(50) | Sí | — |

#### classes

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| classesID | int(11) | No | PRI |
| classes | varchar(60) | No | — |
| classes_numeric | int(12) | Sí | — |
| teacherID | int(11) | Sí | — |
| studentmaxID | int(11) | Sí | — |
| note | text | Sí | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(40) | No | — |
| create_usertype | varchar(20) | No | — |

#### conversations

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| id | int(11) | No | PRI |
| status | int(11) | Sí | — |
| draft | int(11) | Sí | — |
| fav_status | int(11) | Sí | — |
| create_date | timestamp | No | — |
| modify_date | timestamp | No | — |

#### conversation_msg

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| msg_id | int(11) | No | PRI |
| conversation_id | int(11) | No | — |
| user_id | int(11) | No | — |
| subject | varchar(255) | Sí | — |
| msg | text | No | — |
| attach | text | Sí | — |
| attach_file_name | text | Sí | — |
| usertypeID | int(11) | No | — |
| create_date | timestamp | No | — |
| modify_date | timestamp | No | — |
| start | int(11) | Sí | — |

#### conversation_user

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| conversation_id | int(11) | No | — |
| user_id | int(11) | No | — |
| usertypeID | int(11) | No | — |
| is_sender | int(11) | Sí | — |
| trash | int(11) | Sí | — |

#### eattendance

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| eattendanceID | int(200) unsigned | No | PRI |
| schoolyearID | int(11) | No | — |
| examID | int(11) | No | — |
| classesID | int(11) | No | — |
| sectionID | int(11) | No | — |
| subjectID | int(11) | No | — |
| date | date | No | — |
| studentID | int(11) | Sí | — |
| s_name | varchar(60) | Sí | — |
| eattendance | varchar(20) | Sí | — |
| year | year(4) | No | — |
| eextra | varchar(60) | Sí | — |

#### event

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| eventID | int(11) unsigned | No | PRI |
| fdate | date | No | — |
| ftime | time | No | — |
| tdate | date | No | — |
| ttime | time | No | — |
| title | varchar(128) | No | — |
| details | text | No | — |
| photo | varchar(200) | Sí | — |
| create_date | timestamp | No | — |

#### eventcounter

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| eventcounterID | int(11) unsigned | No | PRI |
| eventID | int(11) | No | — |
| username | varchar(40) | No | — |
| type | varchar(20) | No | — |
| name | varchar(128) | No | — |
| photo | varchar(200) | Sí | — |
| status | int(11) | No | — |
| create_date | timestamp | No | — |

#### exam

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| examID | int(11) unsigned | No | PRI |
| exam | varchar(60) | No | — |
| date | date | No | — |
| note | text | Sí | — |

#### examschedule

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| examscheduleID | int(11) unsigned | No | PRI |
| examID | int(11) | No | — |
| classesID | int(11) | No | — |
| sectionID | int(11) | No | — |
| subjectID | int(11) | No | — |
| edate | date | No | — |
| examfrom | varchar(10) | No | — |
| examto | varchar(10) | No | — |
| room | tinytext | Sí | — |
| schoolyearID | int(11) | No | — |

#### expense

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| expenseID | int(11) unsigned | No | PRI |
| create_date | date | No | — |
| date | date | No | — |
| expenseday | varchar(11) | No | — |
| expensemonth | varchar(11) | No | — |
| expenseyear | year(4) | No | — |
| expense | varchar(128) | No | — |
| amount | double | No | — |
| userID | int(11) | No | — |
| usertypeID | int(11) | No | — |
| uname | varchar(40) | No | — |
| schoolyearID | int(11) | No | — |
| note | text | Sí | — |

#### feetypes

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| feetypesID | int(11) unsigned | No | PRI |
| feetypes | varchar(60) | No | — |
| note | text | Sí | — |

#### grade

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| gradeID | int(11) unsigned | No | PRI |
| grade | varchar(60) | No | — |
| point | varchar(11) | No | — |
| gradefrom | int(11) | No | — |
| gradeupto | int(11) | No | — |
| note | text | Sí | — |

#### hmember

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| hmemberID | int(11) unsigned | No | PRI |
| hostelID | int(11) | No | — |
| categoryID | int(11) | No | — |
| studentID | int(11) | No | — |
| hbalance | varchar(20) | Sí | — |
| hjoindate | date | No | — |

#### holiday

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| holidayID | int(11) unsigned | No | PRI |
| schoolyearID | int(11) | No | — |
| fdate | date | No | — |
| tdate | date | No | — |
| title | varchar(128) | No | — |
| details | text | No | — |
| photo | varchar(200) | Sí | — |
| create_date | timestamp | No | — |

#### hostel

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| hostelID | int(11) unsigned | No | PRI |
| name | varchar(128) | No | — |
| htype | varchar(11) | Sí | — |
| address | int(50) | No | — |
| note | text | Sí | — |

#### idmanager

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| idmanagerID | int(11) | No | PRI |
| schooltype | varchar(20) | No | — |
| idtitle | varchar(128) | No | — |
| idtype | varchar(128) | No | — |

#### ini_config

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| configID | int(11) unsigned | No | PRI |
| type | varchar(255) | No | — |
| config_key | varchar(255) | No | — |
| value | varchar(255) | No | — |

#### invoice

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| invoiceID | int(11) unsigned | No | PRI |
| schoolyearID | int(11) | No | — |
| classesID | int(11) | No | — |
| studentID | int(11) | No | — |
| feetypeID | int(11) | Sí | — |
| amount | double | No | — |
| discount | double | No | — |
| userID | int(11) | Sí | — |
| usertypeID | int(11) | Sí | — |
| uname | varchar(60) | Sí | — |
| date | date | No | — |
| create_date | date | No | — |
| day | varchar(20) | Sí | — |
| month | varchar(20) | Sí | — |
| year | year(4) | No | — |
| paidstatus | int(11) | Sí | — |
| deleted_at | int(11) | No | — |

#### issue

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| issueID | int(11) unsigned | No | PRI |
| lID | varchar(128) | No | — |
| bookID | int(11) | No | — |
| serial_no | varchar(40) | No | — |
| issue_date | date | No | — |
| due_date | date | No | — |
| return_date | date | Sí | — |
| note | text | Sí | — |

#### leaveapp

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| leaveID | int(11) unsigned | No | PRI |
| fdate | date | No | — |
| tdate | date | No | — |
| title | varchar(128) | No | — |
| details | text | No | — |
| touserID | int(11) | No | — |
| tousertypeID | int(11) | No | — |
| fromuserID | int(11) | No | — |
| fromusertypeID | int(11) | No | — |
| status | int(11) | No | — |
| create_date | timestamp | No | — |
| schoolyearID | int(11) | No | — |

#### lmember

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| lmemberID | int(11) unsigned | No | PRI |
| lID | varchar(40) | No | — |
| studentID | int(11) | No | — |
| name | varchar(60) | No | — |
| email | varchar(40) | Sí | — |
| phone | tinytext | Sí | — |
| lbalance | varchar(20) | Sí | — |
| ljoindate | date | No | — |

#### loginlog

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| loginlogID | int(11) | No | PRI |
| ip | varchar(45) | No | — |
| browser | varchar(128) | No | — |
| operatingsystem | varchar(128) | No | — |
| login | int(11) unsigned | No | — |
| logout | int(11) unsigned | Sí | — |
| usertypeID | int(11) | No | — |
| userID | int(11) | No | — |

#### mailandsms

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| mailandsmsID | int(11) unsigned | No | PRI |
| usertypeID | int(11) | No | — |
| users | text | No | — |
| type | varchar(10) | No | — |
| senderusertypeID | int(11) | No | — |
| senderID | int(11) | No | — |
| message | text | No | — |
| create_date | timestamp | No | — |
| year | year(4) | No | — |

#### mailandsmstemplate

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| mailandsmstemplateID | int(11) unsigned | No | PRI |
| name | varchar(128) | No | — |
| usertypeID | int(11) | No | — |
| type | varchar(10) | No | — |
| template | text | No | — |
| create_date | timestamp | No | — |

#### mailandsmstemplatetag

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| mailandsmstemplatetagID | int(11) unsigned | No | PRI |
| usertypeID | int(11) | No | — |
| tagname | varchar(128) | No | — |
| mailandsmstemplatetag_extra | varchar(255) | Sí | — |
| create_date | timestamp | No | — |

#### mark

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| markID | int(200) unsigned | No | PRI |
| schoolyearID | int(11) | No | — |
| examID | int(11) | No | — |
| exam | varchar(60) | No | — |
| studentID | int(11) | No | — |
| classesID | int(11) | No | — |
| subjectID | int(11) | No | — |
| subject | varchar(60) | No | — |
| year | year(4) | No | — |

#### markpercentage

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| markpercentageID | int(11) | No | PRI |
| markpercentagetype | varchar(100) | No | — |
| percentage | double | No | — |
| examID | int(11) | Sí | — |
| classesID | int(11) | Sí | — |
| subjectID | int(11) | Sí | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(40) | No | — |
| create_usertype | varchar(40) | No | — |

#### markrelation

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| markrelationID | int(11) unsigned | No | PRI |
| markID | int(11) | No | — |
| markpercentageID | int(11) | No | — |
| mark | varchar(128) | Sí | — |

#### media

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| mediaID | int(11) unsigned | No | PRI |
| userID | int(11) | No | — |
| usertypeID | int(11) | No | — |
| mcategoryID | int(11) | No | — |
| file_name | varchar(255) | No | — |
| file_name_display | varchar(255) | No | — |

#### media_category

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| mcategoryID | int(11) unsigned | No | PRI |
| userID | int(11) | No | — |
| usertypeID | int(11) | No | — |
| folder_name | varchar(255) | No | — |
| create_time | timestamp | No | — |

#### media_share

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| shareID | int(11) unsigned | No | PRI |
| classesID | int(11) | No | — |
| public | int(11) | No | — |
| file_or_folder | int(11) | No | — |
| item_id | int(11) | No | — |
| create_time | timestamp | No | — |

#### menu

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| menuID | int(11) | No | PRI |
| menuName | varchar(128) | No | — |
| link | varchar(512) | No | — |
| icon | varchar(128) | Sí | — |
| pullRight | text | Sí | — |
| status | int(11) | No | — |
| parentID | int(11) | No | — |
| priority | int(11) | No | — |

#### message

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| messageID | int(11) unsigned | No | PRI |
| email | varchar(128) | No | — |
| receiverID | int(11) | No | — |
| receiverType | varchar(20) | No | — |
| subject | varchar(255) | No | — |
| message | text | No | — |
| attach | text | Sí | — |
| attach_file_name | text | Sí | — |
| userID | int(11) | No | — |
| usertype | varchar(20) | No | — |
| useremail | varchar(40) | No | — |
| year | year(4) | No | — |
| date | date | No | — |
| create_date | timestamp | No | — |
| read_status | tinyint(1) | No | — |
| from_status | int(11) | No | — |
| to_status | int(11) | No | — |
| fav_status | tinyint(1) | No | — |
| fav_status_sent | tinyint(1) | No | — |
| reply_status | int(11) | No | — |

#### migrations

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| version | int(3) | No | — |

#### notice

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| noticeID | int(11) unsigned | No | PRI |
| title | varchar(128) | No | — |
| notice | text | No | — |
| schoolyearID | int(11) | No | — |
| date | date | No | — |
| create_date | timestamp | No | — |

#### parents

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| parentsID | int(11) unsigned | No | PRI |
| dni | varchar(60) | No | — |
| name | varchar(60) | Sí | — |
| father_name | varchar(60) | No | — |
| mother_name | varchar(60) | No | — |
| father_profession | varchar(40) | No | — |
| mother_profession | varchar(40) | No | — |
| email | varchar(40) | Sí | — |
| phone | tinytext | Sí | — |
| address | text | Sí | — |
| photo | varchar(200) | Sí | — |
| username | varchar(40) | No | — |
| password | varchar(128) | No | — |
| usertypeID | int(11) | No | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(150) | No | — |
| create_usertype | varchar(150) | No | — |
| active | int(11) | No | — |
| schoolyearID | int(11) | No | — |

#### payment

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| paymentID | int(11) unsigned | No | PRI |
| schoolyearID | int(11) | No | — |
| invoiceID | int(11) | Sí | — |
| studentID | int(11) | No | — |
| paymentamount | double | No | — |
| paymenttype | varchar(128) | No | — |
| paymentdate | date | No | — |
| paymentday | varchar(11) | No | — |
| paymentmonth | varchar(10) | No | — |
| paymentyear | year(4) | No | — |
| userID | int(11) | No | — |
| usertypeID | int(11) | No | — |
| uname | varchar(40) | No | — |
| transactionID | text | Sí | — |

#### permissions

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| permissionID | int(10) unsigned | No | PRI |
| description | varchar(255) | No | — |
| name | varchar(50) | No | — |
| active | enum('yes','no') | No | — |

#### permission_relationships

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| permission_id | int(11) | No | — |
| usertype_id | int(11) | No | — |

#### ppp

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| uno | int(11) | No | — |
| dos | int(11) | No | — |
| tres | int(11) | No | — |

#### promotionlog

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| promotionLogID | int(11) unsigned | No | PRI |
| promotionType | varchar(50) | Sí | — |
| classesID | int(11) | No | — |
| jumpClassID | int(11) | No | — |
| schoolYearID | int(11) | No | — |
| jumpSchoolYearID | int(11) | No | — |
| subjectandsubjectcodeandmark | longtext | Sí | — |
| exams | longtext | Sí | — |
| markpercentages | longtext | Sí | — |
| promoteStudents | longtext | Sí | — |
| status | int(11) | No | — |
| created_at | datetime | No | — |
| create_userID | int(11) | No | — |

#### reply_msg

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| replyID | int(11) unsigned | No | PRI |
| messageID | int(11) | No | — |
| reply_msg | text | No | — |
| status | int(11) | No | — |
| create_time | timestamp | No | — |

#### reset

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| resetID | int(11) unsigned | No | PRI |
| keyID | varchar(128) | No | — |
| email | varchar(60) | No | — |

#### routine

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| routineID | int(11) unsigned | No | PRI |
| classesID | int(11) | No | — |
| sectionID | int(11) | No | — |
| subjectID | int(11) | No | — |
| schoolyearID | int(11) | No | — |
| teacherID | int(11) | No | — |
| day | varchar(60) | No | — |
| start_time | varchar(10) | No | — |
| end_time | varchar(10) | No | — |
| room | tinytext | No | — |

#### schoolyear

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| schoolyearID | int(11) | No | PRI |
| schooltype | varchar(40) | Sí | — |
| schoolyear | varchar(128) | No | — |
| schoolyeartitle | varchar(128) | Sí | — |
| semestercode | int(11) | Sí | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(100) | No | — |
| create_usertype | varchar(100) | No | — |

#### school_sessions

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| id | varchar(40) | No | PRI |
| ip_address | varchar(45) | No | — |
| timestamp | int(10) unsigned | No | MUL |
| data | blob | No | — |

#### section

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| sectionID | int(11) unsigned | No | PRI |
| section | varchar(60) | No | — |
| category | varchar(128) | Sí | — |
| capacity | int(11) | Sí | — |
| classesID | int(11) | No | — |
| teacherID | int(11) | Sí | — |
| note | text | Sí | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(40) | No | — |
| create_usertype | varchar(200) | No | — |

#### setting

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| fieldoption | varchar(255) | No | PRI |
| value | varchar(255) | Sí | — |

#### student

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| studentID | int(11) unsigned | No | PRI |
| dni | text | No | — |
| name | varchar(60) | No | — |
| dob | date | Sí | — |
| sex | varchar(10) | No | — |
| email | varchar(40) | Sí | — |
| phone | tinytext | Sí | — |
| address | text | Sí | — |
| classesID | int(11) | No | — |
| sectionID | int(15) | No | — |
| bloodgroup | varchar(5) | Sí | — |
| bloodgroup2 | varchar(100) | Sí | — |
| country | varchar(128) | Sí | — |
| registerNO | varchar(128) | Sí | — |
| state | varchar(128) | Sí | — |
| library | int(11) | No | — |
| hostel | int(11) | No | — |
| transport | int(11) | No | — |
| photo | varchar(200) | Sí | — |
| parentID | int(11) | Sí | — |
| createschoolyearID | int(11) | No | — |
| schoolyearID | int(11) | No | — |
| username | varchar(40) | Sí | — |
| password | varchar(128) | Sí | — |
| usertypeID | int(11) | No | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(400) | No | — |
| create_usertype | varchar(400) | No | — |
| active | int(11) | No | — |

#### studentextend

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| studentID | int(11) | Sí | — |
| Columna 2 | int(11) | Sí | — |

#### studentgroup

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| studentgroupID | int(11) | Sí | — |

#### studentrelation

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| studentrelationID | int(11) | No | PRI |
| srstudentID | int(11) | Sí | — |
| srname | varchar(40) | No | — |
| srclassesID | int(11) | Sí | — |
| srclasses | varchar(400) | Sí | — |
| srroll | int(11) | Sí | — |
| srregisterNO | varchar(128) | Sí | — |
| srsectionID | int(11) | Sí | — |
| srsection | varchar(40) | Sí | — |
| srschoolyearID | int(11) | Sí | — |

#### subject

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| subjectID | int(11) unsigned | No | PRI |
| classesID | int(11) | No | — |
| teacherID | int(11) | No | — |
| type | int(100) | Sí | — |
| passmark | int(11) | Sí | — |
| finalmark | int(11) | Sí | — |
| subject | varchar(60) | No | — |
| subject_author | varchar(100) | Sí | — |
| subject_code | tinytext | Sí | — |
| teacher_name | varchar(60) | No | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(40) | No | — |
| create_usertype | varchar(20) | No | — |

#### sub_attendance

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| attendanceID | int(200) unsigned | No | PRI |
| schoolyearID | int(11) | No | — |
| studentID | int(11) | No | — |
| classesID | int(11) | No | — |
| sectionID | int(11) | No | — |
| subjectID | int(11) | No | — |
| userID | int(11) | No | — |
| usertype | varchar(20) | No | — |
| monthyear | varchar(10) | No | — |
| a1 | varchar(3) | Sí | — |
| a2 | varchar(3) | Sí | — |
| a3 | varchar(3) | Sí | — |
| a4 | varchar(3) | Sí | — |
| a5 | varchar(3) | Sí | — |
| a6 | varchar(3) | Sí | — |
| a7 | varchar(3) | Sí | — |
| a8 | varchar(3) | Sí | — |
| a9 | varchar(3) | Sí | — |
| a10 | varchar(3) | Sí | — |
| a11 | varchar(3) | Sí | — |
| a12 | varchar(3) | Sí | — |
| a13 | varchar(3) | Sí | — |
| a14 | varchar(3) | Sí | — |
| a15 | varchar(3) | Sí | — |
| a16 | varchar(3) | Sí | — |
| a17 | varchar(3) | Sí | — |
| a18 | varchar(3) | Sí | — |
| a19 | varchar(3) | Sí | — |
| a20 | varchar(3) | Sí | — |
| a21 | varchar(3) | Sí | — |
| a22 | varchar(3) | Sí | — |
| a23 | varchar(3) | Sí | — |
| a24 | varchar(3) | Sí | — |
| a25 | varchar(3) | Sí | — |
| a26 | varchar(3) | Sí | — |
| a27 | varchar(3) | Sí | — |
| a28 | varchar(3) | Sí | — |
| a29 | varchar(3) | Sí | — |
| a30 | varchar(3) | Sí | — |
| a31 | varchar(3) | Sí | — |

#### syllabus

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| syllabusID | int(11) | No | PRI |
| title | varchar(128) | No | — |
| description | text | Sí | — |
| date | date | No | — |
| usertypeID | int(11) | No | — |
| userID | int(11) | No | — |
| originalfile | text | No | — |
| file | text | Sí | — |
| classesID | int(11) | No | — |
| schoolyearID | int(11) | No | — |

#### systemadmin

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| systemadminID | int(11) unsigned | No | PRI |
| dni | varchar(60) | No | — |
| name | varchar(60) | No | — |
| dob | date | No | — |
| sex | varchar(10) | No | — |
| email | varchar(40) | Sí | — |
| phone | tinytext | Sí | — |
| address | text | Sí | — |
| jod | date | No | — |
| photo | varchar(200) | Sí | — |
| username | varchar(40) | No | — |
| password | varchar(128) | No | — |
| usertypeID | int(11) | No | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(40) | No | — |
| create_usertype | varchar(20) | No | — |
| active | int(11) | No | — |
| systemadminextra1 | varchar(128) | Sí | — |
| systemadminextra2 | varchar(128) | Sí | — |
| status | varchar(128) | Sí | — |

#### tattendance

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| tattendanceID | int(200) unsigned | No | PRI |
| schoolyearID | int(11) | No | — |
| teacherID | int(11) | No | — |
| usertypeID | int(11) | No | — |
| monthyear | varchar(10) | No | — |
| a1 | varchar(3) | Sí | — |
| a2 | varchar(3) | Sí | — |
| a3 | varchar(3) | Sí | — |
| a4 | varchar(3) | Sí | — |
| a5 | varchar(3) | Sí | — |
| a6 | varchar(3) | Sí | — |
| a7 | varchar(3) | Sí | — |
| a8 | varchar(3) | Sí | — |
| a9 | varchar(3) | Sí | — |
| a10 | varchar(3) | Sí | — |
| a11 | varchar(3) | Sí | — |
| a12 | varchar(3) | Sí | — |
| a13 | varchar(3) | Sí | — |
| a14 | varchar(3) | Sí | — |
| a15 | varchar(3) | Sí | — |
| a16 | varchar(3) | Sí | — |
| a17 | varchar(3) | Sí | — |
| a18 | varchar(3) | Sí | — |
| a19 | varchar(3) | Sí | — |
| a20 | varchar(3) | Sí | — |
| a21 | varchar(3) | Sí | — |
| a22 | varchar(3) | Sí | — |
| a23 | varchar(3) | Sí | — |
| a24 | varchar(3) | Sí | — |
| a25 | varchar(3) | Sí | — |
| a26 | varchar(3) | Sí | — |
| a27 | varchar(3) | Sí | — |
| a28 | varchar(3) | Sí | — |
| a29 | varchar(3) | Sí | — |
| a30 | varchar(3) | Sí | — |
| a31 | varchar(3) | Sí | — |

#### teacher

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| teacherID | int(11) unsigned | No | PRI |
| dni | varchar(50) | Sí | — |
| name | varchar(60) | No | — |
| designation | varchar(128) | Sí | — |
| dob | date | No | — |
| sex | varchar(10) | No | — |
| email | varchar(40) | Sí | — |
| phone | tinytext | Sí | — |
| address | text | Sí | — |
| jod | date | No | — |
| photo | varchar(200) | Sí | — |
| username | varchar(40) | No | — |
| password | varchar(128) | No | — |
| usertypeID | int(11) | No | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(40) | No | — |
| create_usertype | varchar(60) | No | — |
| active | int(11) | No | — |

#### tmember

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| tmemberID | int(11) unsigned | No | PRI |
| studentID | int(11) | No | — |
| transportID | int(11) | No | — |
| name | varchar(60) | No | — |
| email | varchar(40) | Sí | — |
| phone | tinytext | Sí | — |
| tbalance | varchar(11) | Sí | — |
| tjoindate | date | No | — |

#### transport

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| transportID | int(11) unsigned | No | PRI |
| route | text | No | — |
| vehicle | int(11) | No | — |
| fare | varchar(11) | No | — |
| note | text | Sí | — |

#### user

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| userID | int(11) unsigned | No | PRI |
| dni | varchar(60) | No | — |
| name | varchar(60) | No | — |
| dob | date | No | — |
| sex | varchar(10) | No | — |
| email | varchar(40) | Sí | — |
| phone | tinytext | Sí | — |
| address | text | Sí | — |
| jod | date | No | — |
| photo | varchar(200) | Sí | — |
| username | varchar(40) | No | — |
| password | varchar(128) | No | — |
| usertypeID | int(11) | No | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(40) | No | — |
| create_usertype | varchar(20) | No | — |
| active | int(11) | No | — |

#### usertype

| Columna | Tipo físico | Permite NULL | Clave/índice |
|---|---|---|---|
| usertypeID | int(11) unsigned | No | PRI |
| usertype | varchar(60) | No | — |
| create_date | datetime | No | — |
| modify_date | datetime | No | — |
| create_userID | int(11) | No | — |
| create_username | varchar(40) | No | — |
| create_usertype | varchar(20) | No | — |

## 25. Anexo C: consultas reproducibles de diagnóstico

Consultas de lectura para repetir verificaciones sobre una **copia autorizada** de leadership con cuenta restringida a SELECT/metadatos. Seleccionar previamente la base correcta en el cliente. No ejecutar scripts de mantenimiento para obtener estos resultados. Los valores pueden variar si cambia la copia.

### 25.1 Esquema y restricciones

~~~sql
SELECT VERSION() AS server_version;

SELECT COUNT(*) AS tables_observed
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE';

SELECT COUNT(*) AS columns_observed
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE();

SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_KEY
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME, ORDINAL_POSITION;

SELECT CONSTRAINT_TYPE, COUNT(*) AS total
FROM information_schema.TABLE_CONSTRAINTS
WHERE TABLE_SCHEMA = DATABASE()
GROUP BY CONSTRAINT_TYPE;

SELECT TABLE_NAME, INDEX_NAME, COLUMN_NAME, NON_UNIQUE
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;
~~~

### 25.2 Conteos de núcleo y relaciones por ciclo

~~~sql
SELECT 'student' AS entity_name, COUNT(*) AS rows_count FROM student
UNION ALL SELECT 'parents', COUNT(*) FROM parents
UNION ALL SELECT 'studentrelation', COUNT(*) FROM studentrelation
UNION ALL SELECT 'mark', COUNT(*) FROM mark
UNION ALL SELECT 'markrelation', COUNT(*) FROM markrelation
UNION ALL SELECT 'invoice', COUNT(*) FROM invoice
UNION ALL SELECT 'payment', COUNT(*) FROM payment
UNION ALL SELECT 'assignment', COUNT(*) FROM assignment
UNION ALL SELECT 'assignmentanswer', COUNT(*) FROM assignmentanswer;

SELECT s.schoolyearID,
       COUNT(*) AS student_rows,
       SUM(s.active = 1) AS active_rows,
       SUM(COALESCE(s.parentID, 0) = 0) AS no_parent_reference,
       SUM(s.parentID > 0 AND p.parentsID IS NULL) AS parent_missing,
       SUM(p.parentsID IS NOT NULL AND p.schoolyearID <> s.schoolyearID)
           AS parent_different_year,
       SUM(s.sectionID = 0) AS section_zero,
       SUM(NULLIF(TRIM(s.username), '') IS NOT NULL) AS with_username,
       SUM(NULLIF(TRIM(s.password), '') IS NOT NULL) AS with_password,
       SUM(s.dob IS NULL OR s.dob = '0000-00-00') AS missing_birthdate
FROM student s
LEFT JOIN parents p ON p.parentsID = s.parentID
GROUP BY s.schoolyearID;

SELECT p.schoolyearID,
       COUNT(*) AS parent_rows,
       SUM(NOT EXISTS (
           SELECT 1 FROM student s WHERE s.parentID = p.parentsID
       )) AS without_linked_students,
       SUM(NULLIF(TRIM(p.email), '') IS NULL) AS no_email,
       SUM(NULLIF(TRIM(p.phone), '') IS NULL) AS no_phone
FROM parents p
GROUP BY p.schoolyearID;
~~~

Estas consultas cuentan presencia de credenciales sin extraerlas. No agregar password, email, nombre, DPI o documentos a la salida compartida del diagnóstico.

### 25.3 Duplicados agregados de identidad

~~~sql
SELECT 'parent_username_same_year' AS check_name,
       COUNT(*) AS duplicated_groups,
       COALESCE(SUM(c - 1), 0) AS extra_rows
FROM (
    SELECT COUNT(*) AS c
    FROM parents
    WHERE NULLIF(TRIM(username), '') IS NOT NULL
    GROUP BY schoolyearID, username
    HAVING COUNT(*) > 1
) q
UNION ALL
SELECT 'student_personal_code_same_year', COUNT(*), COALESCE(SUM(c - 1), 0)
FROM (
    SELECT COUNT(*) AS c
    FROM student
    WHERE NULLIF(TRIM(dni), '') IS NOT NULL
    GROUP BY schoolyearID, dni
    HAVING COUNT(*) > 1
) q;

SELECT 'personal_code_multiple_years' AS check_name, COUNT(*) AS groups_count
FROM (
    SELECT dni FROM student
    WHERE NULLIF(TRIM(dni), '') IS NOT NULL
    GROUP BY dni HAVING COUNT(DISTINCT schoolyearID) > 1
) q
UNION ALL
SELECT 'balam_code_multiple_years', COUNT(*)
FROM (
    SELECT state FROM student
    WHERE NULLIF(TRIM(state), '') IS NOT NULL
    GROUP BY state HAVING COUNT(DISTINCT schoolyearID) > 1
) q
UNION ALL
SELECT 'personal_code_conflicting_birthdays', COUNT(*)
FROM (
    SELECT dni FROM student
    WHERE NULLIF(TRIM(dni), '') IS NOT NULL
      AND dob IS NOT NULL AND dob <> '0000-00-00'
    GROUP BY dni HAVING COUNT(DISTINCT dob) > 1
) q;
~~~

### 25.4 Integridad académica

~~~sql
SELECT 'mark_missing_student' AS check_name, COUNT(*) AS total
FROM mark m LEFT JOIN student s ON s.studentID = m.studentID
WHERE s.studentID IS NULL
UNION ALL
SELECT 'mark_missing_subject', COUNT(*)
FROM mark m LEFT JOIN subject s ON s.subjectID = m.subjectID
WHERE s.subjectID IS NULL
UNION ALL
SELECT 'relation_missing_mark', COUNT(*)
FROM markrelation r LEFT JOIN mark m ON m.markID = r.markID
WHERE m.markID IS NULL
UNION ALL
SELECT 'duplicate_mark_groups', COUNT(*)
FROM (
    SELECT COUNT(*) AS c FROM mark
    GROUP BY schoolyearID, studentID, subjectID, examID
    HAVING COUNT(*) > 1
) q
UNION ALL
SELECT 'duplicate_component_groups', COUNT(*)
FROM (
    SELECT COUNT(*) AS c FROM markrelation
    GROUP BY markID, markpercentageID HAVING COUNT(*) > 1
) q
UNION ALL
SELECT 'empty_component', COUNT(*) FROM markrelation
WHERE mark IS NULL OR TRIM(mark) = '';

SELECT 'student_missing_compatible_relation' AS check_name, COUNT(*) AS total
FROM student s
WHERE NOT EXISTS (
    SELECT 1 FROM studentrelation r
    WHERE r.srstudentID = s.studentID
      AND r.srclassesID = s.classesID
      AND r.srschoolyearID = s.schoolyearID
)
UNION ALL
SELECT 'relation_missing_student', COUNT(*)
FROM studentrelation r LEFT JOIN student s ON s.studentID = r.srstudentID
WHERE s.studentID IS NULL
UNION ALL
SELECT 'duplicate_historical_relation_groups', COUNT(*)
FROM (
    SELECT COUNT(*) AS c FROM studentrelation
    GROUP BY srstudentID, srclassesID, srschoolyearID
    HAVING COUNT(*) > 1
) q;

SELECT COUNT(*) AS components_with_conflicting_entered_values
FROM (
    SELECT markID, markpercentageID
    FROM markrelation
    WHERE NULLIF(TRIM(mark), '') IS NOT NULL
    GROUP BY markID, markpercentageID
    HAVING COUNT(DISTINCT TRIM(mark)) > 1
) q;
~~~

### 25.5 Referencias financieras y tareas

~~~sql
SELECT 'invoice_missing_student' AS check_name, COUNT(*) AS total
FROM invoice i LEFT JOIN student s ON s.studentID = i.studentID
WHERE s.studentID IS NULL
UNION ALL
SELECT 'payment_missing_invoice', COUNT(*)
FROM payment p LEFT JOIN invoice i ON i.invoiceID = p.invoiceID
WHERE i.invoiceID IS NULL
UNION ALL
SELECT 'payment_missing_student', COUNT(*)
FROM payment p LEFT JOIN student s ON s.studentID = p.studentID
WHERE s.studentID IS NULL
UNION ALL
SELECT 'assignment_missing_year', COUNT(*)
FROM assignment a LEFT JOIN schoolyear y ON y.schoolyearID = a.schoolyearID
WHERE y.schoolyearID IS NULL;

SELECT paymenttype, COUNT(*) AS payment_rows
FROM payment GROUP BY paymenttype;

SELECT COUNT(*) AS year_in_reused_bloodgroup_field
FROM student WHERE bloodgroup REGEXP '^20[0-9][0-9]$';

SELECT s.fieldoption AS missing_component_configuration
FROM setting s
LEFT JOIN markpercentage p
    ON p.markpercentageID = CAST(SUBSTRING(s.fieldoption, 6) AS UNSIGNED)
WHERE s.fieldoption LIKE 'mark_%' AND p.markpercentageID IS NULL;
~~~

Interpretar paymenttype como etiqueta guardada, no evidencia de un proveedor realmente utilizado. Las consultas anteriores no corrigen registros, no deciden qué persona consolidar y no habilitan cuentas. Los listados por fila necesarios para resolver incidencias se producen en una herramienta restringida de revisión y quedan fuera de este documento compartido.

