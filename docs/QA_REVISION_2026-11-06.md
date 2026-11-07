# Revisión completa de flujos — 6 de noviembre de 2026 (noche)

Rama `qa/revision-flujos` desde `main` `f84af4d` (incluye chat privado y huella), en el worktree aislado `.local/worktrees/qa-uso-real`. La carpeta principal y la rama del otro agente (`feat/chat-presence-receipts`) no se tocaron.

## Qué se probó

| Prueba | Resultado |
| --- | --- |
| Colegio vacío recorrido de punta a punta por seis roles (web escritorio + app conectado) | 49/49 pasos |
| El mismo recorrido en teléfono (390 px) | 47/47, sin errores de consola ni desbordes |
| Colegio de tamaño real (187 estudiantes, 120 familias, 144 cursos, 1.945 recibos): todas las pantallas del menú de 8 roles, escritorio y teléfono, chat incluido | 172 pantallas, 0 errores de consola, 0 desbordes, todas < 2,5 s |
| Lógica y casos límite por API con cada rol (42 comprobaciones) | 42/42 |
| Suites automáticas | ver HANDOFF |

Casos de lógica cubiertos: sobrepago, monto distinto de lo aplicado, pago de Q 0 y con fecha futura, doble clic en «Confirmar pago», reversión sin permiso y con permiso (saldo restituido), estado de cuenta ajeno; libreta de un curso ajeno, nota en bimestre publicado, notas 101/−1/85,555, corrección publicada sin y con motivo; sección llena, doble inscripción; asistencia futura, fuera del ciclo y en sábado; aviso por grado a otra familia; seis accesos cruzados entre roles; doble lectura en portería y código inválido; chat (contactos solo de los docentes del hijo, docente ajeno rechazado, reenvío sin duplicar, vacío y > 4000 caracteres, no leídos y leídos en ambos sentidos, estudiante solo con docentes, portería sin chat).

## Hallazgos y correcciones

| # | Hallazgo | Estado |
| --- | --- | --- |
| R1 | Chat: los botones «Escribir un mensaje», «Nueva conversación», «Reintentar», «Cerrar», etc. usaban la clase `outline` sin `button` y se veían como botones grises del navegador. | **Corregido** en `_polish.scss` (`button.outline:not(.button)`), sin tocar los archivos del chat porque otro agente trabaja en ellos. |
| R2 | Chat: en el diálogo «Con quién conversar» el título quedaba pegado arriba y la X debajo del título. | **Corregido**: encabezado, cuerpo y pie con el mismo diseño de los demás diálogos. |
| R3 | Chat: el selector de ciclo perdía la flecha porque `_chat.scss` se carga después. | **Corregido** con mayor especificidad. |
| R4 | Chat en teléfono: el diálogo de contactos quedaba más angosto que la pantalla. | **Corregido**: hoja inferior a todo el ancho y búsqueda en una fila. |
| N1 | En una copia nueva del repositorio el app muestra «Falta configurar la institución» hasta ejecutar `dart run tool/sync_institution.dart` (los recursos generados no se versionan). | Esperado y documentado; anotarlo en la guía de instalación del app. |
| N2 | Entre el fin de clases y el cierre del ciclo no se puede inscribir con fecha posterior al fin; el chat y los contactos siguen funcionando con la fecha límite del ciclo. | Comportamiento esperado. |

Sin errores de lógica: todas las reglas de dinero, notas, solvencia, permisos y chat respondieron como se espera, con mensajes claros y sin errores del servidor.
