# Avisos

Implementado en el décimo incremento (COM-001). Solo mensajería interna: **no se envían correos, SMS ni push**.

- `communication.Announcements`: título, mensaje, audiencia (`School`, `Grade` de un ciclo, `Course`), destinatarios (estudiantes, encargados, personal —solo para todo el colegio—), estado `Draft → Published → Archived` (solo avanza; un aviso publicado no se edita).
- Redacción: `communication.publish` para todo el colegio y grados (Coordinación académica y Administración institucional); un docente con asignación no finalizada puede publicar en sus cursos.
- Destinatarios calculados en cada lectura: el estudiante por su ubicación vigente; el encargado **solo** por vínculos vigentes con la facultad `Communications`, mostrando a qué estudiante corresponde; el personal por su perfil; el docente ve los avisos de sus cursos.
- `communication.AnnouncementReads`: confirmación de lectura por cuenta (idempotente). Los autores ven el número de lecturas.
- A las familias nunca se les muestra el nombre de usuario interno del autor: si la cuenta no representa a una persona se muestra «Administración del colegio».

API: GET `/api/v1/announcements/options`, GET/POST `/api/v1/announcements`, POST `/{id}`, `/{id}/publish`, `/{id}/archive`, GET `/api/v1/me/announcements`, POST `/api/v1/me/announcements/{id}/read`.

UI: `/workspace/announcements` (recibidos y redactados) y «Avisos recientes» en el inicio con contador sin leer.

Pruebas: `CommunicationSqlTests` (2) y `e2e/announcements.spec.ts`.

Pendiente: notificaciones externas con proveedor contratado y consentimiento, adjuntos, programación de publicación, selección de ciclo para avisos por grado (hoy usa el predeterminado).
