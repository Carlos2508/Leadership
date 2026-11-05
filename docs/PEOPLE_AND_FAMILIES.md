# Personas, estudiantes, encargados y facultades

Implementado en el tercer incremento (PEO-001/002). Decisión en `adr/ADR-004-PERSONAS-Y-VINCULOS.md`.

## Modelo

| Tabla | Contenido |
| --- | --- |
| `people.Persons` | Nombres, apellidos, nombre preferido, nacimiento opcional, estado `Active`/`Archived`, rowversion |
| `people.PersonContacts` | Teléfono/correo/dirección, etiqueta y principal por tipo (máximo 10) |
| `people.Students` | Perfil de estudiante con código institucional único |
| `people.Guardians` | Perfil de encargado |
| `people.GuardianRelationships` | Estudiante, encargado, parentesco, inicio, finalización con motivo, rowversion |
| `people.RelationshipFaculties` | Una fila por facultad concedida, con revocación; historial completo |
| `people.AccountLinks` | Cuenta ↔ persona, un vínculo vigente por cada lado |

No existe ciclo ni grado aquí: la matrícula anual es otro módulo. La cuenta no es la persona: una persona puede existir sin cuenta y una cuenta puede no representar a nadie (personal administrativo).

## Reglas

- Duplicado posible (mismos nombres/apellidos y fecha compatible) devuelve 409 `possible-duplicate`; la interfaz exige confirmar «Son personas distintas».
- Código de estudiante: 1–30 caracteres `A-Z 0-9 . -`, en mayúsculas, único (también por índice SQL).
- Correo validado; teléfono con dígitos, espacios, guiones y prefijo `+`.
- No se vincula a personas archivadas. Archivar exige finalizar vínculos vigentes y desvincular la cuenta.
- Parentescos: madre, padre, abuela/o, hermana/o, otro familiar, tutela legal, otro. Solo describen; no otorgan acceso.
- Facultades: `AcademicRecords`, `Finance`, `BillingResponsible`, `EmergencyContact`, `Pickup`, `Communications`. Ninguna por defecto.
- Finalizar un vínculo revoca sus facultades y conserva filas. Puede registrarse un vínculo nuevo después.
- Toda escritura usa versión (rowversion en base64) y devuelve 409 si cambió.
- Los cambios de vínculo, facultades, estado y cuenta exigen motivo (8–500) que queda en auditoría; alta y edición de datos se auditan sin motivo.

## API

| Ruta | Requisito |
| --- | --- |
| GET `/api/v1/people?search=&role=all\|student\|guardian\|none&status=Active\|Archived\|all&page=` | `people.read` |
| GET `/api/v1/people/{id}` | `people.read` |
| GET `/api/v1/people/catalog` | `people.read` |
| POST `/api/v1/people` | `people.manage` + CSRF |
| POST `/api/v1/people/{id}` | Igual; edición con versión |
| POST `/api/v1/people/{id}/roles` | Igual; `student` (con código) o `guardian` |
| POST `/api/v1/people/{id}/status` | Igual; archivar/restaurar con motivo |
| POST `/api/v1/relationships` | `people.manage`; crea vínculo con facultades |
| POST `/api/v1/relationships/{id}` | Igual; parentesco y facultades |
| POST `/api/v1/relationships/{id}/end` | Igual; finaliza |
| GET `/api/v1/people/linkable-accounts?search=` | `people.manage` + `accounts.manage` |
| POST `/api/v1/people/{id}/account` y `/account/unlink` | `people.manage` + `accounts.manage` |
| GET `/api/v1/me` | Cuenta activa; su persona y estudiantes con vínculo vigente |
| GET `/api/v1/me/students/{id}` | Cuenta activa + ser el estudiante o encargado vigente; si no, 404 |

## Interfaz

`/workspace/people` (directorio con filtros, alta con detección de duplicados), `/workspace/people/{id}` (ficha, contactos, perfiles, vínculos activos/finalizados, facultades, cuenta, archivo). En `/workspace` una cuenta vinculada ve su ficha y, si es encargado, sus estudiantes y facultades. En móvil la tabla del directorio se presenta como filas apiladas.

## Pruebas

`PeopleSqlTests` (6, SQL Server real): separación de cuenta/persona/vínculo y auditoría sin nombres; duplicados y código único; historial de facultades y finalización; versiones y archivo; autoservicio con acceso horizontal denegado y revocación inmediata al finalizar/desvincular; permisos `people.read`/`people.manage`/vínculo de cuenta. `e2e/people.spec.ts` recorre alta de familia, vínculo con facultad, invitación, activación, vínculo de cuenta y la vista del encargado en escritorio y móvil con Axe.
