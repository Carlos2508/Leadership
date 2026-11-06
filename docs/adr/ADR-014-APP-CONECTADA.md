# ADR-014: app Flutter conectada con la misma sesión de la web

Estado: adoptado (6 de noviembre de 2026). Complementa ADR-008 (prototipo) y ADR-009 (lector de portería).

## Contexto

El propietario pidió que el app tenga el mismo diseño que la web y pueda hacer lo mismo. El prototipo usa datos ficticios en memoria; solo el lector de portería consume la API real con credencial de dispositivo. Toda la autorización escolar vive en el servidor (cookie de Identity, `ActiveAccount`, permisos SQL por recurso, MFA, sellos de seguridad y auditoría). Reproducirla en otro esquema de tokens duplicaría reglas y riesgos.

## Decisión

- El app nativo inicia sesión con `POST /api/v1/auth/login` y, si la cuenta tiene autenticador, `POST /api/v1/auth/login/second-factor`. Ambos llaman a `SignInFlow`, el mismo servicio que usan los formularios Razor (`Login`, `LoginWith2fa`, `Logout`). Resultado: la misma cookie `SchoolPlatform.Session`, con su caducidad deslizante de 30 minutos y validación del sello de seguridad.
- Antifalsificación igual que la web: `GET /api/v1/auth/antiforgery` (anónimo) entrega el token para el ingreso; después de iniciar sesión el app pide `GET /api/v1/antiforgery` y envía `X-XSRF-TOKEN` en cada escritura. Los endpoints de ingreso usan la política de límite `identity-forms`.
- Las respuestas de fallo no revelan si el usuario existe o está inactivo; el bloqueo responde 423 y la auditoría registra los mismos eventos que la web.
- El app no recibe permisos propios: muestra módulos según `/api/v1/session` (permisos efectivos, estudiante vinculado, perfil escolar) y cada consulta sigue autorizada por el servidor. Ocultar una pantalla no es autorización.
- La cookie se guarda solo en memoria del proceso del app: al cerrarlo se vuelve a ingresar. No se persisten contraseñas. La dirección del servidor debe ser HTTPS salvo `localhost` en desarrollo, igual que el lector de portería.
- El modo de demostración con datos ficticios se conserva separado y marcado; nunca se usa como respaldo ante errores de la API.
- La credencial `GateDevice` sigue limitada a `whoami` y `scan`; no se reutiliza para cuentas de personas.

## Consecuencias

- Una sola lógica de ingreso para web y app; las pruebas `AppAuthSqlTests` y `MfaSqlTests` cubren ambos caminos.
- OIDC + PKCE queda como evolución posible si se agrega un proveedor de identidad externo; no es necesario para el modelo actual de una instalación por colegio.
- Flutter web solo puede usar la API conectada desde el mismo origen del host; fuera de él queda en modo demostración.
- Pendiente: pruebas en dispositivos físicos Android/iOS y cobertura del resto de módulos escritos (ver `HANDOFF.md`).
