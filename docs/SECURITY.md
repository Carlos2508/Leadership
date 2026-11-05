# Seguridad implementada y límites actuales

Autenticación mediante ASP.NET Core Identity en SQL Server. Razor Pages propias para ingreso/salida, sin registro público ni cuentas/contraseñas predeterminadas. Contraseñas nuevas con hash adaptable de Identity; ningún hash PHP se importa.

- Cookie HttpOnly, SameSite Lax, vencimiento de 30 minutos y Secure fuera de Development. No se guardan credenciales en Angular/localStorage.
- CSRF en formularios de ingreso/salida. La salida es POST; GET solo presenta confirmación. Ningún `ReturnUrl` externo produce redirección.
- Activación/recuperación mediante código de un uso, con hash en SQL, vencimiento, revocación y POST antiforgery. Sin secreto en URLs o listados. Los formularios públicos de ingreso/activación aceptan como máximo 16 KiB y tienen límite de 120 solicitudes por minuto por IP en esta instancia.
- Cinco fallos bloquean la cuenta durante quince minutos; el mensaje no revela si existe o está bloqueada.
- Cuenta inactiva no inicia sesión. `ActiveAccount` consulta el estado durable en cada solicitud privada; una cookie existente no evita una desactivación.
- Las cookies comparan directamente el sello de seguridad por petición, incluso si no avanzó el reloj. Recuperación/cierre de sesiones/cambios de perfil invalidan las cookies previas. Los permisos `accounts.manage` y `audit.read` se vuelven a comprobar en SQL y se exigen también al servicio de escritura.
- Último gestor activo protegido frente a suspensión o retiro de su permiso; las cuentas pendientes no cuentan para esa protección. Canje de códigos y cambios de acceso serializados en una transacción SQL corta; evento de auditoría y cambio se confirman juntos.
- Auditoría durable de operaciones y acceso, sin contraseña/código/hash. Los ingresos rechazados no se atribuyen al titular como actor. EF y un trigger SQL impiden modificar/borrar eventos; un dueño del esquema todavía puede alterar esos controles.
- 401 para anónimo, 403 para autenticado sin acceso. Fallback de autorización para endpoints nuevos, y política explícita en las API privadas existentes.
- Configuración pública mediante proyección explícita; sin conexión SQL, secretos o campos tributarios. API y páginas de identidad usan `Cache-Control: no-store`.
- CSP con scripts del mismo origen, `frame-ancestors 'none'`, `nosniff` y referencia limitada. Se desactivó CSS crítico inline de Angular para evitar el script inline de activación de estilos; no se habilitó `unsafe-inline` para scripts. Los estilos sí lo permiten para variables/componentes Angular.
- En Production: HTTPS/HSTS, ninguna API de muestra. En desarrollo HTTP solo se enlaza localmente mediante los scripts provistos.

Esto no es una certificación ni declara la plataforma lista para publicar. Faltan MFA con alta/challenge, permisos sobre expedientes y relaciones familiares reales, almacenamiento privado, retención, endurecimiento de operación y aceptación de los módulos escolares. Una cuenta marcada con segundo factor no se autentica mediante el formulario simple; no se omite MFA. La verificación de identidad de recuperación es una actuación institucional documentada, no una validación automática de documentos. Ver `IDENTITY_AND_ACCESS.md`.

Para producción definir conexión con certificado validable y usuario de ejecución con privilegios mínimos; migraciones con otra identidad. `TrustServerCertificate=true` aparece solo en Development y fixtures. Configurar Data Protection durable y protegida según hosting. El proceso web no migra ni crea cuentas al arrancar. `--provision-owner` es un comando de consola separado: exige base ya migrada, credenciales suministradas explícitamente y ausencia de gestores activos. Los comandos de preparación automática de Development siguen rechazados en Production.
