# ADR-015: preparación de la migración inicial para 2027

Fecha: 6 de noviembre de 2026, America/Guatemala.

Estado: **implementado y validado con datos sintéticos** por solicitud posterior del propietario. El corte con personas reales no se ha ejecutado. El importador MIG-001a anterior conserva su alcance separado de personas.

## Contexto y autorización

El propietario hará la migración, no el cliente. Pide estudiar alternativas en una rama separada: estudiantes, padres/encargados existentes y matrícula en el ciclo indicado; el nuevo sistema empezará a usarse en 2027 sin historial académico. Todos los accesos importados deben comenzar inactivos y habilitarse individualmente después.

La solicitud posterior exige dejar la herramienta lista y probar los escenarios. Autoriza implementación y ensayos sintéticos; no supone importar datos reales para desarrollar, modificar el PHP/MariaDB ni efectuar el corte ahora. ADR-003/004/005/012/014 siguen vigentes.

## Decisión implementada

Herramienta de consola operada por el propietario: extracción de lectura → revisión explícita del padrón → `plan` sin escrituras → aplicación transaccional del paquete exacto → conciliación independiente. Sin asistente web para el cliente, conexión permanente entre plataformas ni sincronización de notas.

**Ampliación por solicitud posterior de máxima sencillez:** el propietario abre `Migrar estudiantes.cmd` y utiliza una pantalla de tres pasos. Es un asistente local del operador, deshabilitado en el host normal, habilitado expresamente con `--migration-assistant` y accesible solo desde loopback con sesión Identity activa y los tres permisos de importación. Reutiliza SignInFlow/MFA y autorización SQL; el proceso local aísla el nombre de su cookie para no reemplazar otras sesiones de localhost. No hay esquema de tokens nuevo ni rutas académicas anónimas.

El detector admite configuraciones PHP literales y no ejecuta/incluye el archivo; las claves se usan solo dentro del proceso hijo. La propuesta de separación de nombres y adultos se confirma desde la pantalla; no modifica el contrato estricto ni otorga facultades. El operador elige continuidad/grado por grupos y corrige excepciones. Puede consolidar expresamente un encargado repetido entre fichas familiares. Las propuestas incompletas siguen bloqueadas por el importador.

Las revisiones se guardan fuera de Git en directorios privados por operador/lote, sin claves; solo el titular con permisos puede reabrirlas. Un bloqueo de archivo más el bloqueo SQL impide que dos ventanas/procesos cambien el paquete durante la carga. Una revisión guardada invalida su plan anterior. El asistente crea y verifica un backup COPY_ONLY antes de aplicar, conserva su referencia privada y ejecuta reconcile después del commit. Si no puede respaldar, no carga datos. Puede preparar exclusivamente la migración aditiva del padrón y un rol SQL vacío cuando faltan; otros pendientes de esquema exigen actualizar la instalación.

- Una persona estable por estudiante/encargado confirmado; varias referencias antiguas pueden corresponder a esa persona solo mediante decisión de revisión. Una fila familiar antigua puede representar dos adultos: su descomposición requiere identificación explícita, no un mapeo uno a uno obligatorio.
- El ciclo de origen sirve para seleccionar candidatos. El GUID del ciclo de destino, el grado, la sección opcional y la fecha efectiva de cada matrícula son explícitos. Ni el año de hoy ni el ciclo predeterminado deciden escrituras. No se deduce promoción, continuidad ni repetición de las notas o del orden de grados.
- Solo estudiantes seleccionados para continuar y sus encargados identificados. Los demás candidatos quedan excluidos con motivo o pendientes; no reciben matrícula por omisión. No se exige encargado a un estudiante.
- Vínculos familiares verificables separados de las personas, con facultades inicialmente vacías. No se atribuye un teléfono familiar a un adulto sin revisión.
- Una cuenta pendiente por persona confirmada, incluso si tiene varios perfiles, vinculada a ella: `IsActive=false`, `PasswordHash=null`, sin códigos, sesiones, autenticadores ni permisos administrativos. Persona activa y matrícula activa son compatibles con acceso inactivo. No suspender cuentas preexistentes ajenas al lote ni al operador.
- El rol de las cuentas nuevas debe elegirse por clave SQL y verificarse con **cero permisos administrativos efectivos**, nunca por nombre. La revisión individual de vínculos/facultades y el canje del código de un uso habilitan después el acceso usando IAM existente y la misma cookie para web/app.
- No usar `InviteAsync` para generar cuentas masivamente: también emite un código. `RosterAccountProvisioner` prepara cuentas pendientes sin emitir secretos dentro de la transacción/bloqueo IAM compartidos; el vínculo y auditoría se confirman juntos.
- Paquete v2 estricto y comando `--import-roster`, separado del importador v1. Rechaza campos desconocidos/ausentes, propiedades repetidas y modos desconocidos. El v1 también rechaza campos desconocidos y fuentes que ya tengan lotes v2.

## Consecuencias y límites

La migración nueva `InitialRosterImport` incorpora `people.RosterBatches` (hashes, actor, ciclo y resultado técnico durable) y `RosterSourceAliases` (fuente/entidad/ID/discriminador → persona/lote). Conserva `LegacyIdentities` y las migraciones aplicadas. Una referencia familiar exige decisión explícita; su descomposición en adultos no se infiere. Solo se crean fichas nuevas: un lote distinto no actualiza ni reutiliza fichas importadas antes; un lote inicial completo evita repetir encargados entre lotes.

Contraseña del operador comprobada sin rehash/escrituras durante plan, permisos revalidados y TOTP real cuando corresponde. Identity y School comparten conexión/transacción serializable. Bloqueos en orden IAM → matrícula; la escritura web de matrícula también adquiere el bloqueo de matrícula antes de validar cupo. Un fallo revierte las cuentas y los registros escolares. Hash del plan liga archivo, catálogo, actor y estado de filas ordenadas; un cambio de destino obliga a plan nuevo.

Conciliación independiente de solo lectura y reintento por el mismo lote: no duplica ni suspende cuentas activadas después; informa cambios posteriores. Los reportes/auditoría guardan IDs/conteos sin nombres, contactos o secretos. Exportador PHP independiente de solo lectura, sin cargar el legado, con control de motores/esquema y salida privada; generador de revisión deja todas las decisiones pendientes hasta revisión del propietario.

No se migra historial 2026, notas, promedios, tareas, asistencia, dinero, solvencia, claves, permisos antiguos ni credenciales de portería. Tampoco se generan cuotas, carnets ni códigos de activación al importar. Las cuotas 2027 se configuran después por su flujo normal; ausencia de saldo no equivale a solvencia.

El ensayo usa datos sintéticos y SQL Server real en bases propiedad del fixture, además de MariaDB sintético en una instancia aparte. La migración real será una operación posterior del propietario con respaldo verificado y conciliación. No se declara ejecutado ni conciliado el corte real.

Detalle, alternativas, hallazgos verificables, archivos preparatorios y pruebas de aceptación: [MIGRATION_2027.md](../MIGRATION_2027.md).
