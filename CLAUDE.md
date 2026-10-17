# CLAUDE.md

## Proyecto

CVExpert, Fase II del curso Diseño de Software (TEC). Cinco microservicios, cada uno en su repo, detrás de **Azure API Management (APIM)**. Clientes: web (React) y Android.

Este repo es el microservicio de **Notificaciones**:
- Recibe eventos de otros servicios por una cola de **Azure Service Bus**.
- Los guarda en **MongoDB** (Cosmos DB con API de MongoDB).
- Los entrega en tiempo real a la web con **SignalR** (`/hubs/notifications`), como push al móvil con **Firebase Cloud Messaging (FCM)** y por **correo** con plantillas editables en español e inglés.
- Eventos: cambio de estado de una postulación, CV generado, nueva oferta, curso recomendable.

## Stack

.NET 8 (`net8.0`), ASP.NET Core Web API con controllers, `MongoDB.Driver`, `Azure.Messaging.ServiceBus`, Swashbuckle (Swagger), SignalR, pruebas con xUnit + `Microsoft.AspNetCore.Mvc.Testing`.

## Reglas de diseño (obligatorias)

- Ningún servicio llama a otro. Solo APIM llama a este backend.
- La identidad llega en los encabezados `X-User-Id` y `X-User-Email`; en rutas de administración también `X-User-Role`. Este servicio **no** valida tokens de Google (lo hace APIM).
- Fechas siempre como **epoch en milisegundos UTC** (`long`), nunca como texto.
- Ninguna URL, cadena de conexión ni credencial en el código ni en `appsettings.json`. Local: `dotnet user-secrets`. Azure: Key Vault.
- Listas paginadas: `{ totalRecords, page, pageSize, items }`.
- Errores: `{ status, message, errors?: { campo: mensaje } }` (`errors` solo en 400).

## Canales de entrega

Todos los canales son responsabilidad de este servicio (ningún otro servicio puede enviarlos):

- **Web — SignalR** (`/hubs/notifications`): solo mientras la pestaña está abierta; aviso en vivo y contador. El usuario se identifica por `X-User-Id` (`HeaderUserIdProvider`); sin ese encabezado la conexión se cierra. Enviar con `IHubContext<NotificationsHub>.Clients.User(userId)`.
- **Móvil Android — push con FCM**: llega aunque la app esté cerrada. La app registra su token de FCM en este servicio (vía APIM).
- **Correo**: plantillas editables en español e inglés.
- **Historial — REST**: web y móvil consultan las mismas notificaciones guardadas en Mongo.

## Estado actual

Hecho: estructura base, `/health`, formato de errores (`Errors/`), hub de SignalR con identidad por `X-User-Id`, pruebas de integración (`WebApplicationFactory`, sin Mongo ni Service Bus).

Pendiente, en este orden: consumidor de Service Bus → persistencia en Mongo → envío por SignalR → endpoints REST → FCM → correo. Al agregar el consumidor de Service Bus, las pruebas deben poder quitarlo o reemplazarlo en el factory para no conectarse a Azure.

## Contratos (propuesta, pendiente de acordar con el equipo)

**Evento de la cola.** Como este servicio no puede consultar a otros, el evento debe traer todo lo necesario (incluido correo e idioma):

```json
{
  "eventId": "uuid",                      // idempotencia: Service Bus entrega al menos una vez
  "type": "postulacion.estadoCambiado",   // | cv.generado | oferta.nueva | curso.recomendado
  "occurredAt": 1760716800000,            // epoch ms UTC
  "userId": "usuario-1",
  "userEmail": "ana@ejemplo.com",
  "language": "es",                       // es | en
  "data": { }                             // campos propios de cada tipo
}
```

**Endpoints para clientes (vía APIM):**

- `GET /notifications?page&pageSize&unreadOnly` → `{ totalRecords, page, pageSize, items }`
- `GET /notifications/unread-count`
- `PATCH /notifications/{id}/read`, `PATCH /notifications/read-all`
- `POST /devices` `{ token, platform: "android" }`, `DELETE /devices/{token}` (token de FCM)
- `GET`/`PUT /admin/templates/{type}/{language}` (requiere `X-User-Role`)
- SignalR: evento `notificacionRecibida` con la misma forma que cada elemento de `items`.

**Decisiones abiertas:**

1. `oferta.nueva`: ¿a quién se notifica? Este servicio no sabe qué usuarios están interesados; lo más simple es que el servicio de ofertas publique un evento por destinatario.
2. Confirmar que `userEmail` y `language` viajan en cada evento.
3. ¿Preferencias por canal (p. ej. apagar correos)? Si sí, `GET`/`PUT /preferences`.
4. ¿Se guarda el texto ya armado (título y cuerpo en su idioma) o solo `type` + `data` y cada cliente lo arma?
5. Confirmar con APIM que `X-User-Id` llega en el `negotiate` y en el handshake del WebSocket de SignalR.

## Notas técnicas

- Service Bus: usar una política SAS de la cola con solo **Listen** (nunca `RootManageSharedAccessKey`); en Azure, preferir Managed Identity.
- Cosmos DB con API de MongoDB: la cadena de conexión debe llevar `retryWrites=false`.
- En Windows PowerShell 5.1 usar `curl.exe` (`curl` es alias de `Invoke-WebRequest`).

## Comandos

```bash
dotnet build
dotnet test
dotnet run --project src/Notificaciones.Api   # Swagger en /swagger (Development)
```

## Forma de trabajo

`main` protegida; todo entra por PR revisado por otro integrante. No hacer commit ni push sin que el usuario lo pida.
