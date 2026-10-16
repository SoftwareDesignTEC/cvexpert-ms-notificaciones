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

## Comandos

```bash
dotnet build
dotnet test
dotnet run --project src/Notificaciones.Api   # Swagger en /swagger (Development)
```

## Forma de trabajo

`main` protegida; todo entra por PR revisado por otro integrante. No hacer commit ni push sin que el usuario lo pida.
