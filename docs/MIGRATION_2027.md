# Migración inicial de estudiantes y familias: guía de uso

**Forma recomendada para el propietario:** abrir **`Migrar estudiantes.cmd`** y seguir [los tres pasos del asistente](MIGRAR_EN_3_PASOS.md). Detecta el origen local, propone el padrón, permite revisar por grupos, prepara el esquema aditivo cuando falta y realiza respaldo/plan/aplicación/conciliación desde la misma pantalla. No requiere editar los archivos o comandos descritos abajo; esta guía conserva el modo avanzado.

Actualizado: 6 de noviembre de 2026, America/Guatemala. Operador: propietario/desarrollador.

El flujo de consola ya está implementado: **extraer → revisar → planificar → aplicar → conciliar**. Guarda personas estables, encargados/vínculos confirmados, matrícula del ciclo explícito y una cuenta por persona **inactiva, sin contraseña ni código de activación**. Ningún parentesco concede facultades automáticamente. El propietario activa después cada acceso mediante el código individual existente.

No importa notas, historial 2026, dinero, tareas, asistencia, fotos, documentos ni credenciales antiguas. No genera cuotas, carnets o mensajes. No modifica el PHP ni la base del legado. Una persona puede existir como estudiante y encargado con una sola cuenta; un encargado puede tener varios hijos; un estudiante no necesita encargado.

Decisión: [ADR-015](adr/ADR-015-MIGRACION-INICIAL-2027.md). La comparación de seis alternativas y el análisis original se conservan como [estudio histórico](MIGRATION_2027_STUDY.md).

## Preparar la instalación de destino

1. Usar la rama `feat/legacy-migration-2027` o integrar este incremento en la instalación que vaya a operarse. No cambiar la rama del otro agente. Cada colegio mantiene su propia copia/base.
2. Preparar .NET 10 según `global.json`, restaurar con lock y compilar el host. El artefacto local de esta entrega está en `.local/migration-tool/` y el wrapper usa por defecto el DLL de `bin/Release/net10.0`.

```powershell
dotnet restore SchoolPlatform.slnx --locked-mode
dotnet publish src/SchoolPlatform.Host/SchoolPlatform.Host.csproj -c Release -o .local/migration-tool
```

3. Configurar el ambiente y `ConnectionStrings__SchoolDatabase` mediante el gestor de secretos/entorno de esa instalación. No escribir la conexión ni contraseñas dentro del paquete, marca, Git o comandos grabados. Confirmar el catálogo y verificar el respaldo antes de una carga real.
4. Aplicar las migraciones de Identity y después School con el procedimiento habitual. El importador **no migra ni crea la base al arrancar**. La migración nueva es `20261106202522_InitialRosterImport`: agrega `people.RosterBatches` y `people.RosterSourceAliases`, conservando el mapa v1. Se incluye el [SQL de actualización](migration-2027/initial-roster.sql), generado desde `ReceiptNumbersNoCache`; está destinado exclusivamente a una instalación que ya tenga ese historial. No ejecutarlo repetidamente ni contra otro catálogo. En una base nueva se aplica la cadena completa de migraciones, no solamente este delta.
5. Crear o revisar en la web el ciclo 2027 con sus **fechas reales**, grados y secciones; abrir inscripciones. La etiqueta 2027 no es un GUID. No se deduce promoción ni continuidad a partir de 2026. El ciclo predeterminado no decide escrituras.
6. Tener una cuenta operadora activa con contraseña y `people.manage`, `enrollment.manage`, `accounts.manage`. El comando verifica contraseña, permisos SQL, bloqueo y TOTP si el operador tiene MFA o la política lo exige. No basta conocer el usuario. Para configurar previamente la estructura se usa `academic.manage`.
7. Elegir un rol SQL activo con **cero permisos del catálogo administrativo** para las cuentas nuevas. No se elige por nombre. Si no existe, crearlo desde Roles con el gestor; el importador no siembra roles ni concede privilegios por su nombre.

Consultas de lectura para obtener IDs/estado del destino, sin datos personales:

