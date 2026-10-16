# cvexpert-ms-notificaciones

Microservicio de **Notificaciones** de CVExpert. Consume eventos de otros servicios desde una cola de Azure Service Bus, los guarda en MongoDB y los entrega al usuario en tiempo real a la web (SignalR), como push al móvil (Firebase Cloud Messaging) y por correo con plantillas en español e inglés.

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Configuración local (secretos)

Ninguna cadena de conexión ni credencial va en el código ni en `appsettings.json`. En local se usan `dotnet user-secrets`; en Azure, Key Vault.

```bash
dotnet user-secrets --project src/Notificaciones.Api set "Mongo:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets --project src/Notificaciones.Api set "ServiceBus:ConnectionString" "Endpoint=sb://ejemplo.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<clave>"
dotnet user-secrets --project src/Notificaciones.Api set "ServiceBus:QueueName" "notificaciones"

# Ver los valores configurados
dotnet user-secrets --project src/Notificaciones.Api list
```

Los valores anteriores son de ejemplo; pide los reales al equipo.

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

## Estructura

```
cvexpert-ms-notificaciones/
├── Notificaciones.sln
├── src/Notificaciones.Api/
│   ├── Controllers/       # Endpoints HTTP (HealthController)
│   ├── Hubs/              # Hubs de SignalR (NotificationsHub)
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
