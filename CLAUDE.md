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

Hecho: estructura base, `/health`, formato de errores (`Errors/`), hub de SignalR con identidad por `X-User-Id`, pruebas de integración (`WebApplicationFactory`, sin Mongo ni Service Bus), borrador del contrato en `docs/contratos/` (versión 0.1, validado).

Contrato: falta que el equipo lo revise (preguntas abiertas al final de `docs/contratos/README.md`) y copiar la carpeta a `cvexpert-docs/contratos` por PR.

Código pendiente, en este orden: consumidor de Service Bus → persistencia en Mongo → envío por SignalR → endpoints REST → FCM → correo. Al agregar el consumidor de Service Bus, las pruebas deben poder quitarlo o reemplazarlo en el factory para no conectarse a Azure.

## Contratos

La fuente de verdad es [`docs/contratos/`](docs/contratos/): `evento-notificacion.schema.json` (mensaje de la cola `notificaciones-eventos`), `openapi.yaml` (endpoints REST) y `README.md` (cómo publicar eventos, plantillas, SignalR y preguntas abiertas). Si el código y el contrato no coinciden, manda el contrato; cualquier cambio se acuerda con el equipo y se actualiza ahí primero.

## Notas técnicas

- Service Bus: usar una política SAS de la cola con solo **Listen** (nunca `RootManageSharedAccessKey`); en Azure, preferir Managed Identity.
- Cosmos DB con API de MongoDB: la cadena de conexión debe llevar `retryWrites=false`.
- En Windows PowerShell 5.1 usar `curl.exe` (`curl` es alias de `Invoke-WebRequest`).

## Comandos

```bash
dotnet build
dotnet test
dotnet run --project src/Notificaciones.Api   # Swagger en /swagger (Development)

# Validar contratos (requiere Node)
npx @redocly/cli lint docs/contratos/openapi.yaml
npx ajv-cli validate --spec=draft2020 -c ajv-formats -s docs/contratos/evento-notificacion.schema.json -d <ejemplo>.json
```

## Forma de trabajo

`main` protegida; todo entra por PR revisado por otro integrante. No hacer commit ni push sin que el usuario lo pida.
