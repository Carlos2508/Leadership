# Recorrido funcional desde una base vacía

Fecha: 5 de noviembre de 2026, America/Guatemala. Estado: **recorrido completo; 5 incidencias encontradas y corregidas** (QA-001 a QA-005). Pendientes físicos al final.

## Evidencias

Carpeta local ignorada por Git: `.local/qa/evidencia/` (capturas y PDFs con datos ficticios, sin credenciales ni QR de emparejamiento).

**Incidencia de proceso QA-004:** las primeras capturas se guardaron en `web/test-results/qa-desde-cero/`, y la ejecución completa de Playwright **vacía `web/test-results/` al iniciar**: se perdieron `02`, `03`, `05`, `07`, `09`–`13` y `14` originales. Se regeneraron desde el estado final de la base las que siguen siendo observables (`01`, `04`, `06`, `08`, `14`–`25`, PDFs de informes, carnets, recibos y notas). Las capturas de estados intermedios (cupo rechazado, vista previa de cargos, notas retenidas antes del pago, solvencia tras reversión) **no se pueden regenerar**; sus resultados quedan descritos aquí y confirmados en SQL/auditoría. Regla nueva: evidencias de QA manual fuera de `web/test-results/`.

## Objetivo y alcance

Solicitud del propietario: reiniciar la base y probar la plataforma paso a paso para ver cómo funcionan los flujos y detectar errores. Este recorrido usa la interfaz de desarrollo con datos ficticios. No es importación, piloto ni aprobación de políticas del colegio. Los E2E automáticos conservan sus bases aisladas; no se apuntan a la base de desarrollo.

## Paso 0. Respaldo y reset

- Destino comprobado: SQL Server local, **SchoolPlatform_Development**, host de desarrollo detenido.
- Antes del reset: 4 cuentas y 2 personas sintéticas del incidente histórico documentado. Esta solicitud reemplaza la instrucción anterior de conservarlas.
- Respaldo `COPY_ONLY` con checksum: `C:/Program Files/Microsoft SQL Server/MSSQL16.MSSQLSERVER/MSSQL/Backup/SchoolPlatform_Development_20261105_103603.bak`.
- Restauración de prueba completada: `DBCC CHECKDB` sin errores; 4 migraciones Identity y 11 School. Solo se retiró el catálogo temporal de verificación.
- Reinicio autorizado exclusivamente del catálogo de desarrollo y aplicación de migraciones: **correcto: 0 cuentas, 0 personas, 11 migraciones School** antes de preparar el gestor.
- No se modifica PHP, MariaDB ni se utiliza información real. No se guardan contraseñas/códigos en este documento o capturas.

## Secuencia prevista

1. Preparar gestor por consola, entrar y verificar el estado vacío.
2. Crear ciclo escolar, cuatro bimestres, grados y secciones; probar validaciones de fechas/cupos.
3. Crear materias y cursos; registrar docente y asignarlo.
4. Registrar estudiantes y encargado, facultades y matrícula anual.
5. Configurar cuotas/corte de solvencia y emitir cargos con revisión.
6. Capturar notas (incluye pendiente, cero y exoneración), publicar y comprobar retención.
7. Registrar cobro sintético, descargar recibo y volver a consultar notas.
8. Crear tarea/entrega y asistencia por curso; publicar aviso y comprobar audiencia.
9. Emitir carnet y probar entrada, duplicado, salida manual, anulación y reporte.
10. Revisar permisos, auditoría, escritorio/móvil, errores de servidor y consistencia SQL.

Cada paso registrará la acción, el resultado observado, la evidencia y las incidencias/correcciones. Lo no probado se marcará como pendiente; una pantalla de muestra no cuenta como flujo implementado.

## Paso 1. Gestor y estado vacío — aprobado

1. Preparar `qa-operador` mediante consola `--provision-owner`, con una contraseña exclusiva de esta corrida (sin contraseña predeterminada).
2. Ingresar: solo aparecen gestión de cuentas y auditoría. La cuenta inicial no recibe funciones escolares implícitas.
3. En Cuentas y accesos → Gestionar → Administración institucional, registrar el motivo del recorrido ficticio.
4. Guardar: se invalida la sesión; al volver a ingresar aparece la navegación escolar.
5. Ciclos y grados muestra «Aún no hay ciclos» y «Aún no hay grados».

Credencial local cifrada con DPAPI del usuario de Windows en `.local/qa/operator.credential.xml` (ignorada). No se imprime su secreto ni se incluye en capturas.

## Paso 2. Ciclo, bimestres y estructura — aprobado