```sql
SELECT Id, Label, StartsOn, EndsOn, Status FROM academic.Years ORDER BY StartsOn;
SELECT Id, Code, Name, IsActive FROM academic.Grades ORDER BY Sequence;
SELECT Id, AcademicYearId, GradeLevelId, Name, Capacity FROM academic.Sections;
SELECT r.[Key], r.Label FROM iam.AccessRoles r
WHERE r.IsActive = 1 AND NOT EXISTS
    (SELECT 1 FROM iam.AccessRolePermissions p WHERE p.RoleKey = r.[Key]);
```

## 1. Extraer del legado con lectura solamente

Se usa el PHP CLI instalado, con PDO MySQL, **sin cargar ningún archivo de la aplicación antigua**. El wrapper pide una credencial MariaDB de solo lectura y mantiene los secretos fuera de argumentos/listados. No utiliza ni lee la configuración de conexión del PHP antiguo.

```powershell
# Primero ver catálogo, versión, ciclos de origen y motores; sin fichas personales.
./scripts/Export-LegacyRoster.ps1 -HostName localhost -Database '<catalogo-legado>' -Inspect

# Después elegir el ID de ciclo fuente, que NO tiene por qué ser 2026 o 2027.
./scripts/Export-LegacyRoster.ps1 -HostName localhost -Database '<catalogo-legado>' `
  -SourceYearId '<id-numerico-origen>' -SourceKey 'legacy-school-local'
```

Reemplazar los marcadores antes de ejecutar. `-PhpPath` y `-Port` permiten otra instalación. `SourceKey` debe mantenerse estable para esta misma fuente en todos los intentos; cambiarla no es una forma de resolver duplicados.

El exportador comprueba seis tablas, columnas necesarias y motores InnoDB; ejecuta las lecturas en una conexión con `REPEATABLE READ`, instantánea consistente y transacción `READ ONLY`. Esta garantía requiere no hacer DDL concurrente y no incluye tablas no transaccionales: si encuentra otro motor, rechaza la operación. [Referencia oficial de MariaDB](https://mariadb.com/docs/server/ha-and-performance/standard-replication/enhancements-for-start-transaction-with-consistent-snapshot).

Produce `raw-<uuid>.json` dentro de `.local/migration/`, carpeta ignorada con ACL restringida al usuario actual/SYSTEM. No sobrescribe archivos; solo imprime conteos y hash. Conserva códigos con ceros iniciales y no extrae contraseñas, usuarios, permisos o identificadores oficiales. `studentrelation` se extrae aparte para no multiplicar alumnos por sus filas históricas. Encargados de otro ciclo se conservan como referencia a revisar. Una referencia inexistente queda visible en el estudiante.

No se filtra por `active` del legado: ese campo no demuestra continuidad en 2027. El ciclo fuente selecciona **candidatos**, no matriculados confirmados. La operación se rechaza si el ciclo no existe, no hay candidatos, faltan columnas, el motor no es compatible o falla cualquier consulta. No entrega archivos parciales.

## 2. Preparar y revisar el padrón

```powershell
./scripts/New-RosterReview.ps1 -ExportFile '.local/migration/raw-<uuid>.json' `
  -OutputFile '.local/migration/review-2027.json' `
  -TargetYearId '<guid-real-del-ciclo>' -TargetYearLabel '2027' `
  -EnrolledOn '<fecha-real-yyyy-MM-dd>' -RoleKey '<clave-sql-del-rol-vacio>'
```

El borrador nace **bloqueado**: decisiones `pending`, nombres/apellidos vacíos, grados sin resolver y matrículas sin revisar. Conserva el hash exacto de la extracción; genera identificadores de ingreso opacos sin datos personales. No divide nombres, inventa adultos, copia contactos compartidos o promueve alumnos automáticamente. Consultar el archivo raw en privado para completar la revisión.

El [ejemplo completamente sintético](migration-2027/review.example.json) tiene la estructura v2 real; los GUIDs, hash, fechas y rol son ilustrativos y deben reemplazarse en una carga real. No agregar comentarios/campos extras al JSON: el lector rechaza campos desconocidos, campos requeridos ausentes, nulos indebidos y propiedades repetidas.

Revisión que haces tú:

