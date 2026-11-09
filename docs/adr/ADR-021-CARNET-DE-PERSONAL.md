# ADR-021: Carnet para todo el personal y rediseño del carnet

Fecha: 8 de noviembre de 2026. Estado: aceptado por solicitud del propietario (rama `feat/carnet-personal`).

## Contexto

Solo los estudiantes tenían carnet (ADR-009). El propietario pidió carnet para todas las personas del colegio, excepto padres y encargados, y aportó un rediseño del frente y del reverso como referencia.

## Decisión

- **Quién recibe carnet:** quien tiene perfil de estudiante recibe el carnet de estudiante. Quien no es estudiante pero tiene perfil de personal (`StaffProfile`) recibe el carnet de personal. Si alguien es encargado y también personal, recibe el de personal. Encargados y personas sin perfil escolar no tienen carnet: la API responde 404.
- **Tabla propia `people.StaffCards`:** misma forma que `StudentCards`, con token aleatorio de 12 caracteres, un carnet activo por persona (índice único filtrado), reemplazo con motivo que revoca el anterior bajo un bloqueo propio y auditoría `StaffCardReissued`/`StaffCardsIssued` en la misma transacción. No se generalizó `StudentCards`, porque `GateEvents.CardId` y la portería dependen de ella.
- **Portería:** el carnet de personal identifica a la persona, pero no registra asistencia. Al escanearlo, el resultado es `Unknown` con el mensaje «Carnet de personal…». No se agregó un resultado nuevo, para no romper lectores ya instalados. La asistencia del personal no forma parte de este incremento.
- **Ciclo explícito:** para imprimir por persona (`GET /api/v1/people/{id}/card.pdf?yearId=…`) y para el personal completo (`GET /api/v1/staff/cards.pdf?yearId=…`) hay que indicar el ciclo. Solo se aceptan ciclos con inscripción abierta o en curso. El de estudiante exige una ubicación en ese ciclo (409 si no la tiene). `GET /api/v1/people/{id}/card` (`people.read`) devuelve el tipo, el carnet vigente y los ciclos disponibles. `GET /api/v1/cards/years` alimenta la impresión masiva. Las rutas anteriores `/students/{id}/card*` y `/years/{id}/cards.pdf` se conservan.
- **Personas archivadas:** no reciben carnet de personal.
- **Diseño CR80:**
  - Frente: cabecera grafito con escudo, nombre institucional (palabras previas al `ShortName` como antetítulo, `ShortName` grande y `CampusLabel` debajo) y «CARNÉ año».
  - Cuerpo claro con aros tenues; foto con marcas de esquina de acento; tipo de identificación; nombres y apellidos en dos líneas escaladas para que quepan; grado/sección o puesto; chip grafito con el código (estudiante) o el ID agrupado (personal); QR en recuadro blanco.
  - Pie: el del estudiante es grafito con el lema; el del personal es de color de acento con «PERSONAL DEL COLEGIO», para distinguirlos a simple vista.
  - Reverso: cabecera con nombre y «IDENTIFICACIÓN año», etiqueta ESTUDIANTE/PERSONAL, condiciones de uso numeradas (`CardRules`), aviso propio de cada tipo y banda grafito con el contacto de emergencia y el Code 128 en un recuadro blanco.
- **Textos institucionales:** solo en `config/institution.json`. `Documents.StaffCardNotice` es nuevo, opcional y vacío en esta instalación hasta que el colegio lo redacte; el aviso de estudiantes (`CardNotice`) nunca se usa en el carnet de personal.

## Consecuencias

- Nueva migración aditiva `StaffCards`. Si otra rama agrega migraciones al mismo tiempo, hay que regenerar el snapshot al integrar.
- Impresión física pendiente: probar CR80/carta al 100 % y lectura del QR y del Code 128 con los lectores reales del colegio.
