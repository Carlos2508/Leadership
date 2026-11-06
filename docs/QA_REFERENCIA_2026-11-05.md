# QA del rediseño según las referencias

5 de noviembre de 2026, America/Guatemala. Incremento local sobre `434a824`, sin despliegue ni migraciones.

## Resultado

Página pública, ingreso Razor y área privada con la composición solicitada: carbón/dorado, marfil, Inter, imágenes del paquete, curvas, marca compacta, cabecera y menú oscuros, indicadores y tarjetas. Los módulos existentes reciben el mismo sistema visual. ADR-013 sustituye la dirección visual anterior y conserva los contratos escolares y de autorización.

La portada se amplía con Nosotros/misión/visión, Vida Escolar, Admisión, Comunidad, preguntas y Contacto. Se recorrieron todos los enlaces del menú; se cierra el menú móvil y el destino queda visible bajo la cabecera. Contactos, redes y ubicación tienen destinos configurados. El mapa no se carga antes de solicitarlo; no aparecen contactos vacíos ni llamadas a un prefijo sin número.

El panel estudiantil consulta sus cursos, tareas, actividades y comunicados reales. El calendario navega meses y días con ratón/teclado, señala vencimientos y filtra actividades. Las acciones llevan al seguimiento autorizado. Administración, docentes y familias conservan indicadores y acciones según su contexto. No se copian un promedio general, horario o módulo de Recursos ficticios para completar la imagen.

## Verificación

| Comprobación | Resultado |
| --- | --- |
| Build Angular y copia/precompresión | Correctos; advertencia conocida SweetAlert2 CommonJS |
| `dotnet test --no-restore`, SQL Server real | **89 aprobadas, 0 omitidas** |
| Playwright completo, 82 casos | **81 aprobados, 1 omisión prevista de rendimiento móvil, 0 fallos** |
| Playwright tras el último ajuste visual | **18 adicionales aprobados, 0 fallos** |
| Axe WCAG A/AA, escritorio y móvil | Sin violaciones en los recorridos comprobados |
| Portada y panel estudiantil | 320, 390, 900, 1440 y 1920 px, sin desbordamiento horizontal |
| Directorios y dashboard operativo | También 768/1024 px según sus recorridos existentes |
| Originales frente a las cuatro imágenes copiadas | SHA-256 idéntico en cada par |
| `git diff --check` | Correcto |
| SQL final, catálogos `SchoolPlatform_Test_*` / `SchoolPlatform_E2E_*` | **0 temporales residuales** |
| Host local final | `/`, `/health/live` y `/health/ready`: **200** |

Las seis nuevas ejecuciones de `reference-design.spec.ts` cubren configuración pública vacía, navegación pública y panel estudiantil real en escritorio/móvil. Se preparan cursos, matrícula, actividades y comunicados mediante las APIs dentro de la base exclusiva del harness; no se reutiliza la base escolar. Las contraseñas/códigos de ese recorrido viven en memoria y sus trazas están desactivadas. También se desactivaron trazas de los recorridos de activación/recuperación y comunicados.

Las pruebas SQL del dashboard comprueban curso/identidad propia y familiar, ausencia de datos académicos para facultad solo financiera y retiro de cursos/tareas al revocar la facultad. Las notas siguen fuera del resumen y detrás de `GradeService.MyGradesAsync` y sus cortes de solvencia.

La suite completa vuelve a recorrer cuentas, roles, personas/familias, matrícula, cursos, asistencia y cambios no guardados, captura/publicación/solvencia de notas, tareas/entregas, comunicados, caja/recibos/reversión/ajustes, ciclos financieros y errores/respuestas tardías, MFA, carnets/portería/dispositivos, recuperación de sesión, rendimiento y límites anónimos.

## Ajustes encontrados en la revisión

- Consulta de cursos: ordenar la proyección de un record no era traducible por EF; se corrigió el ordenamiento antes de proyectar y se validó con SQL real.
- Los enlaces públicos se integran con el router y un desplazamiento calculado desde la cabecera, para no ocultar títulos ni perder el destino al cambiar de fragmento.
- Inicio permanece seleccionado aunque el ciclo esté en query; Académico/Tareas/Calificaciones/Asistencia conservan el ciclo al navegar por fragmentos.
- La tarjeta no inventa cero cursos cuando no hay una matrícula disponible. Las facultades exclusivamente financieras no aparentan acceso a tareas.
- Contactos, imágenes, textos y secciones opcionales vacíos se ocultan. No se emiten `mailto:` vacíos ni una acción de admisión hacia un prefijo telefónico solo.
- La revisión de capturas detectó texto blanco sobre la curva dorada en menús largos y el pie del ingreso. Se dio fondo oscuro al área de navegación/cuenta y se elevó el pie de ingreso; se repitieron 18 pruebas y se revisaron sus capturas.
- Las pruebas existentes se adaptaron al saludo y al bloque de comunicados nuevos. El selector de Administración se restringe a navegación, pues el perfil de la cabecera puede tener el mismo nombre.

## Evidencias y continuidad

Carpeta preservada, ignorada por Git: `.local/qa/evidencia/reference-redesign-2026-11-05/`.

- `complete-suite/`: 159 PNG de la corrida completa.
- `final-visual/`: 58 PNG posteriores al ajuste final.
- `portada-escritorio.png`, `panel-estudiantil-escritorio.png`, `portada-movil.png` y `panel-estudiantil-movil.png`: copias de acceso directo a las vistas comparativas.

Se revisaron capturas de portada, panel, ingreso, menú largo, listados/formularios y tamaños pequeños. Los ejemplos de Carlos y sus cifras son datos de QA aislada; las capturas no representan registros de la instalación. La fecha, cifras y accesos del producto siguen a cada cuenta/ciclo, no a los valores del raster de referencia. El dorado de texto grande se ajusta a contraste AA. Misión/visión son redacción autorizada en configuración; no políticas oficiales investigadas.

Versión final disponible en `http://localhost:5082`, lanzador 3372 y metadatos en `.local/qa/reference-design-host.json`; logs `reference-design-5082.out.log` / `.err.log`. Se comprobó que sirve el CSS final `styles-DM44YVEV.css` y la paleta configurada del paquete. No reconstruir el host con Playwright abierto.

Sin commit/push de este incremento ni despliegue. Se preservaron los datos de desarrollo y no se modificó PHP/MariaDB/Numix. Flutter no cambió; siguen pendientes anteriores de políticas, publicación, licencias/operación y dispositivos físicos. Procedencia de imágenes y fuentes en `reference/REFERENCE_DESIGN_ASSETS.md`.
