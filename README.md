# Plataforma escolar

Reconstrucción en ASP.NET Core 10, Angular y SQL Server. **Un proyecto y una base de datos por colegio.** Para entregarlo a otro colegio se copia el proyecto y se cambia su configuración.

La marca y datos institucionales se editan en [`config/institution.json`](config/institution.json): nombre, teléfono, contactos, logos de pantalla/comprobantes/boletas, favicon, colores, región y vocabulario. No existe editor de marca para administradores.

Ya incluye bienvenida, ingreso/salida real con Identity, gestión de cuentas/perfiles, invitaciones, activación y recuperación sin exigir correo, cierre de sesiones y auditoría persistente. La muestra del directorio es ficticia y solo existe en Development. Expedientes, matrícula, finanzas, tareas y notas publicadas todavía no están implementados.

El prototipo móvil Flutter, con datos ficticios y sin conexión a la API, está en [`mobile/`](mobile/README.md).

## Ejecutar en Windows

Desde la raíz, con .NET y SQL Server local disponibles:

```powershell
dotnet tool restore
dotnet restore --locked-mode
./scripts/Invoke-Web.ps1 ci
./scripts/Invoke-Web.ps1 run build
./scripts/Initialize-Development.ps1
./scripts/Initialize-Development.ps1 -CreateOperator
./scripts/Start-Development.ps1 -SkipBuild
```

Abrir [la plataforma local](http://localhost:5080). La muestra visual está en [Vista general](http://localhost:5080/preview). El operador se crea de forma interactiva, sin contraseña predeterminada. El wrapper prepara Node 24.15.0 local al repositorio; no cambia tu instalación global.

El primer operador tiene gestión de accesos y auditoría. Después de ingresar, abre `/workspace/access` para invitar cuentas. El titular activa o recupera su acceso en `/Identity/Account/Activate` con el código entregado por el colegio. Los perfiles de cuentas no conceden acceso a expedientes ni permisos docentes/financieros. Ver [cuentas y accesos](docs/IDENTITY_AND_ACCESS.md).

## Verificar

```powershell
dotnet test --verbosity quiet
./scripts/Invoke-Web.ps1 exec playwright install chromium
./scripts/Invoke-Web.ps1 test
```

Las pruebas SQL y las de navegador usan bases temporales propias. Playwright prepara un gestor sintético y elimina su base `SchoolPlatform_E2E_*` al finalizar; nunca utiliza la base del colegio. Ver conexiones, publicación y ejecución completa en [RUNBOOKS](docs/RUNBOOKS.md). El proyecto PHP y MariaDB del legado no se modificaron ni se migraron registros.

## Continuar el trabajo

- [Progreso y validación](docs/PROGRESS.md) y [handoff para otra sesión](docs/HANDOFF.md).
- [Plan de ejecución](docs/ROADMAP.md), [definición](docs/PROJECT_DEFINITION.md) y [arquitectura](docs/ARCHITECTURE.md).
- [Configuración del colegio](docs/BRANDING.md), [seguridad](docs/SECURITY.md) y [decisiones](docs/adr/README.md).
- [Reglas académicas](docs/ACADEMIC_RULES.md), [solvencia](docs/BILLING_AND_CLEARANCE.md) y [migración](docs/MIGRATION.md).

El plan maestro original y el material de referencia del kit están en `docs/reference/`. Sus propuestas se distinguen de las decisiones implementadas; la aclaración del propietario sobre un proyecto independiente por colegio queda registrada en la definición y ADR-001.
