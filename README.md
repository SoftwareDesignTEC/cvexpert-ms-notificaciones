# cvexpert-ms-notificaciones

Microservicio de **Notificaciones** de CVExpert. Consume eventos de otros servicios desde una cola de Azure Service Bus, los guarda en MongoDB y los entrega al usuario en tiempo real a la web (SignalR), como push al móvil (Firebase Cloud Messaging) y por correo con plantillas en español e inglés.

## Requisitos

- [SDK de .NET](https://dotnet.microsoft.com/download) 8 o superior, con el runtime de .NET 8 instalado (el proyecto apunta a `net8.0`).

## Configuración local (secretos)

Ninguna cadena de conexión ni credencial va en el código ni en `appsettings.json`. En local se usan `dotnet user-secrets`; en Azure, Key Vault.

| Clave | Para qué | Si falta |
|---|---|---|
| `Mongo:ConnectionString` | Cadena de conexión de MongoDB / Cosmos DB | El consumidor de la cola no arranca |
| `Mongo:Database` | Base de datos (por defecto `notificaciones`, en `appsettings.json`) | El consumidor de la cola no arranca |
| `ServiceBus:ConnectionString` | Cadena SAS de la cola, solo **Listen** | El consumidor de la cola no arranca |
| `ServiceBus:QueueName` | Nombre de la cola: `notificaciones-eventos` | El consumidor de la cola no arranca |
| `Azure:SignalR:ConnectionString` | Cadena de Azure SignalR Service (tiempo real) | Se usa SignalR local (solo sirve para desarrollo y pruebas) |

Sin alguna de estas claves, la API sigue funcionando; solo se registra una advertencia.

```bash
dotnet user-secrets --project src/Notificaciones.Api set "Mongo:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets --project src/Notificaciones.Api set "ServiceBus:ConnectionString" "Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<politica-listen>;SharedAccessKey=<clave>;EntityPath=notificaciones-eventos"
dotnet user-secrets --project src/Notificaciones.Api set "ServiceBus:QueueName" "notificaciones-eventos"
dotnet user-secrets --project src/Notificaciones.Api set "Azure:SignalR:ConnectionString" "Endpoint=https://<recurso>.service.signalr.net;AccessKey=<clave>;Version=1.0;"

# Ver los valores configurados
dotnet user-secrets --project src/Notificaciones.Api list
```

Los valores anteriores son de ejemplo; pide los reales al equipo.

- Para Service Bus usa una política de acceso compartido **de la cola**, solo con permiso **Listen**. No uses `RootManageSharedAccessKey`: da control total sobre todo el namespace. Si la cadena incluye `EntityPath`, debe coincidir con `ServiceBus:QueueName`.
- En Cosmos DB con API de MongoDB la cadena de conexión debe llevar `retryWrites=false`.
- En Azure las mismas claves van en Key Vault con `--` en lugar de `:` (por ejemplo `ServiceBus--ConnectionString`).

## Consumidor de la cola

`NotificationEventsConsumer` lee la cola `notificaciones-eventos` (PeekLock, hasta 4 mensajes a la vez) y por cada mensaje:

1. Lo valida contra [`docs/contratos/evento-notificacion.schema.json`](docs/contratos/evento-notificacion.schema.json). Si no es JSON válido o no cumple el esquema, lo manda a la dead-letter sin reintentar (`InvalidJson` o `SchemaValidationFailed`, con el detalle en la descripción).
2. Lo guarda en la colección `notifications`. Si el `eventId` ya existe es un duplicado: se completa sin hacer nada más.
3. Lo entrega por los canales (`INotificationDispatcher`; por ahora solo tiempo real con SignalR). Si la entrega falla, el mensaje se completa igual porque ya está guardado.
4. Si Mongo falla, espera 5 s y abandona el mensaje para que Service Bus lo reintente; al agotar `MaxDeliveryCount`, Service Bus lo pasa a la dead-letter.

Al arrancar crea los índices de `notifications` (`eventId` único y `userId` + `createdAt` descendente) y no consume hasta tenerlos. En Cosmos DB el índice único solo se puede crear con la colección vacía.

## Tiempo real (SignalR)

El APIM es Consumption y no soporta WebSockets, así que se usa **Azure SignalR Service**:

1. **Negotiate (vía APIM).** La librería cliente hace `POST /notifications/hub/negotiate`. APIM agrega `X-User-Id` y el backend responde con la `url` del SignalR Service y un `accessToken` emitido para ese usuario. Sin `X-User-Id` responde 401.
2. **Conexión (directa).** El cliente se conecta al SignalR Service con esa `url` y ese token, sin pasar por APIM ni por este backend. La identidad viaja dentro del token.
3. **Envío.** Cuando llega una notificación, `SignalRNotificationDispatcher` invoca `notification` en las conexiones del usuario (`IHubContext<NotificationsHub>.Clients.User(userId)`). El argumento tiene la misma forma que un item de `GET /notifications` (`id`, `type`, `title`, `body`, `data`, `read`, `createdAt`).

`title` y `body` se arman al enviar, con textos por defecto por tipo e idioma (`DefaultNotificationRenderer`); por ahora siempre en español, hasta que exista el directorio de usuarios. Se reemplazarán por las plantillas editables.

Sin `Azure:SignalR:ConnectionString` el hub funciona en modo local (el negotiate devuelve un `connectionToken` en vez de `url` y `accessToken`): sirve para desarrollar y probar, pero no detrás de APIM.

La identidad de todas las rutas sale de los encabezados de APIM con el esquema de autenticación `ApimHeaders` (`X-User-Id` → NameIdentifier, `X-User-Email` → Email, `X-User-Role` → Role). Una ruta con `[Authorize]` responde 401 sin `X-User-Id`.

## Correr el servicio

```bash
dotnet run --project src/Notificaciones.Api
```

- Health check: <http://localhost:5029/health>
- Swagger (solo en Development): <http://localhost:5029/swagger>
- Hub de SignalR: `/notifications/hub`

## Pruebas

```bash
dotnet test
```

Las pruebas de integración levantan la API en memoria (`NotificacionesApiFactory`, que quita el consumidor de la cola y fuerza SignalR local), sin Mongo, Service Bus ni Azure SignalR, aunque haya user-secrets configurados. El validador, el handler del consumidor y el dispatcher de SignalR se prueban con dobles.

### Prueba manual

Con el servicio corriendo (`dotnet run --project src/Notificaciones.Api`):

```bash
# 200 { "status": "ok" }
curl -i http://localhost:5029/health

# 404 { "status": 404, "message": "Not Found" }
curl -i http://localhost:5029/no-existe

# SignalR sin X-User-Id: 401 { "status": 401, "message": "Unauthorized" }
curl -i -X POST "http://localhost:5029/notifications/hub/negotiate?negotiateVersion=1"

# SignalR con X-User-Id (en producción lo agrega APIM): 200; en local trae connectionToken, con Azure trae url y accessToken
curl -i -X POST "http://localhost:5029/notifications/hub/negotiate?negotiateVersion=1" -H "X-User-Id: usuario-1"
```

## Convenciones de la API

- **Errores**: siempre `{ status, message, errors? }`; `errors` (`{ campo: mensaje }`) solo aparece en 400.
- **Identidad**: esquema `ApimHeaders` a partir de `X-User-Id`, `X-User-Email` y `X-User-Role`. Las rutas de usuario llevan `[Authorize]` y responden 401 sin `X-User-Id`.
- **SignalR**: hub `/notifications/hub` con `[Authorize]`; mensaje `notification` con la forma de un item de `GET /notifications`. Para enviarle algo a un usuario: `IHubContext<NotificationsHub>.Clients.User(userId)`.

## Estructura

```
cvexpert-ms-notificaciones/
├── Notificaciones.sln
├── src/Notificaciones.Api/
│   ├── Auth/              # Autenticación por los encabezados de APIM (esquema ApimHeaders)
│   ├── Contracts/         # Formas de respuesta del contrato (NotificationResponse)
│   ├── Controllers/       # Endpoints HTTP (HealthController)
│   ├── Errors/            # Formato único de errores { status, message, errors? }
│   ├── Hubs/              # Hub de SignalR y entrega en tiempo real (SignalRNotificationDispatcher)
│   ├── Domain/            # Modelos de dominio (Notification)
│   ├── Infrastructure/    # MongoDB (repositorio) y Service Bus (consumidor de la cola)
│   ├── Options/           # Clases de configuración (patrón Options)
│   ├── Services/          # Lógica de negocio: eventos de la cola, textos de las notificaciones, interfaces de persistencia y entrega
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
└── tests/Notificaciones.Tests/   # Pruebas xUnit
```

## Forma de trabajo

- `main` está protegida: no se hace push directo.
- Todo cambio entra por Pull Request y debe ser revisado y aprobado por otro integrante del equipo.
