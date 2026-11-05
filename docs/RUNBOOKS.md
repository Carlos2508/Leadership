# Operación de desarrollo

## Preparación

Requisitos: Windows/PowerShell, Git, .NET SDK estable 10.0.302 o parche compatible según `global.json`, SQL Server accesible y npm. Node global en esta máquina es 20; `Invoke-Web.ps1` prepara Node 24.15.0 dentro de `.tools/node`, sin cambiar el sistema.

En la raíz:

```powershell
dotnet tool restore
dotnet restore --locked-mode
./scripts/Invoke-Web.ps1 ci
./scripts/Invoke-Web.ps1 run build
./scripts/Initialize-Development.ps1
./scripts/Initialize-Development.ps1 -CreateOperator
./scripts/Start-Development.ps1 -SkipBuild
```

Abrir `http://localhost:5080`. `Initialize-Development.ps1` aplica las migraciones de Identity y luego las de `SchoolDbContext`. Para crear una migración: `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet ef migrations add <Nombre> -p src/SchoolPlatform.Host -c SchoolDbContext -o SchoolData/Migrations` (o `-c IdentityDbContext -o IdentityAccess/Migrations`). El comando de operador pide usuario y contraseña en consola, nunca la imprime y limpia las variables del secreto al terminar. No existe contraseña predeterminada. Si el usuario ya existe, el comando rechaza modificarlo. El espacio autenticado todavía no contiene módulos escolares operativos.

Ese primer operador dispone ahora de gestión de cuentas/auditoría; el procedimiento rechaza crear otro gestor inicial si ya existe uno activo. Los siguientes usuarios se invitan en `/workspace/access`. No son usuarios académicos hasta vincularlos a sus futuros expedientes y facultades. Los comandos no han creado una cuenta predeterminada en la base local.

`appsettings.Development.json` utiliza `localhost`, autenticación integrada y base nueva `SchoolPlatform_Development`. Si tu instancia es otra, definir `ConnectionStrings__SchoolDatabase` en la terminal de esa instalación. No apuntar al legado ni a una base ajena. Cada copia para otro colegio debe usar un nombre de base diferente en su propia configuración de desarrollo y secretos independientes en producción.

## Interfaz

`/`: bienvenida. `/Identity/Account/Login`: acceso real. `/workspace`: sesión protegida y estado inicial. `/preview` y `/preview/students`: muestra sintética solo Development.

Rutas principales (todas verificadas en servidor por permiso o recurso):

| Ruta | Para |
| --- | --- |
| `/workspace` | Inicio: accesos rápidos según permisos, ficha propia, estudiantes a cargo y «Mis cursos» |
| `/workspace/enrollment?year=…` | Inscripciones por ciclo (ciclo siempre en la URL) |
| `/workspace/academic?year=…` | Ciclos, períodos, grados y secciones |
| `/workspace/courses?year=…`, `/workspace/courses/{id}`, `/…/assignments`, `/…/grades` | Cursos, lista por fecha, actividades y libreta |
| `/workspace/grades?year=…` | Publicación de notas por período con corte de solvencia |
| `/workspace/finance?student=…`, `/workspace/finance/setup`, `/workspace/finance/clearance`, `/workspace/finance/receipts/{id}` | Caja, configuración y emisión, solvencia y recibo imprimible |
| `/workspace/my/students/{id}` | Portal del estudiante o encargado: calificaciones, tareas, inscripción y estado de cuenta según facultades |

`/workspace/people`: directorio de personas y familias (`people.read`; altas y vínculos con `people.manage`). `/workspace/people/{id}`: ficha, vínculos, facultades y cuenta.

`/workspace/access`: gestión persistente de cuentas para quien tenga `accounts.manage`. `/workspace/audit`: auditoría para `audit.read`. `/Identity/Account/Activate`: formulario del titular para usar un código de activación o recuperación. Ver `IDENTITY_AND_ACCESS.md` para entrega/verificación y vigencia. No enviar códigos mediante URLs.

