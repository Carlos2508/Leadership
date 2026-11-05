# Seguridad del primer incremento

Autenticación mediante ASP.NET Core Identity en SQL Server. Razor Pages propias para ingreso/salida, sin registro público ni cuentas/contraseñas predeterminadas. Contraseñas nuevas con hash adaptable de Identity; ningún hash PHP se importa.

- Cookie HttpOnly, SameSite Lax, vencimiento de 30 minutos y Secure fuera de Development. No se guardan credenciales en Angular/localStorage.
- CSRF en formularios de ingreso/salida. La salida es POST; GET solo presenta confirmación. Ningún `ReturnUrl` externo produce redirección.
- Cinco fallos bloquean la cuenta durante quince minutos; el mensaje no revela si existe o está bloqueada.
- Cuenta inactiva no inicia sesión. `ActiveAccount` consulta el estado durable en cada solicitud privada; una cookie existente no evita una desactivación.
- 401 para anónimo, 403 para autenticado sin acceso. Fallback de autorización para endpoints nuevos, y política explícita en las API privadas existentes.
- Configuración pública mediante proyección explícita; sin conexión SQL, secretos o campos tributarios. API y páginas de identidad usan `Cache-Control: no-store`.
- CSP con scripts del mismo origen, `frame-ancestors 'none'`, `nosniff` y referencia limitada. Se desactivó CSS crítico inline de Angular para evitar el script inline de activación de estilos; no se habilitó `unsafe-inline` para scripts. Los estilos sí lo permiten para variables/componentes Angular.
- En Production: HTTPS/HSTS, ninguna API de muestra. En desarrollo HTTP solo se enlaza localmente mediante los scripts provistos.

Esto es una base de seguridad, no certificación ni plataforma lista para publicar. Faltan autorización por permisos/recurso, invitaciones, recuperación asistida verificada, MFA para operadores sensibles, revocación de todas las sesiones, auditoría durable, limitación por IP, almacenamiento privado y pruebas de los recursos escolares reales. Hasta implementarlos no se abren datos de estudiantes ni operaciones financieras.

Para producción definir conexión con certificado validable y usuario de ejecución con privilegios mínimos; migraciones con otra identidad. `TrustServerCertificate=true` aparece solo en Development y fixtures. Configurar Data Protection durable y protegida según hosting. No aplicar migraciones automáticas desde el proceso web ni activar comandos locales en Production.
