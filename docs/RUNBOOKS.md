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

Abrir `http://localhost:5080`. El comando de operador pide usuario y contraseña en consola, nunca la imprime y limpia las variables del secreto al terminar. No existe contraseña predeterminada. Si el usuario ya existe, el comando rechaza modificarlo. El espacio autenticado todavía no contiene módulos escolares operativos.

`appsettings.Development.json` utiliza `localhost`, autenticación integrada y base nueva `SchoolPlatform_Development`. Si tu instancia es otra, definir `ConnectionStrings__SchoolDatabase` en la terminal de esa instalación. No apuntar al legado ni a una base ajena. Cada copia para otro colegio debe usar un nombre de base diferente en su propia configuración de desarrollo y secretos independientes en producción.

## Interfaz

`/`: bienvenida. `/Identity/Account/Login`: acceso real. `/workspace`: sesión protegida y estado inicial. `/preview` y `/preview/students`: muestra sintética solo Development.

Para editar Angular con recarga: mantener el host en 5080 y ejecutar `./scripts/Invoke-Web.ps1 start` en otra terminal; usar el puerto que indique Angular (habitualmente 4200). Su proxy dirige API, Identity y recursos al host. La revisión E2E usa el build estático servido por .NET, que es el camino de distribución.

Los cambios en `config/institution.json` requieren reiniciar mediante `dotnet run` o publicar el host otra vez; Angular no se recompila por un cambio de marca. Las claves desconocidas de contacto se dejan vacías.

## Verificación

```powershell
dotnet test --verbosity quiet
./scripts/Invoke-Web.ps1 exec playwright install chromium
./scripts/Invoke-Web.ps1 test
git diff --check
```

Las cinco pruebas de integración SQL crean y eliminan **sus propias bases aleatorias** `SchoolPlatform_Test_*`. Si no se usa SQL local, definir `SCHOOLPLATFORM_TEST_SQL`; el fixture reemplaza siempre el catálogo de esa conexión por su nombre aleatorio. Nunca elimina el catálogo facilitado por el operador. Debe disponer de permiso de crear/eliminar bases en la instancia de pruebas. Sin SQL, ejecutar solo `dotnet test --filter 'Category!=SqlServer'` y reportar claramente que la integración no se verificó.

Playwright inicia un host local de pruebas en 5085. Las capturas y trazas quedan en `web/test-results`, ignorado por Git. Axe revisa las rutas visibles en escritorio y móvil; no equivale a auditoría completa WCAG ni pruebas con lector de pantalla.

## Publicación técnica (todavía no lanzamiento)

```powershell
./scripts/Invoke-Web.ps1 run build
dotnet publish src/SchoolPlatform.Host -c Release -o .local/publish
```

Se distribuyen `config/institution.json`, recursos y web compilada. Cada colegio recibe su propio artefacto, configuración, base y secretos. Definir conexión/certificados/Data Protection y aplicar migraciones por un proceso de despliegue revisado. No usar la cuenta SQL de migración para ejecutar la web. No activar `Development` en una publicación real: habilita la muestra y cookies HTTP locales.

`/health/live` solo confirma que vive el proceso. Readiness SQL, backups/restauración y monitoreo se agregan antes del piloto. Los comandos de preparación automática se rechazan en Production.
