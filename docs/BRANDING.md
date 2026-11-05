# Configuración del colegio

**Un proyecto y una base de datos por colegio.** Para otro colegio se copia este proyecto y se cambia una configuración; no hay SaaS, selector de cliente ni tenants.

La fuente de verdad es `config/institution.json`. El host lee y valida `InstitutionOptions`; Angular y Razor reciben la misma marca. El administrador no tiene formulario ni API para cambiarla.

| Grupo | Configurable |
| --- | --- |
| Identidad | Nombre público/corto, denominación legal, sede, lema, bienvenida e identificador estable de la institución |
| Contacto | Teléfono, correo, dirección y sitio HTTPS |
| Recursos | Logo principal, de acceso, de comprobantes, de boletas y favicon, con rutas independientes |
| Documentos | NIT y pies para comprobantes/boletas, reservados al servidor |
| Tema | Color profundo/lateral, primario, acento y superficie en formato `#RRGGBB` |
| Región | Cultura, zona horaria IANA y moneda ISO |
| Vocabulario | Etiquetas de estudiantes, encargados, colegiatura y bimestre; el español genérico de la interfaz sigue siendo código del producto |

Los contactos desconocidos quedan vacíos y no aparecen en pantalla. Las claves de logos de comprobantes y boletas existen, pero todavía no hay generador de documentos.

## Editar esta instalación

Cambiar `config/institution.json`. Para valores de desarrollo no versionados, crear `config/institution.local.json` con solo las claves que se quieren sobrescribir:

```json
{
  "Institution": {
    "Contact": { "Phone": "TELÉFONO REAL", "Address": "DIRECCIÓN REAL" },
    "Documents": { "ReceiptFooter": "Texto institucional del comprobante" }
  }
}
```

Ejecutar nuevamente `dotnet run` o volver a publicar el host para copiar la configuración actualizada. No es necesario recompilar Angular cuando solo cambia la marca. `--no-build` usa los archivos que ya fueron copiados al directorio de salida; no detecta cambios en los originales.

## Entregar el proyecto a otro colegio

1. Copiar el repositorio sin `.local`, `.tools`, `bin`, `obj`, exportaciones ni secretos del colegio anterior.
2. Editar el **mismo** `config/institution.json`. `docs/examples/institution.example.json` sirve de referencia neutral, no como otro cliente ejecutable.
3. Sustituir los recursos públicos de `src/SchoolPlatform.Host/wwwroot/branding/` y ajustar las cinco rutas si sus nombres cambian. Eliminar recursos de la marca anterior que ya no se utilicen.
4. Asignar un UUID nuevo a `SchoolId` para identificar la nueva instalación. No usarlo para filtrar clientes; toda su base pertenece a un colegio.
5. Configurar una base SQL Server nueva y credenciales independientes. Aplicar migraciones y crear las cuentas que correspondan a esa instalación. No copiar la base ni usuarios del colegio anterior.
6. Compilar/publicar el host y verificar nombre, logos, favicon, colores y contactos. Cuando existan recibos y boletas, revisar también ambas plantillas.

Precedencia: appsettings → `institution.json` → `institution.local.json` → ambiente → argumentos. Los archivos de marca no contienen secretos. Los parámetros SQL se mantienen fuera de estos JSON.

El host falla si falta un nombre obligatorio, recurso o color válido. `/api/v1/bootstrap` expone una proyección explícita de marca, contacto y preferencias públicas; no devuelve configuración completa, conexión ni campos tributarios/documentales. Los JSON están fuera de `wwwroot`.

Un color hexadecimal válido no garantiza contraste: después de cambiar colores se revisan escritorio/móvil y las pruebas de accesibilidad. El escudo actual procede del kit conceptual del diseñador; conservar la atribución y sus límites de fidelidad.
