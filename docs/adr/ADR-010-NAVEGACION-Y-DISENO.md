# ADR-010: navegación escolar y diseño de interfaz

Fecha: 5 de noviembre de 2026, America/Guatemala. Estado: implementado para revisión local.

## Contexto

El propietario rechazó el diseño anterior y pidió una experiencia sencilla y consistente, comparable con Numix, con módulos reconocibles para usuarios en Guatemala. Se revisaron el sitio público, dashboard y listado de productos de Numix y su código local en modo lectura. El usuario resolvió personalmente el aviso inicial de 2FA; no se cambió la seguridad de aquella aplicación.

## Decisiones

- Estudiantes, Padres y Docentes tienen entradas y rutas propias. Comparten el modelo de personas de ADR-004; no se duplican registros ni se fusionan cuenta, vínculo o matrícula. La pestaña Todos permite localizar una persona antes de agregar otro perfil.
- Cada listado filtra en SQL mediante el contrato existente (`student`, `guardian`, `staff`) y preselecciona el perfil del alta. El módulo Docentes indica «Docentes y personal»: ser personal no concede una asignación docente; esta se administra en Cursos. El nuevo campo opcional `StaffPosition` de alta valida cargo y confirma persona, perfil y auditoría en un solo guardado. No requiere migración ni crea accesos.
- El menú ofrece módulos escolares directos y agrupa Portería y Administración en secciones desplegables, abiertas cuando contienen la ruta actual. Los permisos de API y guards no se sustituyen por la navegación.
- Se adopta Figtree para toda la interfaz, superficies claras, paneles blancos, menú sólido, encabezado discreto y jerarquía de acciones. La capa `_identity.scss` se carga al final; `docs/DESIGN.md` registra tamaños, superficies y alertas. Los colores siguen procediendo de `InstitutionOptions` y `config/institution.json`.
- Una marca en la cabecera pública y en el menú privado. Se retiran el escudo gigante del hero, el logo duplicado del ingreso y los adornos que compiten con las acciones. Los PDFs y carnets conservan sus marcas documentales.
- La página pública utiliza contenido institucional configurado, servicios reales, contacto y mapa voluntario. No se inventa oferta educativa, horarios o matrícula. Se retiran instrucciones técnicas de configuración del contenido público.
- En las pruebas de cobros se utiliza la fecha civil de la institución en lugar del día UTC: después de las 18:00 en Guatemala no debe emitirse una fecha futura para el servidor.

## Límites

El cambio web no rediseña ni integra el prototipo Flutter general. No aprueba políticas académicas, licencia de documentos, uso de datos reales o despliegue. Las muestras siguen restringidas a Development. El estado de validación y las evidencias están en `PROGRESS.md` y `HANDOFF.md`.