1. Crear ciclo **2026 · QA ficticio**, del 12/01/2026 al 20/11/2026. Se guarda en borrador y como primer predeterminado.
2. Crear Bimestre 1: 12/01–20/03; Bimestre 2: 23/03–29/05; Bimestre 3: 01/06–14/08; Bimestre 4: 17/08–20/11.
3. Prueba negativa: intentar Bimestre 2 desde 01/03. La UI mantiene el formulario y muestra **«El período se superpone con otro.»**; corregir a 23/03 permite guardar. No se crea un período duplicado.
4. Crear Primero primaria · QA (`QA-1P`, orden 1) y Segundo primaria · QA (`QA-2P`, orden 2), nivel Primaria.
5. Crear sección A en Primero, jornada Matutina y cupo 2 para probar el límite más adelante. Segundo queda sin secciones.
6. Abrir inscripciones con motivo: cambia a Inscripciones abiertas; sigue con 0 inscripciones.

Evidencia: `01-ciclo-bimestres.png`. Fechas/cupos exclusivamente de prueba; no son decisiones del colegio.

## Paso 3. Materias y cursos — aprobado; incidencia en alta docente

1. Cursos y docentes comienza sin materias ni cursos.
2. Crear `QA-MAT` / Matemática · QA y `QA-LEN` / Lenguaje · QA.
3. Crear dos cursos para Primero primaria · QA, alcance Todo el grado; cada lista comienza con 0 estudiantes y Sin docente.

Evidencia: `02-materias-cursos.png` (perdida, ver abajo).

### Incidencia QA-001: el alta web impedía registrar personal

**Reproducción:** Personas y familias → Registrar persona → Elena Docente Ficticia QA, sin marcar estudiante ni encargado → Registrar.

**Observado:** «Selecciona al menos un perfil: estudiante o encargado.» La API admite una persona sin esos perfiles y la ficha tiene «Agregar perfil de personal», pero la validación web impedía llegar hasta allí. No se creó la persona.

**Corrección:** quitar la validación antigua, indicar que los perfiles iniciales son opcionales y explicar que el personal se agrega desde la ficha. No asignar falsamente un perfil familiar a un docente. Prueba de regresión y repetición manual pendientes de registrar abajo.

### Repetición de QA-001

Tras compilar y recargar, Elena se registró sin perfil familiar/estudiantil. Desde su ficha se agregó Personal → Docente correctamente. Se amplió `courses.spec.ts` para registrar docente por UI, en lugar de prepararlo por API, y verificar que no tenga perfil de estudiante ni encargado. La primera ejecución detectó una espera faltante en la prueba; se agregó espera de la confirmación antes de consultar el resultado.

## Paso 4. Personas, vínculos, cuentas y matrícula — aprobado

- Crear Ana (`QA-001`), Bruno (`QA-002`), Celia (`QA-003`), Marta encargada y Elena docente, todos ficticios, sin contactos o fechas personales inventadas.
- Preparar cuentas básicas ficticias de docente, familia y estudiante mediante consola y los servicios `InviteAsync`/`RedeemAsync` (secretos solo en memoria y credenciales locales DPAPI). **La activación con contraseña de estas cuentas no se recorrió en el navegador en esta sesión**. No se editó SQL de IAM ni se eludió su auditoría/transacción.
- Vincular por UI cada cuenta a su persona: Elena/docente, Marta/familia, Ana/estudiante.
- Marta → Ana: facultades Información académica, Estado de cuenta y Comunicaciones. Marta → Bruno: solo Estado de cuenta. El parentesco no concede las otras facultades.
- Asignar Elena únicamente a Lenguaje, desde 12/01/2026. Matemática queda sin docente para comprobar alcance por recurso más adelante.
- Inscribir Ana y Bruno en Primero A desde 12/01/2026. Intentar Celia en A: **«La sección alcanzó su cupo. Elige otra o ajusta el cupo.»**. Cambiar a Sin sección: inscripción correcta; quedan 3 matrículas y A sigue 2/2.
- Iniciar ciclo con motivo: pasa a En curso; desaparecen los controles de edición de fechas/bimestres. Se conservan los cuatro períodos y las tres inscripciones.

Evidencias: `03-cupo-rechazado.png` y `04-matricula.png` en la carpeta de este recorrido.

## Paso 5. Configuración financiera y cargos — aprobado

1. Crear concepto `QA-COL`, Colegiatura ficticia QA, recurrente y que cuenta para solvencia.
2. Crear Cuota 1 · QA con vencimiento 05/02/2026 y corte Publicación Bimestre 1 · QA, que exige hasta cuota 1.
3. Seleccionar el concepto/cuota, importe ficticio Q250 y Primero primaria. Vista previa: 3 destinatarios, 0 existentes, total Q750; aún no hay cargos confirmados.
4. Confirmar: **3 emitidos, 0 existentes, 0 excluidos**.
5. Repetir vista previa con los mismos criterios: **0 por emitir, 3 ya emitidos, Q0; confirmación deshabilitada**. No se duplicó la obligación.

