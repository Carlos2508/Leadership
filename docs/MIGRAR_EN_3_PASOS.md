# Migrar estudiantes: abrir y seguir tres pasos

Haz doble clic en **`Migrar estudiantes.cmd`**, en la raíz de esta rama. Se abrirá el asistente en el navegador. Ingresa con tu cuenta de gestión habitual; si tiene segundo factor, úsalo como siempre.

La primera apertura puede preparar el programa y pedirte el nombre de la base nueva, si la instalación todavía no tiene conexión configurada. Guarda únicamente servidor/nombre de base, nunca contraseñas. También puede pedirte aceptar el certificado HTTPS local. Después recuerda el destino y reutiliza su propia ventana de servidor; no detiene otros proyectos.

## 1. Origen y ciclo

Pulsa **Buscar plataforma anterior**. Reconoce la conexión del PHP local sin ejecutar ese archivo ni guardar o mostrar sus claves. Si no puede reconocerla, abre **Usar otra conexión**.

Elige el ciclo antiguo y el destino, por ejemplo **2027**. Ves cuántos alumnos tiene cada ciclo antiguo para evitar elegir uno vacío. Si el ciclo nuevo no existe, puedes crearlo ahí indicando sus fechas reales.

Pulsa **Preparar estudiantes y familias**.

## 2. Revisar

El asistente ya propone estudiantes, padres/madres, nombres/apellidos, códigos y cuentas pendientes. Elige el grado y la sección de destino **una vez por grupo**; ajusta solo las excepciones. No promueve alumnos automáticamente ni decide quién continúa a partir de notas antiguas.

Primero ves solo las excepciones; puedes mostrar el listado completo si quieres. Los nombres cortos o compuestos aparecen señalados; las familias que requieren revisión se abren solas. Puedes excluir un estudiante o adulto, corregir un nombre y dejar fechas pendientes desde la pantalla. Si el mismo encargado aparece en dos fichas antiguas, elige **Es la misma persona** para compartir una cuenta: no se fusiona por apellido o teléfono automáticamente.

Los datos familiares no identificados quedan pendientes, sin crear adultos ficticios. No se atribuyen teléfonos/correos compartidos a un adulto sin verificar su titular. Los vínculos no reciben permisos familiares automáticamente.

Marca que revisaste el padrón y pulsa **Comprobar y continuar**. Si necesitas salir antes, pulsa **Guardar y continuar después**; al abrirlo de nuevo aparece tu revisión.

## 3. Importar

Comprueba el resumen y pulsa **Importar estudiantes**. El asistente se encarga de:

- Crear y verificar un respaldo de la base nueva.
- Guardar estudiantes, encargados y matrícula del ciclo/grado/sección indicados.
- Preparar todas las cuentas **inactivas y sin contraseña ni códigos**.
- Verificar el resultado después de guardar.

Si el respaldo falla, no importa nada. Si falla la carga, revierte el lote completo. Si repites una carga ya terminada, conserva su resultado y no duplica personas ni desactiva cuentas habilitadas después.

No tienes que editar JSON, copiar identificadores, calcular hashes, ejecutar SQL ni hacer el respaldo manualmente. Las decisiones sobre continuidad, nombres ambiguos y grados siguen siendo tuyas; el asistente las presenta juntas para que sean fáciles de confirmar.

La entrega está probada con datos inventados. Todavía no se leyó ni importó el padrón real del colegio. La herramienta de consola anterior permanece disponible como opción avanzada en [MIGRATION_2027.md](MIGRATION_2027.md).
