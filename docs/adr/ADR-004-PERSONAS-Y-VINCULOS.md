# ADR-004: personas, perfiles escolares y vínculos familiares

Estado: implementado en el tercer incremento. Fecha: 5 de noviembre de 2026, America/Guatemala.

## Contexto

El legado mezcla cuenta, ficha familiar, estudiante y ciclo (un `parentID` por estudiante, padre y madre como textos de una sola ficha, ciclo como parte de la identidad). Las reglas aceptadas exigen separar cuenta, persona, vínculo familiar y matrícula anual; un estudiante puede tener cuenta propia sin encargado, y ningún parentesco concede acceso por sí solo.

## Decisión

- `people.Persons` es el registro estable de una persona de esta institución, sin ciclo ni cuenta. Contactos en `people.PersonContacts` (teléfono, correo, dirección; uno principal por tipo).
- Los perfiles son tablas separadas: `people.Students` (código institucional único, mayúsculas) y `people.Guardians`. Una persona puede tener ambos.
- `people.GuardianRelationships` une estudiante y encargado con parentesco descriptivo, inicio y finalización auditada. Un índice filtrado impide dos vínculos vigentes iguales; finalizar no borra.
- Las facultades viven en `people.RelationshipFaculties`, una fila por concesión con otorgamiento y revocación. **Ninguna se concede por defecto ni se infiere del parentesco.** El catálogo (información académica, estado de cuenta, responsable de pago, emergencia, recoger, comunicaciones) es técnico; su significado legal y qué facultad corresponde a cada familia lo decide el colegio caso por caso. Información académica seguirá condicionada además a la solvencia cuando existan notas publicadas.
- `people.AccountLinks` relaciona una cuenta Identity con una persona: un vínculo vigente por cuenta y por persona. Vincular exige `people.manage` **y** `accounts.manage`, porque otorga a la cuenta el contexto de esa persona. Desvincular conserva el historial y no suspende la cuenta.
- La autorización por recurso de autoservicio (`/api/v1/me/students/{id}`) se calcula en SQL en cada petición: la cuenta representa al estudiante o representa a un encargado con vínculo vigente. Sin acceso se responde 404 para no revelar existencia. Los módulos futuros (notas, finanzas) deben pedir además la facultad concreta mediante `PeopleService.StudentAccessAsync`.
- Permisos nuevos: `people.read` y `people.manage`, con perfiles «Consulta de personas y familias», «Secretaría: personas y familias» y «Administración institucional». Un conjunto de permisos que no coincide con un perfil se muestra como «Permisos personalizados», nunca como cuenta básica.

## Persistencia y auditoría

Se agrega `SchoolDbContext` para los registros escolares (personas ahora; estructura académica y matrícula después), con historial de migraciones en `school.__EFMigrationsHistory`. Identity sigue siendo dueño de `iam` y `audit`; `SchoolDbContext` los mapea excluidos de sus migraciones únicamente para referenciar cuentas con FK, recomprobar permisos y agregar eventos de auditoría **en el mismo `SaveChanges`** que el cambio de negocio. El contexto rechaza modificar cuentas/permisos y modificar o borrar auditoría.

`audit.Events` gana la columna `TargetType` (migración `AuditTargetType`). Nulo significa cuenta (eventos históricos); `Person` o `Relationship` identifican registros escolares. La auditoría no guarda nombres ni datos personales de menores: solo identificadores, tipos de perfil, parentesco y claves de facultad.

Las migraciones se aplican en orden: Identity primero y luego School. `Initialize-Development.ps1`, el harness E2E y los fixtures lo hacen así; `--provision-owner` exige ambas sin pendientes.

## Fuera de alcance / pendientes

- Identificadores oficiales (CUI, pasaporte) y su protección: no se capturan todavía.
- Política de mayoría de edad, custodia y restricciones judiciales: sin automatizar (ver plan §8.7 y §10.3).
- Fusión de duplicados: hoy se advierte por nombre+fecha y se exige confirmación expresa; no hay merge.
- Importación del legado (MIG-001a): no existe; no inferir vínculos por apellido.
