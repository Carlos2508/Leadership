# Prueba de uso real — 6 de noviembre de 2026

Rama `qa/uso-real` (worktree `.local/worktrees/qa-uso-real`, desde `main` `ce563a7`), separada de la rama del otro agente. Sin cambios en `main`.

## Cómo se probó

- Base aislada `SchoolPlatform_E2E_<uuid>` con el `TestHarness`, servidor propio en el puerto 5098. Datos completamente ficticios.
- Colegio en marcha al 6 de noviembre: 12 grados (preprimaria, primaria con secciones A y B, básico), 8 materias, 144 cursos, 14 docentes (de grado y de especialidad), 120 familias con 187 estudiantes (hermanos incluidos), inscripción y 10 colegiaturas, 1.945 recibos con familias al día, atrasadas 1–2 meses y muy atrasadas, notas de los bimestres 1–3 (1 y 2 publicados), asistencia de la última semana y avisos.
- Cuentas por rol: Secretaría, Caja, Coordinación académica, Portería, 4 docentes, 3 familias (con 3 hijos, atrasada, al día) y un estudiante. Tareas del día a día hechas por la interfaz, contando acciones y tiempo, con capturas (`.local/qa/uso-real/`).

## Qué funciona bien

- Las reglas sensibles se cumplen con datos reales: la familia atrasada ve los bimestres que tiene pagados y ve retenido el que no, con el detalle de lo pendiente.
- Cambio de sección, retiro, edición de contacto, aviso a un grado, informes PDF y publicación se hacen en 1–6 acciones.
- Libreta: Enter pasa al siguiente estudiante, guarda sola y avisa la hora del último guardado.
- Registrar a alguien que ya existe pide confirmar el posible duplicado.

## Hallazgos

| # | Quién | Qué cuesta hoy | Estado |
|---|---|---|---|
| H1 | Dirección y Coordinación | Inicio tardaba 0,9 s y Publicación de notas 0,83 s con 187 estudiantes (2 consultas por curso y período: ~1.150 consultas). Con 600 estudiantes serían 3 s o más. | **Corregido en la rama**: 3 consultas por año, mismo resultado verificado (Inicio 0,2 s, Publicación 0,11 s). |
| H2 | Secretaría y Caja | Buscar «Cabrera» devuelve 20 estudiantes sin grado ni sección: no se distingue a los homónimos ni a los hermanos. | **Corregido**: el directorio de estudiantes muestra «Grado y sección» y la búsqueda de Caja lo muestra bajo el código. |
| H3 | Caja | «Recibos de hoy» daba el total pero no el cierre por forma de pago. | **Corregido**: total por efectivo, transferencia y tarjeta (sin recibos revertidos). |
| H4 | Caja y Dirección | La solvencia no se podía sacar a Excel para dar seguimiento; «Con saldo pendiente» se veía gris. | **Corregido**: «Exportar a Excel (CSV)» y etiqueta en rojo suave. |
| H5 | Familias | Las notas eran una lista larga por bimestre, una debajo de otra. | **Corregido**: tabla tipo boleta (materias × bimestres); los bimestres retenidos se explican debajo. |
| H6 | Familias con varios hijos | No había un total de lo que se debe; había que sumar estudiante por estudiante. | **Corregido**: Inicio muestra «Saldo pendiente total» o «Al día». |
| H7 | Docente de grado (10 cursos) | El menú mostraba solo 6 cursos, ordenados por materia y mezclando grupos. | **Corregido**: con más de 6 cursos el menú se agrupa por grado y sección y muestra todos. |
| H8 | Docente de grado | La asistencia es por curso: con dos grupos de 5 materias, el inicio muestra 10 «Asistencia» y se pasa lista 10 veces al día. | **Recomendado** (decisión del colegio): asistencia diaria por grupo, una vez por sección, y por clase solo donde aplique. |
| H9 | Secretaría | Familia nueva con 2 hijos a mitad de año: 48 acciones en 3 pantallas (ficha de la madre, 2 fichas, 2 vínculos, 2 inscripciones). | **Recomendado**: asistente «Nueva familia» que registre, vincule e inscriba en un solo flujo. |
| H10 | Secretaría y Caja | Un estudiante inscrito a mitad de año queda «Configuración incompleta · 8 cuotas sin emitir»: las colegiaturas no se le generan solas. | **Recomendado**: al inscribir, ofrecer emitir las cuotas del ciclo que faltan (con vista previa). Hoy lo avisa la campana como «inscripción sin cargos». |
| H11 | Caja | Una madre con 2 hijos paga en 2 recibos y repite la búsqueda; no hay vista de familia. | **Recomendado**: estado de cuenta familiar con los hermanos y cobro en un solo recibo si el colegio lo aprueba. |
| H12 | Docente | Una misma tarea para sus 5 materias se crea 5 veces. | **Recomendado**: «Publicar también en…» otros cursos del docente. |
| H13 | Secretaría | Los carnets de un grado solo aparecen en Inscripciones después de filtrar un grado. | **Recomendado**: botón visible «Imprimir carnets» que pida grado y sección. |
| H14 | Coordinación | Publicación muestra cuántas notas faltan, pero no de qué cursos ni de qué docentes. | **Recomendado**: detalle por curso con su docente para pedir lo pendiente. |
| H15 | Familias | Al retener notas se explica qué falta, pero no cómo o dónde pagar. | **Recomendado**: texto configurable «Cómo pagar» en `institution.json`. |

## Validación de la rama

- .NET/SQL: **127/127** (incluye la nueva comprobación de grado y sección en búsquedas).
- Playwright completo en la rama: ver HANDOFF.
- Recorridos de uso: `.local/qa/uso-real/uso.mjs` (21 tareas) y capturas de las mejoras.
