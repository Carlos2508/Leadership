# Configuración del colegio

**Un proyecto y una base de datos por colegio.** Para otro colegio se copia este proyecto y se cambia una configuración; no hay SaaS, selector de cliente ni tenants.

La fuente de verdad es `config/institution.json`. El host lee y valida `InstitutionOptions`; Angular y Razor reciben la misma marca. El administrador no tiene formulario ni API para cambiarla.

| Grupo | Configurable |
| --- | --- |
| Identidad | Nombre público/corto, denominación legal, sede, lema, bienvenida e identificador estable de la institución |
| Contacto | Teléfono, correo, dirección y sitio HTTPS; `DialPrefix` (+502) para llamar con un toque, `Phones` adicionales con etiqueta, `WhatsApp` (solo dígitos con código de país), `OfficeHours`, `MapQuery` (texto que se busca en Google Maps) y `Social` (URLs HTTPS) |
| Página pública | `Public.HeroTitle/HeroAccent/HeroDescription`, `AboutTitle/About`, misión y visión, `Highlights`, `LifeTitle/LifeText/Programs`, `DigitalTitle/DigitalText`, admisión y `AdmissionSteps`, comunidad, `QuestionsTitle/Questions`, `ContactTitle/ContactText`; secciones opcionales vacías se ocultan |
| Recursos | Logo principal, de acceso, de comprobantes, de boletas, de carnet y favicon; imágenes opcionales `HeroImage`, `StudentImage`, `CommunityImage`, con rutas independientes bajo `/branding/` |
| Documentos | NIT y pies para comprobantes/boletas, reservados al servidor |
| Tema | Color profundo/lateral, primario, acento y superficie en formato `#RRGGBB` |
| Región | Cultura, zona horaria IANA y moneda ISO |
| Vocabulario | Etiquetas de estudiantes, encargados, colegiatura y bimestre; el español genérico de la interfaz sigue siendo código del producto |

Los contactos desconocidos quedan vacíos y no aparecen en pantalla. Comprobantes, cartas de notas y carnets conservan sus recursos de impresión independientes.

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

## Página pública (`/`)

La referencia vigente (ADR-013) incluye portada con curva e imagen, pilares, misión/visión, vida escolar, admisión, comunidad, preguntas y contacto. Cada entrada del menú llega a una sección real. Todo texto institucional sale de la configuración, sin editor administrativo ni instrucciones técnicas en la página. El mapa de Google se carga **solo al pulsar «Ver mapa»** (CSP `frame-src https://www.google.com`); antes no hay conexión con terceros. «Cómo llegar» abre Google Maps con `MapQuery`.

Teléfono, correo y dirección proceden de los carnets aportados; el teléfono y la dirección coinciden con el [listado público de MINEDUC consultado](https://infopublica.mineduc.gob.gt/mineduc/images/c/c8/DIGEACE_DIGEACE_GUATEMALA_INCISO2C_2023_VERSION09.pdf). WhatsApp e Instagram reproducen el paquete entregado. No se agregaron horarios, tarifas ni niveles no comprobados. Misión, visión y presentación nuevas son redacción autorizada para esta solicitud, no documentos oficiales investigados. Los enlaces del pie para privacidad/términos son consultas por correo, no políticas publicadas.

Las imágenes aportadas son ilustrativas, como indica el paquete. Las rutas, procedencia y licencia de las fuentes se documentan en `reference/REFERENCE_DESIGN_ASSETS.md`. Para otra instalación se deben sustituir todos estos contenidos y recursos en la misma configuración, incluidos los campos nuevos de `Public`.

## Documentos

`Institution:Documents:PassingScore` (opcional, 0–100): nota mínima aprobada por el colegio. Solo si se define, los boletines colorean cada resultado como aprobado o por reforzar y muestran la leyenda. Vacío = sin juicio de aprobación (política pendiente). El diseño de los PDF está en `docs/DESIGN.md`.
