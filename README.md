# cvexpert-ms-notificaciones

Microservicio de **Notificaciones** de CVExpert. Consume eventos de otros servicios desde una cola de Azure Service Bus, los guarda en MongoDB y los entrega al usuario en tiempo real a la web (SignalR), como push al móvil (Firebase Cloud Messaging) y por correo con plantillas en español e inglés.

## Requisitos

- [SDK de .NET](https://dotnet.microsoft.com/download) 8 o superior, con el runtime de .NET 8 instalado (el proyecto apunta a `net8.0`).

## Configuración local (secretos)

Ninguna cadena de conexión ni credencial va en el código ni en `appsettings.json`. En local se usan `dotnet user-secrets`; en Azure, Key Vault.

```bash
dotnet user-secrets --project src/Notificaciones.Api set "Mongo:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets --project src/Notificaciones.Api set "ServiceBus:ConnectionString" "Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<politica-listen>;SharedAccessKey=<clave>;EntityPath=notificaciones"
dotnet user-secrets --project src/Notificaciones.Api set "ServiceBus:QueueName" "notificaciones"

# Ver los valores configurados
dotnet user-secrets --project src/Notificaciones.Api list
```

Los valores anteriores son de ejemplo; pide los reales al equipo.

- Para Service Bus usa una política de acceso compartido **de la cola**, solo con permiso **Listen**. No uses `RootManageSharedAccessKey`: da control total sobre todo el namespace. Si la cadena incluye `EntityPath`, debe coincidir con `ServiceBus:QueueName`.
- En Cosmos DB con API de MongoDB la cadena de conexión debe llevar `retryWrites=false`.

## Correr el servicio

```bash
dotnet run --project src/Notificaciones.Api
```

- Health check: <http://localhost:5029/health>
- Swagger (solo en Development): <http://localhost:5029/swagger>
- Hub de SignalR: `/hubs/notifications`

## Pruebas

```bash
dotnet test
```

Las pruebas de integración levantan la API en memoria (`WebApplicationFactory`), sin Mongo ni Service Bus.

### Prueba manual

Con el servicio corriendo (`dotnet run --project src/Notificaciones.Api`):

```bash
# 200 { "status": "ok" }
curl -i http://localhost:5029/health

# 404 { "status": 404, "message": "Not Found" }
curl -i http://localhost:5029/no-existe

# SignalR: negotiate devuelve un connectionToken (en producción APIM agrega X-User-Id)
curl -i -X POST "http://localhost:5029/hubs/notifications/negotiate?negotiateVersion=1" -H "X-User-Id: usuario-1"
```

## Convenciones de la API

- **Errores**: siempre `{ status, message, errors? }`; `errors` (`{ campo: mensaje }`) solo aparece en 400.
- **SignalR**: el hub identifica al usuario por el encabezado `X-User-Id` y cierra las conexiones que no lo traen. Para enviarle algo a un usuario: `IHubContext<NotificationsHub>.Clients.User(userId)`.

## Estructura

```
cvexpert-ms-notificaciones/
├── Notificaciones.sln
├── src/Notificaciones.Api/
│   ├── Controllers/       # Endpoints HTTP (HealthController)
│   ├── Errors/            # Formato único de errores { status, message, errors? }
│   ├── Hubs/              # Hub de SignalR e identificación del usuario por X-User-Id
│   ├── Domain/            # Modelos de dominio
│   ├── Infrastructure/    # Acceso a MongoDB y Service Bus
│   ├── Options/           # Clases de configuración (patrón Options)
│   ├── Services/          # Lógica de negocio
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
└── tests/Notificaciones.Tests/   # Pruebas xUnit
```

## Forma de trabajo

- `main` está protegida: no se hace push directo.
- Todo cambio entra por Pull Request y debe ser revisado y aprobado por otro integrante del equipo.
