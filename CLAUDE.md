# CLAUDE.md

## Proyecto

CVExpert, Fase II del curso Diseño de Software (TEC). Cinco microservicios, cada uno en su repo, detrás de **Azure API Management (APIM)**. Clientes: web (React) y Android.

Este repo es el microservicio de **Notificaciones**:
- Recibe eventos de otros servicios por una cola de **Azure Service Bus**.
- Los guarda en **MongoDB** (Cosmos DB con API de MongoDB).
- Los entrega en tiempo real a la web con **SignalR** sobre **Azure SignalR Service** (hub `/notifications/hub`), como push al móvil con **Firebase Cloud Messaging (FCM)** y por **correo** con plantillas editables en español e inglés.
- Eventos: cambio de estado de una postulación, CV generado, nueva oferta, curso recomendable.

## Stack

.NET 8 (`net8.0`), ASP.NET Core Web API con controllers, `MongoDB.Driver`, `Azure.Messaging.ServiceBus`, Swashbuckle (Swagger), SignalR con Azure SignalR Service (`Microsoft.Azure.SignalR`, tier Free), pruebas con xUnit + `Microsoft.AspNetCore.Mvc.Testing`.

## Reglas de diseño (obligatorias)

- Ningún servicio llama a otro. Solo APIM llama a este backend.
- La identidad llega en los encabezados `X-User-Id` (el `sub` del token de Google, el mismo en todos los servicios; APIM lo sobrescribe siempre) y `X-User-Email`; en rutas de administración también `X-User-Role`. Este servicio **no** valida tokens de Google (lo hace APIM). Los encabezados solo llegan cuando la operación está conectada en APIM.
- Fechas siempre como **epoch en milisegundos UTC** (`long`), nunca como texto.
- Ninguna URL, cadena de conexión ni credencial en el código ni en `appsettings.json`. Local: `dotnet user-secrets`. Azure: Key Vault.
- Listas paginadas: `{ totalRecords, page, pageSize, items }`.
- Errores: `{ status, message, errors?: { campo: mensaje } }` (`errors` solo en 400).

## Canales de entrega

Todos los canales son responsabilidad de este servicio (ningún otro servicio puede enviarlos):

- **Web — SignalR** (hub `/notifications/hub`): solo mientras la pestaña está abierta; aviso en vivo y contador. El APIM es Consumption y no soporta WebSockets, así que se usa **Azure SignalR Service** (Free): el cliente hace `POST /notifications/hub/negotiate` por APIM (ahí llega `X-User-Id`), el backend responde con la URL del SignalR Service y un token de acceso para ese usuario, y el cliente se conecta directo al SignalR Service. La conexión **no** trae `X-User-Id`: el usuario se toma en el negotiate y viaja en el token. Enviar con `IHubContext<NotificationsHub>.Clients.User(userId)`.
- **Móvil Android — push con FCM**: llega aunque la app esté cerrada. La app registra su token de FCM en este servicio (vía APIM).
- **Correo**: plantillas editables en español e inglés.
- **Historial — REST**: web y móvil consultan las mismas notificaciones guardadas en Mongo.

## Estado actual

Hecho: estructura base, `/health`, formato de errores (`Errors/`), pruebas de integración (`NotificacionesApiFactory`, sin Mongo, Service Bus ni Azure SignalR), borrador del contrato en `docs/contratos/` (versión 0.1, validado), consumidor de la cola con persistencia en Mongo (PR #3), tiempo real con Azure SignalR Service.

Consumidor (`Infrastructure/ServiceBus/NotificationEventsConsumer.cs`): valida con `NotificationEventValidator` (escrito a mano: si cambia el esquema, cambiarlo también ahí y en sus pruebas), guarda en `notifications` (índice único `eventId` para deduplicar; `userId` + `createdAt` desc) y llama a `INotificationDispatcher`. Inválido → dead-letter; duplicado → completa; Mongo caído → abandona tras 5 s; fallo del dispatcher → completa igual. Sin `ServiceBus:*` o `Mongo:*` no arranca y la API sigue funcionando. `NotificacionesApiFactory` lo quita en las pruebas de integración. Falta probarlo contra Cosmos y Service Bus reales (en especial que un `eventId` repetido llegue como `DuplicateKey`).

Identidad: esquema de autenticación `ApimHeaders` (`Auth/ApimHeadersAuthenticationHandler.cs`): `X-User-Id` → NameIdentifier, `X-User-Email` → Email, `X-User-Role` → Role; sin `X-User-Id` no autentica y `[Authorize]` responde 401. Los endpoints REST deben usar `[Authorize]` y leer el usuario del NameIdentifier.

Tiempo real: `NotificationsHub` con `[Authorize]` en `/notifications/hub`. Con `Azure:SignalR:ConnectionString` usa Azure SignalR Service; sin ella, SignalR local con una advertencia (las pruebas fuerzan local en el factory). `SignalRNotificationDispatcher` es el `INotificationDispatcher` actual: invoca `notification` con `NotificationResponse` (`Contracts/`, la forma del item de `GET /notifications`, que deben reutilizar los endpoints REST). `title`/`body` no se guardan: los arma `INotificationRenderer` al enviar o consultar; hoy `DefaultNotificationRenderer` (textos fijos es/en, siempre `es` hasta que exista el directorio), que se reemplaza por las plantillas editables. Falta probar el negotiate contra un SignalR Service real (en modo Azure devuelve `url` + `accessToken`; en local, `connectionToken`).

Contrato: en `main` (PR #2). Andrés y Gabo confirmaron los campos de `data` de sus eventos; falta que Justin confirme `course.recommended` (y quedan las demás preguntas abiertas al final de `docs/contratos/README.md`). Las copias en `cvexpert-docs` las mantiene Justin en su rama de consolidación (`contratos/notificaciones.yaml`, `eventos/`, `docs/fuentes.md`); este repo no les abre PR, solo se le avisa el commit de origen cuando cambie el contrato.

Código pendiente, en este orden: endpoints REST (incluido el directorio, para usar el idioma del usuario) → FCM → correo. Al agregar FCM y correo, `INotificationDispatcher` pasa a ser uno compuesto que llama a cada canal y aísla los fallos de cada uno.

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
