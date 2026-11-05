# Diseño de interfaz

Decisiones visuales de la plataforma. Los **colores institucionales** (`--deep`, `--primary`, `--accent`, `--surface`), logos y textos siguen saliendo solo de `config/institution.json`; este documento cubre lo que es propio de la plataforma y se mantiene igual para cualquier colegio.

## Identidad

- **Tipografía:** *Fraunces* (serif editorial con eje óptico) para títulos, cifras del panel, nombre en el lector de portería y alertas; *Figtree* para el texto. Autoalojadas en `wwwroot/fonts` (CSP `font-src 'self'`), licencia SIL OFL (`wwwroot/fonts/OFL-*.txt`). Se usan también en las páginas de ingreso (`auth.css`).
- **Firma visual:** cada etiqueta superior (eyebrow) lleva una barra corta en el color de acento; la opción activa del menú, una barra lateral de acento.
- **Superficies:** fondo con dos halos muy suaves de los colores institucionales; barra lateral con textura reticular casi imperceptible; tarjetas con sombra en dos capas y borde teñido del color primario.
- **Movimiento:** elevación leve al pasar sobre accesos y cifras, ícono que gira unos grados, botones que se hunden al presionar. Todo se desactiva con `prefers-reduced-motion`.
- **Personas:** avatares con dos iniciales en uno de cinco tonos derivados del nombre (estable para cada persona).
- **Estados vacíos:** ícono dentro de una forma orgánica teñida, no un glifo suelto.

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
