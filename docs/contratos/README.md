# Contrato del servicio de Notificaciones

Fuente de verdad del contrato entre Notificaciones y el resto de CVExpert:

| Archivo | Qué define | Para quién |
|---|---|---|
| [`evento-notificacion.schema.json`](evento-notificacion.schema.json) | Mensaje de la cola de Service Bus (JSON Schema 2020-12) | Quien publica eventos: Ofertas y Postulaciones, CVs, Brecha y Cursos |
| [`openapi.yaml`](openapi.yaml) | Endpoints REST (OpenAPI 3.0) | APIM y clientes web y Android |
| Este README | Cómo publicar un evento, ejemplos, plantillas y tiempo real | Todos |

## 1. Cómo publicar un evento

**Destino:** cola `notificaciones-eventos` del namespace `cvexpert-bus` (Azure Service Bus, tier Basic). Pide al equipo de Notificaciones una política SAS de la cola **solo con permiso Send**.

**Un mensaje por destinatario.** Si un evento aplica a varias personas, envía un mensaje por cada una, cada uno con su propio `eventId`.

**Propiedades del mensaje de Service Bus** (obligatorias):

| Propiedad | Valor |
|---|---|
| `messageId` | El mismo `eventId` del cuerpo |
| `subject` | El mismo `type` del cuerpo |
| `contentType` | `application/json` |

**Cuerpo** (JSON en UTF-8):

| Campo | Tipo | Descripción |
|---|---|---|
| `schemaVersion` | entero | Siempre `1` |
| `eventId` | texto | UUID v4, único por mensaje |
| `type` | texto | `application.status_changed`, `cv.generated`, `job.new` o `course.recommended` |
| `userId` | texto | Id del destinatario: el mismo valor que APIM envía en `X-User-Id` |
| `occurredAt` | entero | Cuándo ocurrió el evento, en **epoch milisegundos UTC**. Nunca texto ISO |
| `source` | texto | Servicio que publica. Cada `type` solo acepta su publicador (tabla siguiente) |
| `data` | objeto | Campos propios del tipo |

**Reglas:**

- El sobre es estricto: no agregues campos fuera de `data`. Si hace falta uno nuevo, se sube `schemaVersion`.
- Dentro de `data` se aceptan campos adicionales: el consumidor los ignora y el mensaje no se rechaza. Los campos obligatorios sí se validan.
- Los ids (`applicationId`, `jobOfferId`, `cvId`) van **siempre como texto**, aunque en tu base sean números: `"1532"`, no `1532`.
- Un campo opcional se puede omitir o enviar como `null`.
- No envíes correo ni idioma: Notificaciones los toma de su propio directorio (`PUT /notifications/directory/me`, que llama el cliente).

### `data` por tipo

| `type` | `source` | Obligatorios | Opcionales |
|---|---|---|---|
| `application.status_changed` | `ofertas-postulaciones` | `applicationId`, `jobOfferId`, `jobTitle`, `company`, `newStatus` | `previousStatus` |
| `cv.generated` | `cvs` | `cvId`, `title` | `templateId` |
| `job.new` | `ofertas-postulaciones` | `jobOfferId`, `title`, `company`, `location`, `modality`, `matchScore` | — |
| `course.recommended` | `brecha-cursos` | `title`, `platform`, `url`, `isPaid` | `duration`, `skill` |

- `previousStatus` y `newStatus`: `applied`, `in-progress`, `interview` o `rejected` (valores de la Fase I).
- `modality`: `remote`, `on-site` o `hybrid`.
- `matchScore`: número de 0 a 100 y admite decimales. Para `job.new`, Ofertas ya filtró a quienes superan su mínimo de compatibilidad; Notificaciones no calcula compatibilidad.
- `url`: enlace absoluto `http://` o `https://`. `isPaid`: booleano. `duration`: texto para mostrar, por ejemplo `"12 horas"`.

## 2. Ejemplos completos