- `candidates`: cada alumno fuente queda `include` o `exclude`, con motivo de 8–500 caracteres. Un incluido apunta a su `personKey`; un excluido tiene `personKey:null` y no debe figurar en personas/alias/matrículas. Los pendientes bloquean el lote entero. Varios IDs antiguos del mismo estudiante pueden apuntar a una persona **tras revisión explícita**, con sus alias correspondientes.
- `people`: nombres/apellidos ya separados, nacimiento opcional `yyyy-MM-dd`, perfiles `student`/`guardian`, código estudiantil estable y único, usuario opaco reservado y contactos verificados. No usar DNI como código de ingreso. Códigos: 1–30 caracteres `A-Z 0-9 . -`. Contactos admitidos: un `Phone` y un `Email` por persona, opcionales. Valores inválidos se corrigen o se excluyen expresamente, nunca se descartan en silencio.
- `distinctPersonConfirmed`: solo poner `true` cuando se haya verificado que una coincidencia de nombre/fecha corresponde a **otra persona**. No permite reutilizar código/usuario/alias ni fusiona fichas.
- `families`: una decisión explícita por referencia familiar de los estudiantes incluidos. `include` enumera los adultos identificados; `exclude` tiene `personKeys:[]` y motivo, por ejemplo referencia rota o titular sin identificar. No dejar un encargado ausente sin decisión.
- `legacyAliases`: cada referencia original apunta a una persona canónica. Estudiante: `entity:"student"`, ID original y `discriminator:"person"`. Familia: `entity:"parents"`, ID de ficha familiar y discriminadores distintos por adulto confirmado. La misma ficha antigua puede originar padre y madre; el mismo adulto puede tener varios alias revisados.
- `relationships`: solo parentescos confirmados, fecha explícita, `reviewed:true` y `faculties:[]`. Una ficha `parents` contiene `name`, `father_name`, `mother_name`: `name` puede repetir al padre. No crear un tercer adulto ni atribuir teléfono/DNI familiar a ambos. Un adulto se guarda una vez para todos sus hijos.
- `enrollments`: una fila por estudiante canónico, GUID de ciclo destino, grado y sección de **ese ciclo/grado** o `null`, fecha dentro del calendario y `reviewed:true`. `0` no representa una sección. Revisar continuidad, repetición o promoción sin trasladar notas antiguas.
- `accountPreparation`: mantener `pending-without-secrets`, `isActive:false`, `passwordHash:null`, `issueActivationCodes:false`, `sendMessages:false`, rol SQL sin permisos y comprobación de permisos vacíos habilitada.

Si se excluye un estudiante, retirar sus objetos exclusivos del borrador; conservar los adultos compartidos que todavía tengan otro estudiante incluido. Los adultos sin estudiante válido se rechazan. Los conflictos se resuelven antes de aplicar: ninguna política «primera fila gana».

## 3. Planificar sin escrituras

```powershell
./scripts/Invoke-RosterImport.ps1 -File '.local/migration/review-2027.json' `
  -Mode plan -Actor '<operador>' -Database '<catalogo-destino>' -Environment Production
```

El wrapper pide contraseña/TOTP de forma oculta y restaura el entorno al terminar. Si se usa el artefacto publicado, agregar `-HostDll '<ruta-absoluta>/SchoolPlatform.Host.dll'`. La conexión procede del entorno/secretos de la instalación, no del paquete. El comando no inicia un servidor HTTP.

Revisar `server`, `database`, `yearId`, `yearLabel`, conteos de alumnos/encargados/vínculos/cuentas/excluidos y las dos huellas: `fileSha256` representa los bytes del paquete; **`planSha256` es el valor que exige apply**, e incluye el destino/estado/actor. `-Database` debe coincidir con la conexión final; sin esa confirmación explícita el comando rechaza incluso plan. El ciclo se comprueba contra su GUID y etiqueta, y los cupos suman destino existente más **todas** las filas del lote. Antes del corte comparar además los grupos del padrón revisado con la estructura del ciclo.

Plan no crea personas, cuentas, códigos, lotes o eventos. Cualquier incidencia bloquea todo el paquete. Si otra operación cambia las precondiciones entre plan y apply, hay que planificar/revisar otra vez.

## 4. Aplicar exactamente el plan y conciliar

Con respaldo del destino verificado y padrón final revisado:

```powershell
./scripts/Invoke-RosterImport.ps1 -File '.local/migration/review-2027.json' `
  -Mode apply -PlanSha256 '<planSha256-revisado>' -Actor '<operador>' -Database '<catalogo-destino>' -Environment Production

# Lectura independiente posterior al commit:
./scripts/Invoke-RosterImport.ps1 -File '.local/migration/review-2027.json' `
  -Mode reconcile -Actor '<operador>' -Database '<catalogo-destino>' -Environment Production
