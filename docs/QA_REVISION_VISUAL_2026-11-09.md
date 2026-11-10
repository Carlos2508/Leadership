# Revisión visual y de experiencia — 9 de noviembre de 2026

Revisión sobre `main` `1d45066`, con correcciones en `codex/qa-visual-experiencia`. Se usaron exclusivamente cuentas y bases sintéticas. La revisión no modificó el legado ni la base escolar.

## Hallazgos corregidos

1. **Identificar al hijo:** Tareas, Calificaciones y Asistencia mostraban el título de la sección y el código, pero no el nombre del estudiante. Ahora el nombre queda visible también en teléfono.
2. **Datos de otro hijo durante una carga:** Resumen y las tres páginas académicas conservaban información anterior y aceptaban respuestas fuera de orden. Cada consulta retira el estado anterior y valida su generación antes de mostrar resultados, incluso en cuenta corriente y bloques independientes del Resumen.
3. **Adjuntos:** un fallo al consultar archivos se presentaba como «Sin archivos». Ahora tiene estado de carga, error explícito y reintento. Una respuesta tardía de una entrega cerrada no aparece en otra entrega.
4. **Texto sin guardar:** cerrar una entrega, pulsar Escape o salir de Tareas podía perder lo escrito. Se solicita confirmación, cancelar conserva el texto y las operaciones en curso bloquean la salida. La recarga/cierre de pestaña usa la advertencia del navegador.
5. **Fallos recuperables:** Resumen deja de inventar listas vacías o mostrar cargas permanentes si fallan tareas, notas, asistencia o portería. Hay reintento y detalle por bloque. El calendario informa fallos de portería y Ajustes permite recuperarse del fallo inicial sin conservar el aviso anterior. Las consultas iniciales de las páginas estudiantiles también ofrecen reintento.
6. **Diálogos en pantallas cortas:** Inventario no usaba el contenedor común y la estructura de horarios no tenía cuerpo desplazable. Guardar/cancelar quedaban fuera de la vista. Ambos mantienen encabezado y acciones accesibles, con desplazamiento del formulario; se comprobó desde 320 × 640.
7. **Atajo de rol propio:** «Primeros pasos» solo encontraba la cuenta si estaba en la primera página del directorio. Ahora busca por usuario y pagina los resultados cuando hace falta; un fallo deja explicación visible.
8. **Libreta poblada en teléfono:** un título largo ensanchaba una actividad hasta ocultar sus campos bajo las columnas fijas. Se acotaron encabezados/campos y se reservaron anchos para nombre, actividad y total. La prueba con datos guardados comprueba que el primer campo quede completamente expuesto incluso a 320 px.

No se cambiaron permisos, políticas de calificación, configuración institucional ni contratos del servidor.

## Capturas y evaluación

- **166 vistas privadas por corrida**, antes y después, de Dirección, Secretaría, Caja, Docente, Estudiante, Familia con dos hijos, Portería y Bodega. Escritorio 1440 × 1000 y teléfono 390 × 844. Se evaluaron jerarquía, lectura, identidad del estudiante, navegación, controles, tablas y estados vacíos.
- **42 diálogos por corrida**, en escritorio y teléfonos de 390 × 844 y 320 × 640: personas, matrícula, cursos, tareas, punteo, plantilla con numerosas actividades, avisos, cuentas, roles, inventario y horarios. Se comprobaron acciones visibles y desplazamiento. Las capturas finales usan el ajuste de márgenes móvil comprobado también por E2E.
- **9 vistas públicas:** inicio, ingreso y activación/recuperación mediante código, en los tres tamaños. Sin credenciales en las capturas.
- **8 PDF, 23 páginas renderizadas:** horarios en carta/A4, carnets estudiantiles y de personal en carta/tarjeta y libreta con actividades/punteos. Se revisaron logos, cuadrícula, nombres, anversos/reversos, totales y ausencia de recortes. El espacio vacío de un horario corto es intencional; no se agranda artificialmente la tipografía para llenar la hoja.
- Las dos corridas de vistas privadas y la revisión de diálogos/públicas terminaron sin infracciones WCAG A/AA detectadas por Axe ni desbordamiento horizontal de página. Las pestañas/tablas con desplazamiento interno conservan ese comportamiento intencional. Se inspeccionaron hojas de contacto y capturas individuales de los hallazgos y sus correcciones; esto complementa los controles automáticos.

Evidencia local ignorada: `.local/qa/evidencia/review-visual-2026-11-09/`. `before/` y `after/` contienen capturas y `survey.json`; `probes-before/` y `probes-after/` reproducen archivos fallidos y cambio entre hijos; `dialogs/`, `dialogs-after/`, `public/`, `prints/` y `regression-final/` conservan el resto. Las respuestas interceptadas para provocar lentitud/errores son sintéticas y no se presentan como validación de autorización del servidor.

## Validación

- SQL Server real: **183 pruebas aprobadas**; TRX `.local/qa/test-results/review-visual-full.trx`.
- Flutter: **105 pruebas aprobadas**, análisis sin observaciones.
- Angular y publicación Release: correctos.
- Regresión web completa: **115 aprobadas** y tres omisiones previstas. Las dos de migración se ejecutaron después correctamente con MariaDB/SQL aislados; rendimiento se mide una vez en escritorio.
- Libreta con actividades: **10 pruebas aprobadas** en la última corrida de libreta, notas, resiliencia y tareas, incluidas dos pruebas nuevas con datos guardados, recarga, suma, estados no entregó/exonerado, rechazo de más de 100 puntos y PDF. Las dos nuevas se suman a las 115 de la suite completa: **117 comprobaciones web distintas**, más las **2 de migración**. Las otras ocho repiten flujos afectados después de la última corrección de CSS.

Logs finales: `.local/qa/test-results/review-visual-{web-final,grade-final,migration,flutter,flutter-analyze,build-final,release-final}.log`. Se preservaron las 213 capturas de la suite completa antes de que Playwright vaciara su carpeta para la migración. Las pruebas nuevas son `student-pages-resilience.spec.ts` y `activity-points.spec.ts`; `first-steps.spec.ts` cubre además la paginación.

Las nuevas pruebas de resiliencia cubren cambios entre hermanos, respuestas tardías, fallos/reintentos, archivos, conservación del borrador y botones de diálogos en pantallas cortas. El atajo de rol propio se prueba excluyendo la cuenta de la primera página.

## Alcance y continuidad

No se declara ausencia absoluta de defectos. La evaluación visual corresponde al navegador; las pruebas de Flutter no sustituyen revisión en dispositivos físicos. La comprobación física de huella/Face ID, lectores e impresión continúa pendiente. Los detalles de actividades y descarga de adjuntos del chat en Flutter siguen como pendientes documentados en ADR-022; este incremento corrige la web.

El código se integró y subió a `main` en `0deca89`. La publicación local corregida está activa en `http://127.0.0.1:5085`, desde `.local/main-local/qa-visual-app`, con health 200. El reinicio validó base, puerto, PID y ruta de la instancia original. La comprobación posterior confirmó ingreso de los ocho usuarios QA, nombres de estudiante en las tres páginas y acciones de Inventario visibles, sin errores de ejecución. Conserva la base sintética y el ciclo 2027. Evidencia: `local-main/`; comparación de la libreta en `libreta-antes-despues.png`. No se modificó la instancia 5086 del otro agente.
