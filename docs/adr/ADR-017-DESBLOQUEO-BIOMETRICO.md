# ADR-017: desbloqueo biométrico del app conectado

Fecha: 6 de noviembre de 2026. Adoptado por solicitud del propietario. Amplía ADR-014 conservando Identity y SignInFlow.

## Decisión

Después del ingreso completo (contraseña y MFA cuando corresponde), la persona puede activar **Desbloquear con huella** en su perfil del app Android/iOS. Se guarda exclusivamente la misma cookie de Identity en un almacén protegido por biometría; nunca la contraseña, códigos TOTP/recuperación, antiforgery ni plantillas de huellas/rostro. El estado no secreto de activación y la dirección del colegio pueden estar en preferencias. Web, demostración y portería conservan sus flujos separados.

`POST /api/v1/auth/remember-device` exige sesión `ActiveAccount` y CSRF. Reemite el mismo ticket/principal/esquema de Identity con caducidad absoluta de 14 días, sin renovación deslizante. No introduce refresh tokens ni otro login. La validación durable de sello, cuenta activa y autorización sigue ejecutándose en el servidor. Una sesión pendiente de segundo factor no puede recordar el dispositivo. `forget-device` devuelve la sesión a 30 minutos y comportamiento ordinario, también con sesión/CSRF y auditoría sin secretos.

El app pide al sistema desbloquear el registro protegido antes de restaurarlo. Después consulta `/session`, `/me` y `/me/courses`; no da acceso con una copia local de permisos o datos ni abre contenido en modo desconectado. Cancelar conserva el bloqueo. Un fallo de red permite reintentar sin borrar el ticket; una sesión vencida/revocada exige ingreso completo y elimina el recordatorio. Cerrar sesión, cambiar de cuenta o desactivar la opción elimina el ticket local. Al reiniciar, si se activó, se abre directamente el acceso con el botón de huella; al perder el primer plano se cubren todas las rutas y se exige desbloqueo antes de continuar.

Almacén `flutter_secure_storage` 11.2.0 separado del de portería. Android: namespace propio, biometría fuerte obligatoria y autenticación por operación; iOS: Keychain con código de dispositivo y `biometryCurrentSet`. `local_auth` 3.0.2 consulta disponibilidad/enrolamiento, sin leer ni comparar biometría en Dart. Se conserva la autenticación por contraseña si el dispositivo no admite el mecanismo o lo invalida. El respaldo Android permanece deshabilitado; el ticket iOS se protege localmente y no se sincroniza.

Android usa `FlutterFragmentActivity`, permiso `USE_BIOMETRIC` y tema AppCompat; iOS declara el propósito de Face ID. Los requisitos y límites de plataforma proceden de las fuentes del mantenedor: [local_auth](https://pub.dev/packages/local_auth), [configuración Android](https://pub.dev/packages/local_auth_android), [almacenamiento protegido](https://pub.dev/packages/flutter_secure_storage), [controles de Keychain](https://pub.dev/documentation/flutter_secure_storage/latest/flutter_secure_storage/IOSOptions-class.html). La política de biometría fuerte de Android depende de las capacidades/versiones del sistema; no se sustituye silenciosamente por una contraseña guardada.

## Validación y operación

Pruebas con almacén/servidor sintéticos: consentimiento, no persistencia de contraseñas/antiforgery, reinicio, cancelación, red, revocación, MFA, salida y desactivación. Prueba de interfaz: cubrir contenido al pasar a segundo plano y desbloquear solo después de lectura protegida más validación del servidor. Prueba SQL/HTTP del ticket normal/persistente, CSRF, MFA pendiente y revocación del sello.

La biometría real y los cambios de huellas/Face ID deben comprobarse en dispositivos Android/iOS compatibles. Un build web, un mock o un APK compilado no acreditan una lectura de huella física. La sesión absoluta de 14 días puede exigir de nuevo contraseña/MFA al vencer, cambiar accesos o cerrar sesiones desde el colegio.
