# Revisión completa de flujos — 6 de noviembre de 2026 (noche)

Rama `qa/revision-flujos` desde `main` `f84af4d` (incluye chat privado y huella), en el worktree aislado `.local/worktrees/qa-uso-real`. La carpeta principal y la rama del otro agente (`feat/chat-presence-receipts`) no se tocaron.

## Qué se probó

| Prueba | Resultado |
| --- | --- |
| Colegio vacío recorrido de punta a punta por seis roles (web escritorio + app conectado) | 49/49 pasos |
| El mismo recorrido en teléfono (390 px) | 47/47, sin errores de consola ni desbordes |
| Colegio de tamaño real (187 estudiantes, 120 familias, 144 cursos, 1.945 recibos): todas las pantallas del menú de 8 roles, escritorio y teléfono, chat incluido | 172 pantallas, 0 errores de consola, 0 desbordes, todas < 2,5 s |
| Lógica y casos límite por API con cada rol (42 comprobaciones) | 42/42 |
| PDF (estado de cuenta, recibo, boleta, carnets por grado y por estudiante, listados) y acceso a PDF de otra familia | 7 PDF válidos; el de otra familia responde 404 |
| Página de ingreso: contraseña equivocada, usuario inexistente, campos vacíos, cierre de sesión | Mismo mensaje genérico para usuario inexistente y contraseña equivocada (no revela cuentas) |
| App conectado (teléfono): docente pasa asistencia y guarda notas; familia abre el chat y escribe al docente | 8/8 pasos, recorridos por su árbol de accesibilidad |
| Suites automáticas | ver HANDOFF |

Casos de lógica cubiertos: sobrepago, monto distinto de lo aplicado, pago de Q 0 y con fecha futura, doble clic en «Confirmar pago», reversión sin permiso y con permiso (saldo restituido), estado de cuenta ajeno; libreta de un curso ajeno, nota en bimestre publicado, notas 101/−1/85,555, corrección publicada sin y con motivo; sección llena, doble inscripción; asistencia futura, fuera del ciclo y en sábado; aviso por grado a otra familia; seis accesos cruzados entre roles; doble lectura en portería y código inválido; chat (contactos solo de los docentes del hijo, docente ajeno rechazado, reenvío sin duplicar, vacío y > 4000 caracteres, no leídos y leídos en ambos sentidos, estudiante solo con docentes, portería sin chat).

## Hallazgos y correcciones

| # | Hallazgo | Estado |
| --- | --- | --- |
| R1 | Chat: los botones «Escribir un mensaje», «Nueva conversación», «Reintentar», «Cerrar», etc. usaban la clase `outline` sin `button` y se veían como botones grises del navegador. | **Corregido** en `_polish.scss` (`button.outline:not(.button)`), sin tocar los archivos del chat porque otro agente trabaja en ellos. |
| R2 | Chat: en el diálogo «Con quién conversar» el título quedaba pegado arriba y la X debajo del título. | **Corregido**: encabezado, cuerpo y pie con el mismo diseño de los demás diálogos. |
| R3 | Chat: el selector de ciclo perdía la flecha porque `_chat.scss` se carga después. | **Corregido** con mayor especificidad. |
| R4 | Chat en teléfono: el diálogo de contactos quedaba más angosto que la pantalla. | **Corregido**: hoja inferior a todo el ancho y búsqueda en una fila. |
| R5 | Carnets de un grado completo: podían salir en cualquier orden y no por sección y apellido, porque `DISTINCT` en SQL descarta el `ORDER BY` de la consulta (`CardService.BatchAsync`). | **Corregido**: se ordena en memoria después de `Distinct` (sección, apellidos, nombres); prueba `CardBatchOrderSqlTests`. |
| R6 | Libreta (app y web): si una nota era inválida, el aviso decía «Revisa las notas marcadas», pero en teléfono la casilla marcada suele quedar fuera de la pantalla y no se sabía cuál era. | **Corregido**: el aviso nombra a los estudiantes («Revisa la nota de Daniela Barrios López: …», hasta tres y «y N más»). |
| R7 | App: las casillas de nota y el estado de cada estudiante no tenían nombre accesible; un lector de pantalla decía solo «campo de texto». | **Corregido**: «Nota de …» y «Estado de …». |
| R8 | App: el aviso «Tienes un mensaje sin leer · Ver» quedaba fijo (Flutter mantiene los avisos con botón) y, al abrir una conversación, tapaba el cuadro de texto y el botón Enviar. | **Corregido**: el aviso se cierra solo a los 5 s y se oculta al abrir Mensajes; prueba de widget. |
| R9 | App: la lista de conversaciones mostraba la fecha en UTC (de noche aparecía el día siguiente). | **Corregido**: fecha local, igual que la web. |
| N1 | En una copia nueva del repositorio el app muestra «Falta configurar la institución» hasta ejecutar `dart run tool/sync_institution.dart` (los recursos generados no se versionan). | Esperado y documentado; anotarlo en la guía de instalación del app. |
| N3 | La libreta abre por defecto el primer bimestre sin publicar (no el que contiene la fecha de hoy). Se mantiene: la nota del bimestre se carga al cerrarlo, cuando ya empezó el siguiente. | Comportamiento esperado. |
| N4 | El encabezado de los PDF repite la localidad cuando el nombre configurado ya la incluye (nombre + ciudad en `institution.json`). | Configuración del colegio, no código. |
| N2 | Entre el fin de clases y el cierre del ciclo no se puede inscribir con fecha posterior al fin; el chat y los contactos siguen funcionando con la fecha límite del ciclo. | Comportamiento esperado. |

Sin errores de lógica: todas las reglas de dinero, notas, solvencia, permisos y chat respondieron como se espera, con mensajes claros y sin errores del servidor.
