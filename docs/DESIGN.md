# Diseño de interfaz

Decisiones visuales de la plataforma. Los **colores institucionales** (`--deep`, `--primary`, `--accent`, `--surface`), logos y textos siguen saliendo solo de `config/institution.json`; este documento cubre lo que es propio de la plataforma y se mantiene igual para cualquier colegio.

## Identidad

- **Tipografía:** *Inter* variable, normal y cursiva, en la web y el acceso Razor. Autoalojada en `wwwroot/fonts` (CSP `font-src 'self'`), licencia SIL OFL conservada. Títulos sin serif, texto habitual de 14–15 px y cifras tabulares. Las fuentes históricas se conservan para no alterar documentos existentes, pero la web ya no utiliza Figtree/Fraunces.
- **Firma visual:** referencia pública/estudiantil aportada por el propietario, ADR-013. Menú y encabezado oscuros, selección con cápsula dorada, curvas en portada y base lateral, una marca por cabecera/menú y un birrete decorativo discreto.
- **Superficies:** marfil institucional y paneles blancos con borde fino, sombra suave y radios de 14–22 px. Botones principales dorados con texto oscuro; acciones destructivas rojas. Sitio público, ingreso/activación y espacio privado ocupan todo el ancho y alto de la ventana (sin marco exterior, margen ni esquinas redondeadas); la barra lateral va fija a toda la altura en escritorio. Las tarjetas quedan solo dentro del contenido. Activación/recuperación y 2FA no repiten el logo sobre los campos: ya está en el panel lateral.
- **Movimiento:** solo transiciones breves de color y foco. Respetar `prefers-reduced-motion`.
- **Personas:** avatares con dos iniciales en uno de cinco tonos derivados del nombre (estable para cada persona).
- **Estados vacíos:** ícono pequeño en un cuadrado de esquinas suaves, explicación concreta y siguiente paso.

## Navegación y página pública

Referencia vigente: capturas pública/estudiantil y paquete HTML/CSS proporcionados por el propietario. Sustituye la dirección visual Numix de ADR-010, conservando la claridad de módulos, los permisos y la información operativa. Ver ADR-013.

- Menú con Estudiantes, Padres y Docentes como entradas independientes, además de Inscripciones, Cursos, Calificaciones y Ciclo escolar. Portería, Finanzas y Administración conservan sus permisos. «Mi cuenta y seguridad» es una utilidad separada.
- Directorios `/workspace/students`, `/workspace/parents`, `/workspace/teachers`: mismo registro de personas, filtros de servidor `student`, `guardian`, `staff`, alta con perfil preseleccionado y pestaña «Todos» para encontrar fichas existentes. Docentes incluye al personal y lo indica en el título; no convierte todo el personal en docente asignado ni concede cursos por el nombre del cargo. Las asignaciones siguen en Cursos.
- El registro de personal permite indicar cargo al crear la ficha, con validación, perfil y auditoría confirmados en el mismo `SaveChanges`. No crea cuentas ni vínculos ni matrícula.
- Una marca en la cabecera pública y una en el menú del área privada. Sin escudo gigante en el hero ni segundo logo en el formulario de ingreso. Documentos y carnets conservan sus marcas propias configuradas.
- Página pública con portada e imágenes del paquete, pilares, misión/visión, vida escolar, admisión, comunidad, preguntas y contacto. Todos los enlaces del menú tienen destinos reales. La redacción pública nueva fue autorizada por el propietario y está en configuración; no constituye una política ni una oferta/estadística verificada. Los campos vacíos se ocultan. Mapa bajo solicitud y sin instrucciones técnicas para familias.
- Diseño fluido hasta 1680 px de contenido, menú móvil con foco/Escape/fondo inerte, tablas adaptadas y formularios utilizables desde 320 px.

Capa: `web/src/styles/_identity.scss` (se carga al final).

## Inicio y roles

El dashboard muestra indicadores, pendientes y acciones por contexto. La composición de referencia usa cuatro indicadores personales, actividades/calendario y comunicados/acciones breves; conserva el resumen operativo de administración y la docencia. Mis clases proviene de la matrícula, no de la muestra; no se inventa un promedio general. El ciclo está visible y queda en la URL; portería identifica su alcance de hoy para todos los ciclos. Cero, sin configuración y fallo de carga tienen significados diferentes. Las cifras monetarias usan aplicaciones confirmadas y nunca se confunden con solvencia. Ver ADR-011/013.

El editor de roles usa acciones agrupadas por área, selección completa opcional, motivo y confirmación del número de cuentas afectadas. No deduce permisos del nombre: Director y Secretaría pueden tener todos. Los perfiles escolares se mantienen separados. Ver ADR-012.

