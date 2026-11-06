# ADR-015: preparación de la migración inicial para 2027

Fecha: 6 de noviembre de 2026, America/Guatemala.

Estado: alcance solicitado por el propietario y estrategia propuesta; **no implementada ni ejecutada**. El importador MIG-001a anterior sigue limitado a personas.

## Contexto y autorización

El propietario hará la migración, no el cliente. Pide estudiar alternativas en una rama separada: estudiantes, padres/encargados existentes y matrícula en el ciclo indicado; el nuevo sistema empezará a usarse en 2027 sin historial académico. Todos los accesos importados deben comenzar inactivos y habilitarse individualmente después.

Esto autoriza investigar y preparar el procedimiento. No supone importar datos reales para desarrollar, modificar el PHP/MariaDB ni efectuar el corte ahora. ADR-003/004/005/012/014 siguen vigentes.

## Estrategia recomendada

Herramienta de consola operada por el propietario: extracción de lectura → revisión explícita del padrón → `plan` sin escrituras → aplicación transaccional del paquete exacto → conciliación independiente. Sin asistente web para el cliente, conexión permanente entre plataformas ni sincronización de notas.

- Una persona estable por estudiante/encargado confirmado; varias referencias antiguas pueden corresponder a esa persona solo mediante decisión de revisión. Una fila familiar antigua puede representar dos adultos: su descomposición requiere identificación explícita, no un mapeo uno a uno obligatorio.
- El ciclo de origen sirve para seleccionar candidatos. El GUID del ciclo de destino, el grado, la sección opcional y la fecha efectiva de cada matrícula son explícitos. Ni el año de hoy ni el ciclo predeterminado deciden escrituras. No se deduce promoción, continuidad ni repetición de las notas o del orden de grados.
- Solo estudiantes seleccionados para continuar y sus encargados identificados. Los demás candidatos quedan excluidos con motivo o pendientes; no reciben matrícula por omisión. No se exige encargado a un estudiante.
- Vínculos familiares verificables separados de las personas, con facultades inicialmente vacías. No se atribuye un teléfono familiar a un adulto sin revisión.
- Una cuenta pendiente por persona confirmada, incluso si tiene varios perfiles, vinculada a ella: `IsActive=false`, `PasswordHash=null`, sin códigos, sesiones, autenticadores ni permisos administrativos. Persona activa y matrícula activa son compatibles con acceso inactivo. No suspender cuentas preexistentes ajenas al lote ni al operador.
- El rol de las cuentas nuevas debe elegirse por clave SQL y verificarse con **cero permisos administrativos efectivos**, nunca por nombre. La revisión individual de vínculos/facultades y el canje del código de un uso habilitan después el acceso usando IAM existente y la misma cookie para web/app.
- No usar `InviteAsync` para generar cuentas masivamente: hoy también emite un código. Hace falta una operación IAM para preparar cuentas pendientes sin emitir secretos, con bloqueo, auditoría y vínculo transaccionales.
- El paquete debe tener una versión nueva y estricta. No enviarlo al importador actual, que no interpreta matrículas ni cuentas y tolera propiedades desconocidas. Un modo desconocido debe fallar explícitamente, no parecer un plan correcto.

## Consecuencias y límites

Se necesita ampliar el importador y su persistencia de lotes/alias antes del corte. `LegacyIdentities` permite varios IDs antiguos hacia una persona, pero su clave actual no representa por sí sola un registro familiar que se divide entre dos adultos. La solución debe incorporar alias revisados con discriminador y vínculos explícitos, sin reescribir migraciones aplicadas.

No se migra historial 2026, notas, promedios, tareas, asistencia, dinero, solvencia, claves, permisos antiguos ni credenciales de portería. Tampoco se generan cuotas, carnets ni códigos de activación al importar. Las cuotas 2027 se configuran después por su flujo normal; ausencia de saldo no equivale a solvencia.

El ensayo de desarrollo usará datos sintéticos y SQL Server real en bases propiedad del fixture. La migración real será una operación posterior del propietario con respaldo verificado y conciliación. No se declara completada MIG-001 ni listo el corte.

Detalle, alternativas, hallazgos verificables, archivos preparatorios y pruebas de aceptación: [MIGRATION_2027.md](../MIGRATION_2027.md).
