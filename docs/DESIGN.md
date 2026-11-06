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
- Menú personal: el docente ve el grupo «Docencia» con sus cursos asignados (máximo seis) y la familia «Mis estudiantes» con cada vínculo vigente. Se cargan de `/api/v1/me/courses` y `/api/v1/me`; son atajos de presentación, la autorización sigue en el servidor.

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
- **Carnet CR80:** frente con banda grafito, foto o iniciales con marco de acento, código del estudiante, QR y banda inferior con el lema y la vigencia; reverso con aviso, normas, teléfono de emergencia, dirección y código de barras dentro de un recuadro.

Pruebas: `DocumentDesignTests` genera documentos con datos sintéticos (cantidad en letras, copias familiar/interna, boletín con y sin nota mínima, retenido, libreta y carnets). Con `SCHOOLPLATFORM_PDF_DUMP=<carpeta>` guarda copias para revisión visual.
