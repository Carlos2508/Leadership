# Carnets y asistencia de portería

Implementación y decisiones: ADR-009. No confundir con asistencia por curso (`ATTENDANCE.md`).

## Flujos web

- `/workspace/gate`: lector de teclado/USB (código + Enter), dirección y modo, resultado con foto cuando exista, marcas recientes, búsqueda y registro manual con motivo. Inicia en solo entrada; automático es una selección explícita.
- `/workspace/gate/report`: fecha institucional, totales, filtro local por nombre/código/grupo, historial de cada estudiante y anulación con motivo. «Sin marcas» no declara una ausencia académica. El reporte no se actualiza automáticamente; «Consultar» lee el estado confirmado.
- `/workspace/gate/devices`: registrar teléfono, mostrar QR una sola vez y revocar con motivo. Al cerrar el QR no se puede recuperar su secreto: se registra otro dispositivo y se revoca el anterior si hace falta.
- `/workspace/people/{id}`: foto privada y, para estudiantes, PDF de carnet en tarjeta/carta; reemplazo con motivo separado de imprimir. En `/workspace/enrollment?year=…`, seleccionar grado/sección habilita impresión masiva (hasta 400 carnets por PDF).
- `/workspace/my/students/{id}`: entradas y salidas de los últimos 45 días, por facultad `AcademicRecords` o titular.

## Configuración del servidor

Sección `Gate` de configuración de instalación (variables de ambiente/secretos, fuera de marca):

| Opción | Valor inicial | Uso |
| --- | --- | --- |
| `PublicUrl` | Vacío | Origen HTTPS accesible desde el teléfono incluido en el QR. Si falta, usa el origen de la petición del administrador; configurar explícitamente detrás de proxy |
| `DuplicateSeconds` | 120 | Ventana para lecturas repetidas/cambio inmediato de dirección |
| `AutoExitAfterMinutes` | 30 | Estancia mínima para alternar entrada a salida en automático |
| `MaxPhotoBytes` | 2097152 | Límite de foto en servidor; UI admite hasta 2 MB |

`Gate__PublicUrl=https://<servidor-del-colegio>` es un ejemplo de formato, no un valor predeterminado. El lector Flutter rechaza HTTP; no desactivar TLS ni validación de certificado. Desarrollo físico requiere un origen HTTPS confiable y accesible. La SPA Angular y los lectores de teclado funcionan en el host local HTTP de Development.

Dispositivos: `GET /api/v1/gate/device/whoami`, `POST /api/v1/gate/device/scan` con `Authorization: Bearer …`. Cuerpo de lectura: `{code,mode}`. La credencial no puede leer reportes, personas, notas ni caja. Los demás endpoints `/api/v1/gate/*` requieren cuenta activa y el permiso correspondiente.

Las fotos viven en `Storage:Root/photos`, fuera del sitio público. Incluir el almacenamiento privado en el respaldo; no basta con copiar la base. No guardar QR de emparejamiento en capturas, logs o documentación.

## Lector Flutter

En el ingreso del prototipo: «Lector de portería», sin iniciar una cuenta ficticia. Registrar teléfono desde web, escanear QR, confirmar servidor, comprobar nombre de institución/dispositivo. Seleccionar entrada/salida/automático y escanear QR o Code 128 del carnet, o escribir código. La cámara se activa solo al pedirlo y se desmonta en segundo plano; se restablece al volver si estaba activa.

El teléfono necesita conexión: no guarda una cola de marcas ni usa datos ficticios. Si no hay confirmación, revisar el reporte antes de repetir. Un dispositivo revocado debe registrarse de nuevo. «Desvincular» borra el acceso local; la revocación central se realiza desde la web. Android/iOS almacenan la credencial con `flutter_secure_storage`; Android excluye la app de backup automático. En web solo vive en la pantalla y se pierde al salir.

## Validación y pendientes físicos

`GateSqlTests`: permisos, vínculo revocado, duplicados/concurrencia, direcciones, reemplazo simultáneo transaccional, auditoría, fotos privadas, dispositivos revocados, PDF y Code 128. `e2e/gate.spec.ts`: flujo completo web y familiar en escritorio/móvil, Axe y capturas sin el QR privado. Flutter: cliente HTTP con respuestas de prueba y widgets de emparejamiento, credencial oculta, lectura, revocación y regreso al ingreso.

Antes del piloto: imprimir CR80/carta al 100 % (sin ajustar a página), verificar frente/reverso, márgenes y lectores reales; probar cámara/permisos y almacenamiento seguro en Android/iOS reales. Confirmar elegibilidad QuestPDF, modo/tiempos y procedimiento de salida con el colegio. La alternancia automática no demuestra autorización para recoger al estudiante.
