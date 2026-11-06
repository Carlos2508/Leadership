# Diseño de interfaz

Decisiones visuales de la plataforma. Los **colores institucionales** (`--deep`, `--primary`, `--accent`, `--surface`), logos y textos siguen saliendo solo de `config/institution.json`; este documento cubre lo que es propio de la plataforma y se mantiene igual para cualquier colegio.

## Identidad

- **Tipografía:** *Figtree* en toda la interfaz, títulos, cifras y acceso. Autoalojada en `wwwroot/fonts` (CSP `font-src 'self'`), licencia SIL OFL. Títulos sin serif, texto habitual de 14–15 px y cifras tabulares. Fraunces deja de utilizarse en la interfaz.
- **Firma visual:** selección del menú con relleno primario y marca lateral de acento. Sin barras decorativas repetidas, texturas ni íconos que giran.
- **Superficies:** fondo institucional claro, barra lateral sólida, encabezado blanco y paneles blancos con borde fino. Sombras para diálogos, alertas y el bloque de acceso público; los listados y módulos no flotan. Radios de 8–14 px.
- **Movimiento:** solo transiciones breves de color y foco. Respetar `prefers-reduced-motion`.
- **Personas:** avatares con dos iniciales en uno de cinco tonos derivados del nombre (estable para cada persona).
- **Estados vacíos:** ícono pequeño en un cuadrado de esquinas suaves, explicación concreta y siguiente paso.

## Navegación y página pública

Referencia visual revisada: página pública, dashboard y listado de productos de Numix, más su código local (solo lectura). Se adopta su claridad de módulos y jerarquía; la marca y los colores siguen siendo los de esta instalación.

- Menú con Estudiantes, Padres y Docentes como entradas independientes, además de Inscripciones, Cursos, Calificaciones y Ciclo escolar. Portería, Finanzas y Administración conservan sus permisos. «Mi cuenta y seguridad» es una utilidad separada.
- Directorios `/workspace/students`, `/workspace/parents`, `/workspace/teachers`: mismo registro de personas, filtros de servidor `student`, `guardian`, `staff`, alta con perfil preseleccionado y pestaña «Todos» para encontrar fichas existentes. Docentes incluye al personal y lo indica en el título; no convierte todo el personal en docente asignado ni concede cursos por el nombre del cargo. Las asignaciones siguen en Cursos.
- El registro de personal permite indicar cargo al crear la ficha, con validación, perfil y auditoría confirmados en el mismo `SaveChanges`. No crea cuentas ni vínculos ni matrícula.
- Una marca en la cabecera pública y una en el menú del área privada. Sin escudo gigante en el hero ni segundo logo en el formulario de ingreso. Documentos y carnets conservan sus marcas propias configuradas.
- Página pública con presentación institucional configurada, bloque de información del portal, servicios, contacto y mapa bajo solicitud. No inventar oferta educativa, horarios o estadísticas; los campos vacíos se ocultan. No mostrar instrucciones técnicas de configuración a las familias.
- Diseño fluido hasta 1680 px de contenido, menú móvil con foco/Escape/fondo inerte, tablas adaptadas y formularios utilizables desde 320 px.

Capa: `web/src/styles/_identity.scss` (se carga al final).

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
