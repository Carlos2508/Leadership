# App móvil (prototipo Flutter)

Prototipo navegable de la app para estudiantes, familias, docentes, administración y caja. **Todos los datos son ficticios** y viven en memoria: no hay conexión con la API ni con SQL Server, y nada se guarda al cerrar la app. No representa módulos implementados en el backend (ver `docs/PROGRESS.md`). Decisión y límites: [ADR-008](../docs/adr/ADR-008-PROTOTIPO-FLUTTER.md).

## Requisitos

- Flutter estable 3.47 o posterior (Dart 3.13). Validado con Flutter 3.47.9.
- Para Android: Android Studio/SDK. Para iOS: macOS con Xcode.

## Primer uso

Desde la carpeta `mobile/`:

```bash
flutter pub get
dart run tool/sync_institution.dart   # copia la marca desde config/institution.json
flutter run                           # dispositivo o emulador conectado
flutter run -d chrome                 # también funciona en navegador
```

`tool/sync_institution.dart` lee el **único** `config/institution.json` (más `config/institution.local.json` si existe), copia solo la proyección pública (sin `Documents`) y los logos de `src/SchoolPlatform.Host/wwwroot/branding/` hacia `assets/institution/`. Esa carpeta está ignorada por Git. Si falta, la app muestra una pantalla neutra que explica cómo generarla; nunca inventa una marca.

Para otro colegio: editar `config/institution.json`, volver a ejecutar la sincronización y ajustar el nombre visible e íconos de Android/iOS (`android/app/src/main/AndroidManifest.xml`, `ios/Runner/Info.plist`, `org.schoolplatform` como identificador de paquete).

## Perfiles de demostración

En la pantalla de ingreso, elige un perfil y presiona **Iniciar sesión** (cualquier contraseña de 6 caracteres o más):

| Perfil | Usuario | Qué muestra |
| --- | --- | --- |
| Estudiante | `estudiante.demo` | Inicio, cursos, agenda y tareas con entrega, notas publicadas, avisos |
| Familia | `familia.demo` | Dos hijos; uno con notas bloqueadas por solvencia, estado de cuenta, envío de comprobantes |
| Docente | `docente.demo` | Clases del día, asistencia, libreta por bimestre, revisión de entregas |
| Administración | `direccion.demo` | Indicadores, directorio y expediente, matrícula con incidencias, anuncios, cuentas y códigos de un uso |
| Caja | `caja.demo` | Resumen, cobro con vista previa e idempotencia, recibos, validación de comprobantes |

Las acciones se reflejan entre perfiles mientras la app esté abierta: si caja acepta el comprobante de la familia, las notas de Diego se habilitan (deslizar hacia abajo para actualizar). En **Perfil → Opciones del prototipo** se pueden simular errores de red para ver los estados de error y reintento.

## Navegación y botón atrás

- **Atrás del sistema (Android) en una pestaña secundaria:** vuelve a la primera pestaña del perfil.
- **Atrás en la pestaña de inicio, la bienvenida o el ingreso:** muestra "Presiona atrás otra vez para salir"; solo una segunda pulsación dentro de 2 s cierra la app.
- **Atrás en un detalle, hoja inferior o diálogo:** cierra solo esa capa y conserva la pestaña y su estado.
- **Formularios con datos sin guardar** (comprobante, cobro, entrega de tarea, revisión, libreta, asistencia, anuncio, recuperación): piden confirmación antes de descartar, también con el gesto de iOS y la flecha superior.
- **Volver a tocar la pestaña activa:** desplaza la lista al inicio.
- Android 14+: atrás predictivo habilitado (`enableOnBackInvokedCallback`). En web, la URL refleja los detalles y el botón atrás del navegador funciona.

## Arquitectura

```
lib/
  app/          enrutador (go_router, rutas por perfil) y contenedor de navegación adaptable
  core/         configuración institucional, tema, formatos, utilidades y componentes visuales
  domain/       modelos y contratos de repositorio (sin Flutter)
  data/         implementación demo en memoria (DemoDatabase) y proveedores de repositorios
  features/     funcionalidades: auth, academics, student, guardian, teacher, finance,
                administration, notices, profile (application = estado, presentation = pantallas)
  shared/       piezas de presentación compartidas entre funcionalidades
```

- Estado con Riverpod 3; las pantallas dependen solo de las interfaces de `domain/repositories`. Para conectar la API real se sustituyen los proveedores de `data/repository_providers.dart`.
- La app **no calcula** aprobación, promedios ni solvencia: muestra lo que responde el servidor (aquí, el servidor simulado). Una respuesta de notas bloqueadas no contiene notas.
- Ocultar pantallas por perfil es solo navegación; la autorización real es responsabilidad del backend.
- Marca, colores, vocabulario (`Terms`) y región salen de la configuración. Los colores funcionales del sistema de diseño (azul, morado, coral, estados) son del producto.
- Tipografía Inter (licencia OFL en `assets/fonts/OFL.txt`) en versiones estáticas reducidas a caracteres latinos.

## Verificación

```bash
flutter analyze            # reglas estrictas, sin advertencias
flutter test               # unitarias, navegación por perfil (3 tamaños y texto grande), botón atrás y flujos
flutter build web --release --no-web-resources-cdn
```

Las pruebas usan una configuración institucional neutra y un reloj fijo; no dependen de la marca sincronizada.

## Pendiente para una app real

Autenticación OIDC con PKCE y tokens en almacenamiento seguro, cliente HTTP con contratos de la API, carga real de archivos/cámara, notificaciones push, caché por usuario con borrado al cerrar sesión, pruebas en dispositivos Android/iOS físicos y accesibilidad con lector de pantalla.