- Menú lateral: los módulos van sin fondo propio sobre el degradado oscuro (sin bloques opacos); solo el activo lleva el tratamiento dorado (degradado translúcido, borde, brillo y marca lateral). La lista se desplaza sola con desvanecido inferior y «Mi cuenta/Cerrar sesión» forman un pie translúcido con desenfoque, siempre visible sobre la curva dorada. El birrete decorativo queda al 8 % de opacidad. Colores solo desde `--deep`/`--accent`.
- Campana del encabezado (`core/notifications.ts`): contador con avisos sin leer y pendientes por rol, panel desplegable (Escape/clic fuera cierra; en teléfono fijo bajo la barra). Los pendientes repiten exactamente las reglas de «Requiere atención» del panel y dejan de contar al verse hasta que cambie su cifra (por cuenta, en `localStorage`); los avisos siguen contando hasta abrirse. Abrir un aviso desde la campana lleva a `/workspace/announcements?open=<id>` y lo marca leído. El app conectado muestra la misma campana con los avisos sin leer.
- «Primeros pasos» (`features/setup-guide.ts`): en un colegio nuevo, Inicio muestra los pasos en orden con su botón; el siguiente paso va en dorado. Se oculta sola cuando el colegio ya tiene inscripciones y cargos, o con «Ocultar esta guía».
- Menú personal: el docente ve el grupo «Docencia» con sus cursos asignados (máximo seis) y la familia «Mis estudiantes» con cada vínculo vigente. Se cargan de `/api/v1/me/courses` y `/api/v1/me`; son atajos de presentación, la autorización sigue en el servidor.

- Encabezado: solo nombre y perfil escolar, sin menú desplegable. «Mi cuenta» y «Cerrar sesión» viven únicamente en el pie del menú lateral; no hay enlace al sitio público dentro del espacio privado. El menú usa una sola columna del ancho de la barra y los nombres largos terminan en elipsis.
- Estudiante y familia: páginas propias de Resumen, Tareas, Calificaciones y Asistencia con las mismas pestañas (`shared/student-header.ts`). Los cursos llevan uno de seis tonos estables derivados del nombre, con contraste AA. Calendarios mensuales con lunes primero y días de otros meses punteados. Las calificaciones no muestran promedios. ADR-022.
- Libreta del docente: cuadrícula de actividades con punteo y total en vivo (color relativo a los puntos asignados). El guardado automático se informa junto al botón y no cambia el texto del botón.

## Formularios y diálogos

Capas finales `web/src/styles/_forms.scss` (controles) y `_polish.scss` (ajustes de página tras la revisión visual completa). Se cargan después de `_identity.scss`.

- **Campos:** borde neutro cálido (`--field-border`), radio 10 px, valor en peso regular y etiqueta en 600. Foco con borde y anillo del acento. Los `select` llevan la misma flecha en toda la plataforma.
- **Casillas y opciones:** dibujadas por la plataforma, no las del navegador. Seleccionadas en el acento con marca oscura. Sus reglas usan `:where()` para no pisar controles ocultos a propósito (asistencia, segmentos).
- **Interruptor:** `<input type="checkbox" class="switch" role="switch">` para activar o desactivar una opción completa.
- **Tarjetas de opción** (`.option-list` > `.option-card`): ícono, título, descripción y un interruptor a la derecha. Las subopciones usan `.sub` y los detalles `.option-detail`. Se usan para atajos como preparar un ciclo o copiar del anterior, en lugar de casillas sueltas dentro de un recuadro con título en el borde.
- **Control segmentado** (`.segmented`): opciones cortas y excluyentes, por ejemplo la cantidad de bimestres. No usar una lista desplegable para elegir entre 2 y 6.
- **Línea de tiempo** (`.timeline`): vista previa de períodos con una barra proporcional dentro del ciclo.
- **Grupos** (`.choice-group`): el título va como etiqueta encima de las opciones, sin el recuadro con título en el borde.
- **Diálogos:** encabezado fijo, cuerpo con desplazamiento propio y botones fijos abajo con línea divisoria. Fondo oscuro con desenfoque. En el teléfono se abren como hoja inferior. `.access-dialog` (portería y carnet) tiene el mismo aspecto.
- **Botones pequeños:** dentro de tablas, listas y paginación miden 36 px, igual que `.compact-button`.
- **Pestañas y selección:** la pestaña activa usa el acento, igual que el menú. El verde se reserva para estados «listo» o «correcto». Las pestañas de módulo se desplazan de lado en el teléfono.
- No quedan grises verdosos del diseño anterior: bordes, fondos y sombras usan neutros cálidos.

En el app (Flutter), `app_theme.dart` define interruptores, casillas, opciones, fechas y chips en el mismo dorado. `academic_page.dart` usa `_OptionGroup`/`_OptionTile` (tarjetas de opción), un `SegmentedButton` para la cantidad y una línea de tiempo. `SectionChips` se desplaza de lado y nunca deja una opción sola en una segunda fila.

## Alertas (SweetAlert2)

Servicio `core/alerts.ts`, con los mismos botones, fuentes y colores que la plataforma (`buttonsStyling: false`).

