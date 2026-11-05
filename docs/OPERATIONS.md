# Operación: salud, respaldos, CI y almacenamiento

## Salud

- `GET /health/live`: el proceso responde (sin dependencias).
- `GET /health/ready`: SQL responde y **no hay migraciones pendientes** de Identity ni de School. Devuelve `Healthy` o 503 `Unhealthy` sin detalles. El proceso web nunca migra solo; un despliegue sin migrar queda «no listo».

## Respaldo y restauración verificada

```powershell
./scripts/Backup-Database.ps1 -Database <BaseDeLaInstalacion> -VerifyRestore
```

- Respaldo `COPY_ONLY` con `CHECKSUM` en la carpeta de respaldos de la instancia (o `-Destination`), verificado con `RESTORE VERIFYONLY`.
- `-VerifyRestore` restaura en `SchoolPlatform_RestoreCheck_<aleatorio>`, ejecuta `DBCC CHECKDB`, informa migraciones aplicadas y elimina **solo** esa base temporal (nombre validado).
- Verificado en esta máquina contra `SchoolPlatform_Development`: CHECKDB sin errores, Identity=4 y School=8 migraciones en ese momento; la base temporal se eliminó.
- No sustituye el plan de respaldos de producción (completo/diferencial/log, retención, copia fuera del servidor): debe definirse con el hosting.

## Integración continua

`.github/workflows/ci.yml`: SQL Server 2022 en contenedor, restore bloqueado, build Release con advertencias como errores, `dotnet test`, build de Angular, Playwright (escritorio y móvil) y evidencias como artefacto. **No se ha ejecutado todavía en GitHub** (requiere hacer push; no se hizo por instrucción). Revisar la primera corrida.

## Almacenamiento privado

`Storage:Root` (por defecto `App_Data/files` bajo el content root, ignorado por Git), `Storage:MaxFileBytes` (10 MB) y `Storage:MaxFilesPerSubmission` (5). En producción usar una carpeta fuera del sitio, con respaldo propio y permisos solo para la identidad de la aplicación. No hay antivirus integrado: los archivos se validan por firma y se sirven siempre como descarga con `nosniff`.

## Seguridad de cuentas en producción

Configurar `AccountAccess:RequireMfaForPermissions` al menos con `accounts.manage`, `audit.read` y `finance.reverse` (ver `IDENTITY_AND_ACCESS.md`). El valor por defecto es vacío para poder preparar la instalación; la UI avisa a quien tenga permisos pendientes de MFA.
