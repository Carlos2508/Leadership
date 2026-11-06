# QA de roles, módulos y flujos — 6 de noviembre de 2026

Solicitud del propietario: quitar el aspecto de «página dentro de una tarjeta», quitar el logo grande de la activación, recorrer todos los flujos y comprobar que cada perfil escolar (estudiante, docente, familia) y cada rol creado (Dirección, Subdirección, Secretaría, Caja) vea solo sus módulos, sin cambiar la paleta. Cada prueba se documenta en un reporte HTML.

## Entorno

- Instalación de desarrollo (`SchoolPlatform_Development`) en `http://localhost:5090`, worktree `qa/roles-flujos` creado desde `main` `4e545ea`.
- Solo datos ficticios (`qa-*`, estudiantes «Ficticio QA»). Contraseñas generadas y guardadas con DPAPI del usuario local en `.local/qa/*.credential.xml`; llegan a Playwright por variables de ambiente que se borran al terminar. Códigos de activación solo en memoria y tapados en las capturas.
- Script del recorrido: `.local/qa/evidencia/qa-flows.mjs` (lanzador `run-flows.ps1`). Reporte autocontenido: `.local/qa/reporte/Reporte-QA-roles-y-flujos.html` (generado por `build-report.py`). Ambos ignorados por Git.

## Roles creados desde la interfaz

| Rol | Permisos | Propósito del recorrido |
| --- | --- | --- |
| Dirección | los 15 | rol con todo, sin ser gestor inicial |
| Subdirección | 10 (sin cuentas, cargos, cobro, reversión ni administración de lectores) | rol amplio parcial |
| Secretaría | 4 (`people.read`, `people.manage`, `enrollment.manage`, `academic.read`) | ejemplo existente, verificado |
| Caja | 4 (`finance.read`, `finance.collect`, `people.read`, `academic.read`) | ejemplo existente, verificado |

Estudiante, docente y familia no son roles de permisos: sus módulos salen del vínculo vigente con la persona y de la asignación docente (ADR-012).

## Resultados

Última corrida completa: **41 pasos, 32 correctos, 9 informativos (datos ya creados en corridas anteriores), 0 errores**.


- Matriz rol × pantalla (19 rutas) y rol × API (8 consultas protegidas): cada cuenta abre solo lo que su permiso autoriza; el servidor responde 403 al resto aunque se escriba la URL.
- Menú: docente ve el grupo **Docencia** con sus cursos; familia ve **Mis estudiantes** con cada estudiante vinculado; estudiante ve Académico/Tareas/Calificaciones/Asistencia/Comunicados.
- Secretaría: alta de estudiante, rechazo por cupo lleno («La sección alcanzó su cupo…») e inscripción sin sección; sin acceso a caja/cuentas (403).
- Caja: cobro de la cuota pendiente con recibo y PDF (200); cargos en solo lectura, emisión 403.
- Dirección: panel, auditoría e informes PDF del bimestre (200).
- Subdirección: publica avisos a todo el colegio; cuentas 403; registrar pago 403.
- Comunicados: el aviso de Subdirección llega a familia y estudiante; el docente solo puede avisar a su curso (un aviso a todo el colegio por API se rechaza con 400) y el estudiante lo recibe.
- Docente: un curso asignado, captura de nota con autoguardado («Guardado a las … · 1 nota»), directorio de personas 403.
- Estudiante: su ficha; estado de cuenta propio 404 y notas de otro estudiante 404.
- Familia: Bruno sin facultad académica → notas 404; saldo Q0 tras el cobro.
- Portería: primera lectura «Entrada registrada»; segunda inmediata «Lectura repetida · no se volvió a marcar» (Duplicate). Reporte del día con filtros.
- Vista móvil 390 px sin desbordamiento horizontal para Dirección, Secretaría, Caja, docente, estudiante y familia.

## Correcciones

| ID | Hallazgo | Corrección |
| --- | --- | --- |
| RF-001 | Sitio público, ingreso/activación y espacio de trabajo enmarcados como tarjeta con margen y bordes redondeados | `_public.scss`, `auth.css` y `_identity.scss`: ancho y alto completos; barra lateral fija a toda la altura en escritorio |
| RF-002 | Logo grande sobre los campos en «Tengo un código…» y en verificación en dos pasos | Se quitó la imagen de `Activate.cshtml` y `LoginWith2fa.cshtml` (el logo sigue en el panel lateral) |
| RF-003 | Docente y familia sin acceso directo a sus cursos/estudiantes en el menú | `SessionService` carga `/me` y `/me/courses`; `shell.ts` muestra grupos Docencia y Mis estudiantes |
| RF-004 | Saludo partía el usuario («¡Hola, qa-/cajero!») y el selector de ciclo se recortaba | `white-space: nowrap` en el nombre y ancho mínimo del selector |
| RF-005 | Recibo saltó del No. 3 al 51 tras reiniciar SQL Server (caché de la secuencia) | Migración School `ReceiptNumbersNoCache`: `ALTER SEQUENCE finance.ReceiptNumbers NO CACHE` |
| RF-006 | En Inscribir, el aviso de cupo lleno seguía visible al cambiar a otra sección | El error se limpia al cambiar grado o sección; las secciones llenas dicen «llena» |

No se cambió la paleta ni el diseño general de `main`.

## Pruebas automáticas

**98 .NET/SQL aprobadas**, 0 omitidas; build Angular correcto; suite Playwright completa **81 E2E aprobados + 1 omisión prevista** (82 casos) tras ajustar `finance.spec.ts` y `grades.spec.ts` para buscar el enlace del estudiante dentro de `#content` (el menú Mis estudiantes ahora repite ese enlace). `git diff --check` correcto. Quedan 7 bases `SchoolPlatform_Test_*` del 5/11 23:35 de una corrida interrumpida; no se borraron por no poder confirmar su origen.

## Observaciones sin cambio

- El estudiante ve «Comunicados» en su menú y el resto «Avisos»: proviene del diseño de referencia estudiantil; se conserva.
- El recorrido es repetible: en corridas posteriores los datos ya creados aparecen como «Informativo». Las capturas de la primera creación se conservan en `shots-corrida1/` y `shots-corrida2/`.
- Los números de recibo ya emitidos (51) no se renumeran.