```

Personas, perfiles, matrículas/ubicaciones, relaciones, cuentas/enlaces, alias, resultado del lote y auditoría se confirman en **una transacción SQL compartida por Identity y School**. El lote usa el bloqueo IAM y el mismo bloqueo de escritura de matrícula que la web. Si falla después de crear cuentas, también se revierten esas cuentas. La comparación de precondiciones usa filas ordenadas del destino, no el contador global de rowversions que puede avanzar por un rollback. [Transacciones compartidas de EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

El reporte contiene IDs técnicos y conteos, sin nombres, contactos o secretos. `reconcile` relee personas, perfiles, contactos, cuentas/enlaces/permisos, matrículas/ubicaciones, relaciones/facultades y alias. Debe devolver `observations:[]` inmediatamente después de importar y antes de habilitar accesos.

| Salida | Significado |
| --- | --- |
| `0` | Plan/aplicación/conciliación correctos y sin observaciones |
| `1` | Operación rechazada o fallida; se imprime código seguro, sin stack ni datos personales |
| `2` | Lote ya aplicado con cambios posteriores observados; no se modificaron esos cambios |

Repetir el mismo `batchId` con bytes y plan originales no duplica nada; devuelve `alreadyApplied:true`. Cambiar el contenido de ese lote se rechaza. Una activación/cambio posterior se informa y **nunca se revierte** al repetir. Si se cortó la consola después de un commit incierto, volver a usar el mismo lote/plan o `reconcile`.

El importador inicial no es un actualizador general: un lote distinto no reutiliza ni sobrescribe personas/cuentas/matrículas importadas antes. Código, usuario o alias existentes provocan conflicto. Consolidar estudiantes y familias compartidas en un único padrón inicial; las correcciones y altas posteriores usan los flujos normales o un incremento explícito. No cambiar `source` ni asignar códigos inventados para saltar conflictos.

## 5. Activar gradualmente

Persona y matrícula quedan activas como registros; el acceso permanece inactivo. No se cambia ninguna cuenta ajena al lote ni la del operador.

En Cuentas: localizar al titular por el usuario reservado en su ficha, verificar identidad y vínculo cuenta/persona, revisar hijos/facultades cuando sea un encargado, y emitir un **código de activación individual en ese momento**. Entregarlo manualmente por canal verificado; el titular elige contraseña y ese canje activa su cuenta. El botón de habilitar no reemplaza este proceso para una cuenta sin contraseña. No hay contraseña compartida ni correo obligatorio.

Web/app usan la misma sesión existente. El encargado no recibe datos académicos/financieros por ser padre: necesita las facultades revisadas; las notas, si se publican después, mantienen las reglas de solvencia. Un saldo vacío no acredita solvencia. Los cargos 2027 se configuran por su flujo normal, separados de la importación.

## Límites comprobables y corte real

Se admiten paquetes de hasta 20 MB, 5.000 personas y 10.000 candidatos, con límites adicionales en listas. La prueba de volumen usa 150 estudiantes sintéticos; no es una certificación de rendimiento del máximo ni del servidor de producción. Ante una incidencia se rechaza el lote completo, sin aplicar filas sueltas.

Antes del corte real hacer la extracción final y revisar diferencias con la anterior; una huella distinta no autoriza sincronizar cambios automáticamente. El propietario define cuándo se deja de registrar en el legado y dónde entrarán las nuevas altas. No se ha ejecutado extracción/carga de personas reales ni cambiado la base del colegio en esta entrega.

Las pruebas, sus escenarios y evidencia están en [QA_MIGRATION_2027.md](QA_MIGRATION_2027.md). Las verificaciones de desarrollo usan SQL Server y una instancia MariaDB independiente con directorio/puerto aleatorios, siempre con datos sintéticos.