Para editar Angular con recarga: mantener el host en 5080 y ejecutar `./scripts/Invoke-Web.ps1 start` en otra terminal; usar el puerto que indique Angular (habitualmente 4200). Su proxy dirige API, Identity y recursos al host. La revisión E2E usa el build estático servido por .NET, que es el camino de distribución.

Los cambios en `config/institution.json` requieren reiniciar mediante `dotnet run` o publicar el host otra vez; Angular no se recompila por un cambio de marca. Las claves desconocidas de contacto se dejan vacías.

## Verificación

```powershell
dotnet test --verbosity quiet
./scripts/Invoke-Web.ps1 exec playwright install chromium
./scripts/Invoke-Web.ps1 test
git diff --check
```

Las pruebas de integración SQL crean y eliminan **sus propias bases aleatorias** `SchoolPlatform_Test_*`. Si no se usa SQL local, definir `SCHOOLPLATFORM_TEST_SQL`; el fixture reemplaza siempre el catálogo de esa conexión por su nombre aleatorio. Nunca elimina el catálogo facilitado por el operador. Debe disponer de permiso de crear/eliminar bases en la instancia de pruebas. Sin SQL, ejecutar solo `dotnet test --filter 'Category!=SqlServer'` o `Verify.ps1 -WithoutSql` y reportar claramente que SQL y E2E de cuentas se omitieron.

Para pasar argumentos a Playwright (por ejemplo `--repeat-each 2` o un archivo concreto) usar desde `web/`: `../.tools/node/node_modules/node/bin/node.exe node_modules/@playwright/test/cli.js test <filtro> --repeat-each 2`; el wrapper npm consume las opciones con guiones. El servidor de pruebas muestra su salida (`stdout: pipe`) para diagnosticar errores 500.

Playwright inicia un host en 5085 con una **base separada** `SchoolPlatform_E2E_<uuid>`. `tests/SchoolPlatform.TestHarness` crea su esquema y un gestor sintético con contraseña aleatoria en memoria; la base se elimina al terminar. `SCHOOLPLATFORM_E2E_SQL` permite otra instancia de pruebas. No configura ni consulta `SchoolPlatform_Development`. El harness no se distribuye con el host. Si se interrumpe abruptamente el proceso, revisar la base exacta antes de limpiarla; no ejecutar borrados generales por prefijo.

Las capturas y trazas quedan en `web/test-results`, ignorado por Git. Axe revisa las rutas visibles en escritorio y móvil; no equivale a auditoría completa WCAG ni pruebas con lector de pantalla. Se prueban altas/activación, recuperación real, denegación a cuentas básicas y auditoría contra SQL.

## Publicación técnica (todavía no lanzamiento)

```powershell
./scripts/Invoke-Web.ps1 run build
dotnet publish src/SchoolPlatform.Host -c Release -o .local/publish
```

Se distribuyen `config/institution.json`, recursos y web compilada. Cada colegio recibe su propio artefacto, configuración, base y secretos. Definir conexión/certificados/Data Protection y aplicar migraciones por un proceso de despliegue revisado. No usar la cuenta SQL de migración para ejecutar la web. No activar `Development` en una publicación real: habilita la muestra y cookies HTTP locales.

Después de revisar/aplicar migraciones, provisionar el primer gestor mediante un comando de consola con la conexión de esa instalación: `./scripts/Provision-Owner.ps1 -Environment Production`. Pide credenciales; no modifica usuarios existentes ni migra bases. Para un artefacto ya publicado, ejecutar `dotnet SchoolPlatform.Host.dll --provision-owner` desde su directorio con `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__SchoolDatabase`, `Bootstrap__Username` y `Bootstrap__Password` suministrados por el entorno seguro, y retirar estos dos últimos al terminar. No incluirlos en argumentos, scripts versionados o logs.

`/health/live` solo confirma que vive el proceso. Readiness SQL, backups/restauración y monitoreo se agregan antes del piloto. Los comandos de preparación automática se rechazan en Production.