Evidencia: `05-cargos-vista-previa.png`. Estos valores son datos sintéticos para probar; no constituyen un plan financiero aprobado.

## Paso 6. Libreta, publicación y corrección — aprobado

1. Ingresar como `qa-docente`: el inicio reconoce a Elena y solo muestra Lenguaje. Abrir Matemática por una URL observada como administrador: **«No tienes acceso a este curso»**. No basta con ocultar el enlace.
2. Lenguaje muestra los 3 matriculados, incluida Celia sin sección. Pegar una columna como desde Excel: Ana 85, Celia E (exonerada), Bruno P (pendiente). El autoguardado completa 3/3 y se conserva al recargar.
3. Prueba negativa: 101 en Ana → «Entre 0 y 100, hasta dos decimales.»; restaurar 85 vuelve a guardarse.
4. Como coordinación, Matemática: Ana 0, Celia 72, Bruno 80. **Enter en Ana enfoca el campo de Celia**. Cero queda como Con nota, no pendiente.
5. Publicación muestra 6/6 en Bimestre 1 y 0/6 en los demás. Intentar publicar Bimestre 2: «Faltan 6 notas por registrar (o marcar como pendiente/exonerado) antes de publicar.»; cancelar sin publicación.
6. Publicar Bimestre 1 con el corte de cuota 1. El período queda marcado Publicado; no se publican promedios anuales ni redondeos.
7. En Matemática/Bimestre 1 cambiar Celia 72→73 sin motivo: se rechaza. Agregar motivo ficticio de QA y guardar: confirma 1 nota. Debe conservarse la revisión previa; se verifica al cierre en SQL.

Evidencias: `06-libreta-docente.png`, `08-publicacion.png`.

## Paso 7. Consulta familiar antes del pago y facultades — aprobado

- Ingresar `qa-familia`: reconoce a Marta y lista únicamente Ana y Bruno vinculados, con sus facultades distintas.
- Ana: Bimestre 1 publicado pero retenido por Q250. No muestra valores de notas; tareas y asistencia siguen visibles. El PDF descargado se conserva como `09-notas-retenidas.pdf` para verificar que no contenga notas.
- Bruno: «Tu vínculo no incluye la consulta de información académica», tampoco tareas/asistencia/matrícula. Sí muestra el saldo Q250. No se concede acceso académico por ser Madre.
- El aviso ficticio llega a la familia por Ana (facultad de comunicaciones), no por Bruno. Abrirlo cambia de Sin leer a Leído.

Evidencias: `09-familia-notas-retenidas.png`, `10-familia-sin-facultad-academica.png`.

## Paso 8. Aprendizaje, asistencia y avisos — aprobado

- Elena creó Lectura y resumen · QA, instrucciones ficticias y entrega 06/11/2026 18:00; primero Borrador, después Publicada con 0/3 entregas.
- Asistencia del 05/11/2026: Ana presente, Celia ausente, Bruno tarde, con observaciones ficticias. Guardado correcto (3 cambios); la familia de Ana ve 1 presente.
- Coordinación redactó y publicó Aviso de prueba · recorrido QA, interno y expresamente ficticio; no se envió correo/SMS.
- Ana (cuenta de estudiante) entregó la tarea **aunque sus notas estaban retenidas**: las tareas no dependen de solvencia. Elena la revisó con retroalimentación; la familia ve «Revisada» y el texto de retroalimentación, no el texto de la entrega.

Evidencias: `07-asistencia-docente.png`, `11-entrega-estudiante.png`.

## Paso 9. Caja, reversión y recibos — aprobado

1. Ana: pago ficticio No. 1 de Q100 → saldo Q150; pago No. 2 de Q150 → saldo Q0, solvente.
2. Revertir No. 2 con motivo: el saldo vuelve a Q150 y la solvencia se bloquea de nuevo; las notas de Ana vuelven a quedar retenidas para la familia.
3. Pago No. 3 de Q150: saldo Q0. En el estado de cuenta familiar el No. 2 aparece tachado como «Revertido».
4. SQL: cada pago coincide con la suma de sus aplicaciones (100/150/150); el cargo de Ana suma Q250 pagados vigentes; Bruno y Celia Q0.
5. PDF del recibo: copia interna (staff, con emisor) y copia familiar (`/me/...receipt.pdf`, **sin usuario interno del emisor**). La familia recibe 403 en la ruta de caja.

Evidencias: `12-solvencia-tras-reversion.png`, `13-recibo-interno.pdf/.png`, `receipt.pdf` (copia familiar).

## Paso 10. Carnets y portería — aprobado con incidencia QA-002