### `application.status_changed`

```json
{
  "schemaVersion": 1,
  "eventId": "3f6c2a1e-8b4d-4c7a-9e21-5d0b7f3a9c10",
  "type": "application.status_changed",
  "userId": "108234567890123456789",
  "occurredAt": 1760745600000,
  "source": "ofertas-postulaciones",
  "data": {
    "applicationId": "1532",
    "jobOfferId": "88",
    "jobTitle": "Desarrollador Backend",
    "company": "Acme",
    "previousStatus": "in-progress",
    "newStatus": "interview"
  }
}
```

### `cv.generated`

```json
{
  "schemaVersion": 1,
  "eventId": "a9e4d2b7-1c3f-4e58-8a6d-0f2b9c7e4d31",
  "type": "cv.generated",
  "userId": "108234567890123456789",
  "occurredAt": 1760745660000,
  "source": "cvs",
  "data": {
    "cvId": "507",
    "title": "CV Desarrollador Backend 2026",
    "templateId": "moderno"
  }
}
```

### `job.new`

```json
{
  "schemaVersion": 1,
  "eventId": "c51b8e30-7d2a-4f96-b4e1-9a3c6d0f8e72",
  "type": "job.new",
  "userId": "108234567890123456789",
  "occurredAt": 1760745720000,
  "source": "ofertas-postulaciones",
  "data": {
    "jobOfferId": "91",
    "title": "Ingeniero de Datos",
    "company": "Globex",
    "location": "San José, Costa Rica",
    "modality": "hybrid",
    "matchScore": 87.5
  }
}
```

### `course.recommended`

```json
{
  "schemaVersion": 1,
  "eventId": "e27f9c14-5a8b-4d03-a6c2-1b7e3f9d5a48",
  "type": "course.recommended",
  "userId": "108234567890123456789",
  "occurredAt": 1760745780000,
  "source": "brecha-cursos",
  "data": {
    "title": "Docker y Kubernetes desde cero",
    "platform": "Udemy",
    "url": "https://www.udemy.com/course/docker-kubernetes/",
    "isPaid": true,
    "duration": "12 horas",
    "skill": "Kubernetes"
  }
}
```

## 3. Entregas duplicadas y mensajes rechazados

**Idempotencia.** El tier Basic de Service Bus **no** tiene detección de duplicados y entrega cada mensaje *al menos una vez*. Notificaciones descarta cualquier mensaje cuyo `eventId` ya procesó, así que reenviar un mensaje con el mismo `eventId` es seguro. Por la misma razón, nunca reutilices un `eventId` para un evento distinto: se descartaría.

**Cola de errores (dead-letter).** Notificaciones mueve el mensaje a `notificaciones-eventos/$deadletterqueue` sin reintentar cuando:

| `DeadLetterReason` | Causa |
|---|---|
| `InvalidJson` | El cuerpo no es JSON válido |
| `SchemaValidationFailed` | El cuerpo no cumple `evento-notificacion.schema.json`: falta un campo, el tipo es incorrecto, el `type` es desconocido, el `source` no corresponde al `type`, etc. |

`DeadLetterErrorDescription` trae el detalle, por ejemplo `data.newStatus: "hired" no es un valor permitido`. Los errores transitorios de Notificaciones (por ejemplo, la base caída) se reintentan. Si se agotan los intentos de la cola, Service Bus mueve el mensaje a la misma cola de errores con `MaxDeliveryCountExceeded`.

## 4. Plantillas

Notificaciones arma `title` y `body` con una plantilla por tipo e idioma (8 en total), editables por un administrador (`/notifications/templates`). Las plantillas usan marcadores `{{campo}}`, que se rellenan con el `data` del evento. Solo se aceptan estos marcadores:

| `type` | Marcadores permitidos |
|---|---|
| `application.status_changed` | `{{applicationId}}`, `{{jobOfferId}}`, `{{jobTitle}}`, `{{company}}`, `{{previousStatus}}`, `{{newStatus}}` |
| `cv.generated` | `{{cvId}}`, `{{title}}`, `{{templateId}}` |
| `job.new` | `{{jobOfferId}}`, `{{title}}`, `{{company}}`, `{{location}}`, `{{modality}}`, `{{matchScore}}` |
| `course.recommended` | `{{title}}`, `{{platform}}`, `{{url}}`, `{{isPaid}}`, `{{duration}}`, `{{skill}}` |

Un marcador opcional sin valor se reemplaza por texto vacío. Guardar una plantilla con un marcador que no está en la lista responde 400.

## 5. Tiempo real (SignalR)

- **Hub:** `/hubs/notifications` (a través de APIM).
- **Identidad:** el servidor identifica al usuario por `X-User-Id`, que agrega APIM. Sin ese encabezado la conexión se cierra. Falta confirmar que llega también en la conexión WebSocket (pregunta abierta 8).
- **Mensaje:** cuando llega una notificación, el servidor invoca `notification` en las conexiones de ese usuario. El argumento es el objeto `Notification` de [`openapi.yaml`](openapi.yaml), el mismo que devuelve `GET /notifications`:

```json
{
  "id": "6711a2f0c3e4b5a6d7e8f901",
  "type": "application.status_changed",
  "title": "Tu postulación avanzó",
  "body": "Tu postulación a Desarrollador Backend en Acme cambió a \"Entrevista\".",
  "data": {
    "applicationId": "1532",
    "jobOfferId": "88",
    "jobTitle": "Desarrollador Backend",
    "company": "Acme",
    "previousStatus": "in-progress",
    "newStatus": "interview"
  },
  "read": false,
  "createdAt": 1760745601234
}
```

```js
connection.on("notification", (n) => { /* agregar a la lista y subir el contador */ });
```

## 6. Preguntas abiertas

1. **WebSockets en APIM.** ¿El tier de APIM soporta WebSockets para SignalR? Si no, el cliente cae a otros transportes (Server-Sent Events o long polling). Hay que decidirlo con Cabrera.
2. **Texto de las plantillas por defecto.** El texto exacto en español e inglés se escribirá al implementar los correos.
3. **Paginación por cursor.** Queda por ver si más adelante hace falta paginar por cursor en vez de por `page`.
4. **Mismo `X-User-Id` en todos los servicios.** Se asume que es el `sub` del token de Google. Falta confirmarlo con Cabrera (APIM).
5. **Campos de `data`.** Andrés, Gabo y Justin deben confirmar que los campos de `data` de sus eventos son los que ellos pueden enviar.
6. **Valores de lista en las plantillas.** Marcadores como `{{newStatus}}`, `{{previousStatus}}`, `{{modality}}` e `{{isPaid}}` mostrarían el valor crudo (`in-progress`, `hybrid`, `true`). **Propuesta por defecto:** al renderizar, esos valores se traducen al idioma del usuario con un diccionario pequeño (por ejemplo `in-progress` → "En proceso" / "In progress", `hybrid` → "Híbrida" / "Hybrid"), en vez de mostrar el valor crudo.
7. **Preferencias por canal.** ¿El usuario puede apagar canales (correo, push, tiempo real) o tipos de evento? Por ahora no está en el contrato ni en el plan. Si se decide, sería un campo `preferences` en `PUT /notifications/directory/me` y el consumidor lo respetaría al enviar. Se decide con el grupo, porque toca la pantalla de configuración de notificaciones.
8. **`X-User-Id` en SignalR.** Falta confirmar con Cabrera (APIM) que `X-User-Id` llega tanto en la petición `negotiate` como en la conexión WebSocket. Los navegadores no dejan poner encabezados propios en un WebSocket, así que si APIM no los agrega ahí, hay que identificar al usuario de otra forma (por ejemplo, con un token de acceso que entrega `negotiate`). Es una verificación pendiente, no un hecho.