| Uso | Método | Comportamiento |
| --- | --- | --- |
| Acción completada | `success`, `info` | Toast arriba a la derecha, 4 s, se pausa al pasar el mouse, cerrable, `role=status`; no quita el foco |
| Acción que falló | `error` | Modal con el motivo del servidor y «Entendido» |
| Acción sensible de un clic | `confirm` | Modal con ícono de pregunta o advertencia; botón rojo si es destructiva |
| Confirmación con motivo | `reason` | Modal con área de texto validada (mínimo de caracteres) |
| Completada con siguiente paso | `done` | Ej. «Pago registrado» → «Ver e imprimir recibo» |

Reglas:

- Los formularios con datos (motivos de anulación, pagos, correcciones) siguen en sus diálogos; sus errores se muestran dentro del diálogo, junto a los campos. La alerta no sustituye la validación.
- Si hay un `<dialog>` nativo abierto, la alerta se monta dentro de él para quedar en la capa superior y ser usable.
- Confirman antes de ejecutar: publicar/archivar avisos, publicar/cerrar actividades, cambiar ciclo predeterminado, quitar período o sección, eliminar foto, quitar archivo de una entrega y desactivar la verificación en dos pasos.
- Los errores al cargar una pantalla se muestran en la propia página (con reintento), no como alerta.
- Paquete fijado `sweetalert2@11.26.25` (MIT); se revisó que la versión no incluya código condicionado por dominio o idioma. Revisar de nuevo antes de actualizarlo.

## Accesibilidad

- Contraste AA verificado por Axe en todas las pruebas E2E (escritorio y móvil). El ámbar de «lectura repetida» usa `#8d570d`.
- Foco visible (`outline` naranja de 3 px) conservado; los anillos de foco de los campos son adicionales.
- Los toasts se anuncian como estado; los modales atrapan el foco y lo devuelven al cerrar.


## Documentos PDF (recibos, boletines, libreta y carnets)

Generados en el servidor con QuestPDF (`Documents/PdfDocuments.cs`). Sistema visual propio de los documentos, independiente del color de la interfaz:

- **Banda superior grafito** (`#1B2025`) con el logo en una insignia blanca circular (funciona con cualquier logo), el nombre de la institución, una etiqueta en el color de acento y un título con una palabra destacada en acento («Boletín de *calificaciones*», «Recibo *No. 000123*»). Arcos decorativos del acento al fondo y una línea de acento debajo.
- **Franja de datos** en tono cálido (`#FBF8F1`) con etiquetas en acento oscuro y valores grandes.
- **Títulos de sección** con barra vertical de acento; **tablas** con encabezado grafito, filas alternas, y la columna principal destacada en acento.
- **Banda inferior grafito** con el nombre corto y el lema del colegio, el nombre del documento, la fecha de generación y la paginación.
- Tipografía **Figtree** estática (400–900) incrustada en el ensamblado (`Documents/Fonts`, licencia OFL); no depende de fuentes del servidor.
- El **acento** sale de `Institution:Theme:Accent`; los documentos no usan el color primario, de modo que un cambio de paleta del sitio no los descompone.

Contenido:

- **Recibo:** número con 6 dígitos, estado («PAGO CONFIRMADO» o «ANULADO» con marca de agua), detalle por concepto y estudiante, total destacado y **cantidad en letras** (quetzales, dólares y otras monedas comunes), contacto, línea de firma (con el usuario emisor solo en la copia interna) y la nota «Comprobante interno de pago. No es una factura fiscal».
- **Boletín:** horizontal, materias × períodos publicados con el último disponible resaltado; «Pendiente», «Exonerado» y «Retenido» se escriben, no se colorean como nota. **Sin promedios ni redondeos**. Los colores aprobado/por reforzar aparecen **solo** si el colegio configura `Institution:Documents:PassingScore` (vacío por defecto).
- **Libreta del curso:** lista con situación y nota del período y línea de firma docente.
- **Carnet CR80 (estudiante y personal, ADR-021):**
  - Frente: cabecera grafito con escudo, antetítulo/`ShortName`/sede y «CARNÉ año»; filete de acento; cuerpo papel claro con aros tenues; foto o iniciales en marco blanco con marcas de esquina de acento; «IDENTIFICACIÓN ESTUDIANTIL/DE PERSONAL»; nombres y apellidos en dos líneas que se reducen para caber; grado y sección o puesto; chip grafito con código o ID; QR en recuadro blanco.
  - Pie: grafito con el lema (estudiante) o de acento con «PERSONAL DEL COLEGIO» (personal), y la vigencia.
  - Reverso: cabecera con nombre y etiqueta del tipo; condiciones numeradas; aviso de cada tipo; banda grafito con contacto de emergencia, dirección y Code 128 en un recuadro blanco.

Pruebas: `DocumentDesignTests` genera documentos con datos sintéticos (cantidad en letras, copias familiar/interna, boletín con y sin nota mínima, retenido, libreta y carnets). Con `SCHOOLPLATFORM_PDF_DUMP=<carpeta>` guarda copias para revisión visual.
