# Progreso

Actualizado: 4 de noviembre de 2026, America/Guatemala.

## Alcance vigente

**Un proyecto y una base independientes por colegio.** El propietario aclaró expresamente que copiará este proyecto para otro colegio y cambiará su nombre/configuración. No construir SaaS, selección de clientes, tenants ni membresías entre instituciones. La marca está en un único `config/institution.json`, editable por el desarrollador, sin panel administrativo.

## Implementado

- Solución .NET 10 con SDK estable fijado, paquetes lock y advertencias como errores; web Angular 22 con TypeScript/templates estrictos y dependencias fijadas.
- Configuración institucional tipada/validada: nombre, sede, lema, contactos, dirección, cinco recursos gráficos independientes, tema, región, términos y campos documentales privados. Contactos desconocidos vacíos.
- Misma marca en Angular y Razor; proyección pública explícita y archivos fuera del directorio web. Ningún nombre Leadership/Mixco en componentes, plantillas o lógica; solo en la configuración y recursos de esta institución.
- Bienvenida, login/logout real, cookies/CSRF, bloqueo de cinco intentos y política de cuenta activa, incluida desactivación con una cookie existente.
- EF Core/SQL Server, migraciones y comandos separados de preparación local. Base creada `SchoolPlatform_Development`; sin usuarios precargados ni contraseña predeterminada.
- `/workspace` protegido con estado inicial honesto; no simula módulos escolares.
- Muestra visual `/preview` y directorio sintético con búsqueda/filtros; API de muestra únicamente en Development.
- Responsive, contraste corregido, tabla desplazable accesible por teclado y menú móvil con foco, Escape y fondo inerte.
- Cálculo académico puro por materia: cuatro bimestres, pendiente distinto de cero, mínima 60 y recuperación que conserva ordinaria/reemplaza efectiva. Sin publicación ni política pendiente activada.
- Documentación, ADR, scripts de ejecución/verificación, fuentes originales copiadas y handoff.

## Validación ejecutada

| Comprobación | Resultado |
| --- | --- |
| `dotnet test --verbosity quiet` | 18 correctas, 0 omitidas; incluye 5 pruebas con SQL Server real |
| `Invoke-Web.ps1 run build` | Correcto con modo producción de Angular y tipos estrictos |
| `Invoke-Web.ps1 test` | 8 correctas: escritorio 1440 y móvil 390, navegación, filtros, ingreso, fallos y teclado |
| Axe en bienvenida/login/muestra/directorio/error | Sin infracciones detectadas en etiquetas WCAG 2 A/AA y 2.1 AA evaluadas; no equivale a certificación completa |
| Revisión visual PNG | Bienvenida, ingreso, panel y directorio revisados en las capturas; pantallas sin estilos detectadas y corregidas antes del cierre |
| `dotnet restore --locked-mode` | Correcto |
| `dotnet list package --vulnerable --include-transitive` | Sin vulnerabilidades reportadas por las fuentes consultadas |
| Instalación npm | Sin vulnerabilidades reportadas en la instalación verificada |
| `dotnet publish … -c Release -o .local/publish` | Correcto |
| Smoke del artefacto publicado en 5086 | HTML y JavaScript 200; marca correcta y hash del bundle igual al build actual. Proceso de prueba detenido |
| `git diff --check` | Sin errores de whitespace; Git avisa conversión LF/CRLF según configuración local |

Las bases aleatorias de integración se crearon y eliminaron mediante el fixture. En la base de desarrollo se comprobó `AspNetUsers` con cero cuentas y ausencia de `SchoolMemberships`. El historial conserva la migración aplicada inicialmente y su simplificación; el modelo actual no tiene esa tabla.

No se modificó el PHP, no se arrancó su MariaDB y no se importaron registros reales. No se creó commit, PR ni publicación externa.

Al cerrar este incremento quedó ejecutándose el host de desarrollo en `http://localhost:5080`. La muestra está en `/preview`; el proceso puede detenerse al cerrar la sesión y se recupera con `Start-Development.ps1 -SkipBuild`.

## Prototipo móvil Flutter (5 de noviembre de 2026)

Prototipo navegable en `mobile/` con **datos ficticios en memoria**; no se conecta a la API ni a SQL. Ver `mobile/README.md` y ADR-004.

- Perfiles: estudiante (inicio, cursos, agenda/tareas con entrega, notas, avisos), familia (dos hijos, notas con bloqueo por solvencia, estado de cuenta, comprobantes), docente (clases del día, asistencia, libreta por bimestre, revisión), administración (indicadores, directorio/expediente, matrícula con incidencias, anuncios, cuentas y códigos de un uso) y caja (resumen, cobro con vista previa e idempotencia, recibos, validación de comprobantes, movimientos).
- Marca desde `config/institution.json` mediante `mobile/tool/sync_institution.dart`; tema claro/oscuro, navegación adaptable (barra inferior / riel lateral) y estados de carga, vacío y error con reintento.

| Comprobación (Linux, Flutter 3.47.9) | Resultado |
| --- | --- |
| `flutter analyze` con reglas estrictas | Sin observaciones |
| `flutter test` | 62 correctas: unitarias de configuración, formatos, solvencia, pagos idempotentes, entregas, libreta y códigos; navegación de los 5 perfiles en 390, 320 y 1024 px y con texto al 130 %; botón atrás del sistema (no cierra la app, vuelve a la primera pestaña, doble pulsación para salir, confirmación con cambios sin guardar); flujos completos |
| `flutter build web --release --no-web-resources-cdn` | Correcto |
| Recorrido Playwright de la build web (390×844 y 1366×860), capturas revisadas | Sin errores de consola tras corregir un desbordamiento de `1 << 32` en web y etiquetas largas de navegación |

No se probó en dispositivos/emuladores Android o iOS ni se compiló APK: el entorno bloquea la descarga del SDK de Android. El botón atrás se verificó con el canal de plataforma simulado en pruebas de widgets y el atrás del navegador en la build web. Las capturas de QA no se versionan.

## Próximo bloque

Completar IAM: permisos por acción/recurso, auditoría durable, activación por invitación y recuperación asistida verificada sin depender exclusivamente del correo. Después implementar personas/encargados/ciclo/matrícula; luego finanzas/solvencia antes de abrir consulta de notas. Ver `ROADMAP.md` y `HANDOFF.md`.

La fase 1 del plan maestro sigue **parcial**. No existen todavía directorio persistente, matrícula, cobros, tareas, notas publicadas, boletas/PDF, promoción, importador ni Redis. Flutter existe solo como prototipo con datos ficticios (`mobile/`), sin integración con la API. La muestra del directorio no reemplaza su implementación real.
