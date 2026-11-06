# Mensajes y desbloqueo del app

## Abrir mensajes

Después de ingresar, busca el icono de conversación junto a la campana. El número indica mensajes sin leer. Al ingresar también aparece un aviso si tienes mensajes pendientes.

Elige el ciclo y pulsa **Nueva conversación**. Un estudiante puede elegir a los docentes de sus cursos. Un encargado puede elegirlos para cada estudiante con el que tenga vínculo y facultad de comunicaciones. Un docente puede escribir a esos estudiantes y encargados. Las conversaciones familiares y estudiantiles permanecen separadas.

Puedes buscar por nombre del contacto o del estudiante, volver al historial y ver mensajes anteriores. **Enviado** confirma que se guardó en el servidor; **Leído** indica que la otra persona abrió esos mensajes. Si falla la conexión, el texto se conserva: pulsa **Reintentar**. No vuelve a enviarse como otro mensaje.

Los mensajes se actualizan automáticamente mientras usas la plataforma. Los avisos de esta entrega aparecen dentro de la web/app; no son notificaciones de sistema con el app cerrado.

## Activar huella

1. Ingresa una vez al app del teléfono con tu cuenta y segundo factor, si lo tienes.
2. Abre tu perfil y activa **Desbloquear con huella**.
3. Confirma la huella o biometría del teléfono. En adelante, usa el botón de desbloqueo al abrirlo.

Tu contraseña no se guarda. Al salir a otra aplicación, tu espacio se bloquea. Puedes cancelar y reintentar o ingresar con contraseña. La opción se muestra cuando el teléfono admite biometría registrada; no se ofrece en la versión web.

La sesión recordada dura hasta 14 días. Si vence o el colegio cierra tus sesiones/cambia tus accesos, debes ingresar otra vez. Para desactivar la huella usa el mismo interruptor; **Cerrar sesión** también borra el recordatorio del teléfono.

## Preparar la instalación

El código incluye la migración aditiva `20261106225356_PrivateChat`. Aplica las migraciones mediante el procedimiento normal de actualización, con la configuración y respaldo de esa instalación, antes de servir la nueva versión. No se aplicó a la base escolar durante el desarrollo ni se usaron personas reales.

Para ver contactos deben existir cuentas activas vinculadas a las personas, matrícula/ubicación en el ciclo y asignación docente vigente. Para encargados también debe estar concedida **Recibir comunicaciones**. Un cargo llamado «Docente» o un rol administrativo no reemplaza esos registros.

Decisiones técnicas: [chat](adr/ADR-016-CHAT-PRIVADO.md) y [huella](adr/ADR-017-DESBLOQUEO-BIOMETRICO.md).