1. Ficha de Ana → PDF de carnet: se emite el carnet. «Reemplazar carnet» con motivo revoca el anterior (auditoría `StudentCardReissued`).
2. Lector de entrada (modo Solo entrada): lectura del carnet → **Entrada registrada**; segunda lectura → **«Entrada ya registrada. No se volvió a marcar.»** (`14-lectura-duplicada.png`).
3. Salida manual con motivo (auditada `GateManualMark`) y posterior anulación con motivo (`GateMarkVoided`): el evento se conserva como anulado y el reporte vuelve a mostrar a Ana «Dentro» (`15-portería-reporte.png`). Reporte: 3 esperados, 1 con entrada, 2 sin marcas.
4. Teléfono lector registrado (sin emparejar físicamente); no se guardó su QR.
5. Impresión masiva de Primero primaria (hoja carta): emite carnets a Bruno y Celia (`StudentCardsIssued`) — 3 activos y 1 revocado en SQL.

### Incidencia QA-002: carnet con logo diminuto y código de barras desalineado

**Observado** en `cards.pdf`: el reverso tenía el Code 128 alineado a la izquierda y el texto centrado; el logo `report.png` (vertical, con texto) quedaba ilegible en la franja del frente.
**Corrección:** código centrado a 12 mm de alto (módulos ≈ 0,3 mm, mejor para lectores 1D) y `Assets.CardLogo` apuntando a `main.png` en `config/institution.json`. Repetido: correcto (`17-carnets-hoja.png`).
**Nota:** el reverso solo muestra nombre, código y contacto porque `Documents.CardRules`, `CardNotice`, teléfono y dirección están vacíos en la configuración: **el colegio debe proporcionar esos textos**; no se inventaron.

## Paso 11. Permisos por rol, PDFs y vista móvil — aprobado con incidencia QA-003

Comprobado por API con cada cuenta (sesión real, sin exponer credenciales):

| Cuenta | Permitido | Rechazado |
| --- | --- | --- |
| Familia (Marta) | Notas/portería/recibos de Ana; estado de cuenta de Bruno; PDF de notas y recibo | Notas/portería de Bruno (404), Celia (404), recibo por ruta de caja (403), `/gate/recent` (403), foto (404) |
| Estudiante (Ana) | Sus notas y marcas de portería | Su estado de cuenta (404: política pendiente), notas de Bruno (404), libreta del curso (404) |
| Docente (Elena) | Libreta de Lenguaje en PDF | Matemática (404), informes por grado (403), portería (403), personas (403) |
| Operador | Informes por grado, carnets, reporte de portería, historial, dispositivos | Informes del Bimestre 2 no publicado (409) |

- Informes PDF de Bimestre 1: Ana con 85 y 0; **Celia y Bruno «Informe retenido: pagos pendientes del corte»**, sin notas.
- Vista móvil (390 px) de la familia: inicio, Ana y Bruno sin desbordamiento horizontal ni errores de consola (`18`–`20-movil-*.png`).

### Incidencia QA-003: título «Calificaciones» pegado al texto

**Observado** en móvil: la tarjeta de calificaciones (con botón de PDF) no tenía separación bajo el título, a diferencia de las demás.
**Corrección:** margen inferior en `.card-title-row`.

### Incidencia QA-005: marca anulada sin dirección en «Últimas marcas»

**Observado** (`24-movil-porteria-lector.png`): la salida manual anulada aparecía solo como «Anulada», sin indicar si era entrada o salida.
**Corrección:** se muestra «Salida anulada» / «Entrada anulada».

Capturas regeneradas: `14-porteria-lector.png`, `15-porteria-reporte.png`, `22-ficha-ana-carnet.png`, `23-auditoria.png`, `24`/`25-movil-porteria-*.png`, `carnet-cr80-1/2.png`, `report-cards.pdf`, `cards.pdf`, `card.pdf`, `recibo-2-revertido-interno.pdf` (ANULADO, con emisor), `recibo-3-familia.pdf` (sin emisor), `notas-familia-ana.pdf`, `libreta-lenguaje.pdf`.

## Verificación final

- SQL: revisión 72→73 de Celia conservada con su motivo; 7 revisiones en total; ningún evento de auditoría sin traza; pagos = aplicaciones.
- Suite .NET: **79/79 correctas**. Playwright completo (escritorio + móvil, Axe): **31 correctas, 1 omitida** (rendimiento solo se mide en escritorio). Tras QA-005: `gate.spec` y `grades.spec` 4/4.
- Log del servidor: sin entradas `fail:`/`crit:` durante el recorrido.

## Pendiente (no se puede comprobar en este equipo)

- Imprimir carnets al 100 % y leerlos con lector USB y cámara reales; emparejar un teléfono físico con HTTPS.
- Activación de cuentas por navegador con código (se usó consola en este recorrido).
- Textos institucionales del reverso del carnet, condición de licencia QuestPDF, tiempos de portería y políticas pendientes (promedios, redondeo, acceso financiero del estudiante).
