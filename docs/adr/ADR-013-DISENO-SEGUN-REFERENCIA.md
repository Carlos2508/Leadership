# ADR-013: diseño público y privado según las referencias del propietario

Fecha: 5 de noviembre de 2026, America/Guatemala. Estado: implementado para revisión local.

## Contexto

El propietario proporcionó dos composiciones de referencia (página pública y panel estudiantil), más el paquete local `Colegio_Leadership_HTML_CSS/Colegio_Leadership`, y pidió reproducir su diseño, colores e imágenes en toda la web. También autorizó redactar contenido de presentación, misión y visión para ampliar la página y dar destinos reales al menú. Esta solicitud sustituye la dirección visual Numix de ADR-010; conserva su navegación por módulos y los límites de autorización de ADR-011/012.

## Decisiones

- Se reconstruye la composición con componentes Angular y CSS adaptable: portada oscura, imagen con curva dorada, cuatro pilares y misión; encabezado y menú privados oscuros, selección dorada, superficies marfil, tarjetas, calendario, comunicados y acciones breves. Los formularios Razor comparten tipografía y paleta. No se usa la captura completa como interfaz rasterizada ni se incorpora el dashboard ficticio del kit como módulo.
- Inter variable normal y cursiva autoalojada, licencia SIL OFL. Los colores siguen en el único `config/institution.json`; CSS deriva sus tonos legibles. El dorado de texto grande sobre marfil se oscurece lo necesario para contraste AA; botones, curvas e imágenes conservan el acento vivo. `_identity.scss` se mantiene al final de `styles.scss`.
- Los recursos del paquete aportado se copian sin alterar a `wwwroot/branding`. Son ilustraciones, no evidencia de instalaciones ni estudiantes reales. Procedencia y licencias en `docs/reference/REFERENCE_DESIGN_ASSETS.md`. Los componentes no contienen nombres, contactos ni colores de cliente.
- Todo contenido institucional, incluidos los textos nuevos autorizados, vive en `PublicSiteOptions`/`InstitutionOptions` y `institution.json`. Misión/visión son redacción para esta solicitud de diseño; no se presentan en la documentación como declaraciones oficiales obtenidas de una fuente del colegio. No se inventan niveles, horarios, precios, requisitos obligatorios ni políticas académicas o legales.
- Inicio, Nosotros, Vida Escolar, Admisión, Comunidad y Contacto tienen secciones reales. Los fragmentos usan el router con desplazamiento bajo el encabezado fijo; el menú móvil se cierra al navegar. Contactos y redes tienen destinos configurados; el mapa solo se carga a solicitud. Privacidad y términos permiten consultar por correo, sin publicar una política ficticia.
- El panel adapta la composición a la información autorizada: el estudiante ve sus cursos, tareas y comunicados; el docente conserva cursos/entregas/asistencia; administración conserva cobros, matrícula, publicaciones y portería. El ciclo continúa explícito en URL. No se copia el promedio «85 %», el horario ni Recursos de muestra como funciones implementadas.
- El resumen incorpora cursos del estudiante obtenidos de su matrícula activa y colocación abierta en el ciclo seleccionado, después de `StudentAccessAsync` y facultad académica/acceso propio. Una facultad solo financiera no devuelve cursos ni tareas. Las tareas conservan `LearningService` como fuente autorizada. El resumen no devuelve calificaciones.
- El calendario navega meses, indica vencimientos reales, filtra por día y permite uso con teclado. Las fechas se calculan en la zona institucional. No crea eventos ficticios ni un calendario lectivo. Los comunicados conservan audiencia y estado de lectura.
- La sesión añade identificador estudiantil y descripción de perfil para presentación. El rol de acceso procede de SQL; esos textos no conceden permisos. Todas las APIs de negocio mantienen `ActiveAccount` y sus comprobaciones por permiso/recurso.

## Validación y límites

SQL Server real, pruebas de aislamiento de facultades, calendario y accesos; capturas y Axe en escritorio/móvil y 320/390/900/1440/1920 px. Resultados finales y ubicación de evidencias en `PROGRESS.md` y `HANDOFF.md`.

Sin migraciones, importación, modificación del PHP/Numix, despliegue ni cambios al lector Flutter. Las imágenes del kit no reemplazan la validación de material institucional para una publicación. Las funciones y políticas pendientes anteriores siguen pendientes.
