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

Hecho: estructura base, `/health`, formato de errores (`Errors/`), pruebas de integración (`NotificacionesApiFactory`: sin consumidor, repositorios en memoria, SignalR local, sin push ni correo), borrador del contrato en `docs/contratos/` (versión 0.1, validado), consumidor de la cola con persistencia en Mongo (PR #3), tiempo real con Azure SignalR Service (PR #5), CI/CD con GitHub Actions al App Service `cvexpert-notificaciones` (PR #6), endpoints REST de directorio, dispositivos y centro de notificaciones (PR #7), canales push (FCM) y correo (ACS) (PR #8), solo llamadas de APIM con certificado de cliente.

Certificado de APIM: `ApimCertificateMiddleware` (`Auth/`) corre antes de la autenticación si `Apim:CertificateThumbprint` tiene huellas (separadas por coma para rotar; sin mayúsculas ni espacios). Lee `X-ARR-ClientCert` (DER en base64), compara la huella y la vigencia, y responde 403 `{ status, message }` si algo falla; `/health` excluido. Sin la clave no se activa (advertencia; el factory de pruebas la deja vacía). Solo es seguro con el App Service en Client certificate mode = Required y `/health` como ruta excluida: el certificado es público y, sin ese modo, el encabezado se puede falsificar. Falta configurarlo en Azure (APIM presenta el certificado → `Apim__CertificateThumbprint` → modo Required) y probarlo con la llamada directa con encabezado falso descrita en el README.

Consumidor (`Infrastructure/ServiceBus/NotificationEventsConsumer.cs`): valida con `NotificationEventValidator` (escrito a mano: si cambia el esquema, cambiarlo también ahí y en sus pruebas), guarda en `notifications` y llama a `INotificationDispatcher`. Inválido → dead-letter; duplicado → completa; Mongo caído → abandona tras 5 s; fallo del dispatcher → completa igual. Sin `ServiceBus:*` o `Mongo:*` no arranca y la API sigue funcionando. No consume hasta que `MongoIndexInitializer` termina. Falta probarlo contra Cosmos y Service Bus reales (en especial que un `eventId` repetido llegue como `DuplicateKey`).

Mongo: colecciones `notifications`, `directory` (`_id` = userId) y `devices`. Repositorios detrás de interfaces (`Services/I*Repository.cs`, implementaciones en `Infrastructure/Mongo/`; en las pruebas, `InMemoryRepositories.cs`). `MongoIndexInitializer` crea todos los índices al arrancar si `Mongo:*` está configurado (en Cosmos, el único solo se crea con la colección vacía); un índice nuevo va en el `EnsureIndexesAsync` del repositorio y en el inicializador. Las consultas reales a Mongo no tienen pruebas automáticas.

Identidad (solo se confía en ella porque la llamada viene de APIM, ver "Certificado de APIM"): esquema de autenticación `ApimHeaders` (`Auth/ApimHeadersAuthenticationHandler.cs`): `X-User-Id` → NameIdentifier, `X-User-Email` → Email, `X-User-Role` → Role; sin `X-User-Id` no autentica y `[Authorize]` responde 401 con "Falta el encabezado X-User-Id.". Los endpoints usan `[Authorize]` y `User.GetUserId()`; el usuario nunca sale del cuerpo ni de la ruta.

Endpoints (`Controllers/`), según `openapi.yaml`: `PUT /notifications/directory/me` (sin `X-User-Email` → 401), `POST`/`DELETE /notifications/devices` (solo `platform: android`; DELETE siempre 204 y solo borra lo propio), `GET /notifications` (`pageSize` 1–100, fuera de rango 400), `GET /notifications/unread-count`, `PATCH /notifications/{id}/read` (404 si no es propia; conserva `readAt`) y `POST /notifications/read-all`. Validaciones con DataAnnotations para que salgan con el formato de errores.

Entrega: `INotificationDispatcher` es `CompositeNotificationDispatcher` (`Services/Channels/`): lee el directorio una vez (si falla, sigue en `es` y sin correo), arma los textos y llama en paralelo a cada `INotificationChannel` registrado con un `NotificationDelivery` (notificación, entrada del directorio o null, idioma, textos); el fallo de un canal se registra y no afecta a los demás. Un canal sin configuración no se registra (advertencia al arrancar). `title`/`body` no se guardan: los arma `INotificationRenderer` al enviar o consultar; hoy `DefaultNotificationRenderer` (textos fijos es/en), que se reemplaza por las plantillas editables.

Tiempo real: `NotificationsHub` con `[Authorize]` en `/notifications/hub`. Con `Azure:SignalR:ConnectionString` usa Azure SignalR Service; sin ella, SignalR local con una advertencia (las pruebas fuerzan local en el factory). `SignalRNotificationChannel` invoca `notification` con `NotificationResponse` (`Contracts/`, la misma forma que los items de `GET /notifications`). Falta probar el negotiate contra un SignalR Service real (en modo Azure devuelve `url` + `accessToken`; en local, `connectionToken`).

Push: `FcmNotificationChannel` (con `Firebase:ServiceAccountJson`) envía a todos los tokens del usuario `notification { title, body }` y `data { notificationId, type }` (formato en la sección 6 de `docs/contratos/README.md`). Borra solo los tokens `Unregistered`; `SenderIdMismatch` e `InvalidArgument` solo se registran (una service account equivocada no debe borrar tokens válidos). El SDK está detrás de `IFcmSender` (`Infrastructure/Firebase/FirebaseFcmSender.cs`, crea la app de Firebase al primer uso). FirebaseAdmin 3.7 marca `MulticastMessage.Tokens` como obsoleto en favor de FIDs (Firebase Installation IDs): pasar a FIDs cambia lo que registra Android y es un cambio de contrato pendiente de acordar.

Correo: `EmailNotificationChannel` (con `Acs:ConnectionString` y `Email:Sender`) solo si el usuario está en el directorio y el tipo está en `EmailNotificationChannel.EmailTypes` (hoy todos menos `job.new`, hasta decidir la pregunta 7 de preferencias por canal). `IEmailComposer`/`DefaultEmailComposer` arma asunto = `title` y HTML simple escapado con pie "CVExpert" (se reemplaza por plantillas editables); `IEmailSender`/`AcsEmailSender` envía con `WaitUntil.Started`. Los adaptadores de Firebase y ACS no tienen pruebas automáticas.

Contrato: en `main` (PR #2). Todos los productores confirmaron los campos de `data` de sus eventos; `course.recommended` está confirmado en el contrato, pero Brecha/Cursos aún no publica el evento. Quedan las demás preguntas abiertas al final de `docs/contratos/README.md`. Las copias en `cvexpert-docs` las mantiene Justin en su rama de consolidación (`contratos/notificaciones.yaml`, `eventos/`, `docs/fuentes.md`); este repo no les abre PR, solo se le avisa el commit de origen cuando cambie el contrato.

Código pendiente: plantillas editables (`/notifications/templates`, requiere `X-User-Role: admin`), que reemplazan a `DefaultNotificationRenderer` y `DefaultEmailComposer`; preferencias por canal si el grupo las decide (pregunta 7). Para que todo funcione de punta a punta: quien administra APIM debe conectar las operaciones, y en Azure faltan el proyecto de Firebase (`Firebase--ServiceAccountJson` en Key Vault) y el recurso de ACS con dominio verificado (`Acs--ConnectionString` en Key Vault, `Email__Sender` en el App Service).

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
