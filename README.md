# cvexpert-ms-notificaciones

Microservicio de **Notificaciones** de CVExpert. Consume eventos de otros servicios desde una cola de Azure Service Bus, los guarda en MongoDB y los entrega al usuario en tiempo real a la web (SignalR), como push al móvil (Firebase Cloud Messaging) y por correo con plantillas en español e inglés.

CVExpert (Fase II de Diseño de Software, TEC) son cinco microservicios, cada uno en su repositorio, detrás de **Azure API Management (APIM)**, con clientes web (React) y Android. Este servicio notifica cuatro eventos: cambio de estado de una postulación (`application.status_changed`), CV generado (`cv.generated`), nueva oferta (`job.new`) y curso recomendado (`course.recommended`). Todos los canales (tiempo real, push y correo) son responsabilidad de este servicio; web y móvil consultan el mismo historial por REST.

**Stack:** .NET 8 (ASP.NET Core Web API con controllers), `MongoDB.Driver` sobre Cosmos DB for MongoDB, `Azure.Messaging.ServiceBus`, SignalR con Azure SignalR Service (`Microsoft.Azure.SignalR`, tier Free), `FirebaseAdmin` (FCM), Azure Communication Services (correo), Swashbuckle (Swagger) y xUnit con `Microsoft.AspNetCore.Mvc.Testing`.

## Estado

**En producción y probado** (App Service `cvexpert-notificaciones`):
- Consumidor de la cola contra Service Bus y Cosmos DB, incluido el descarte de duplicados (2026-11-04: se reenvió el `eventId` `d112ddfb-e5c5-4d92-8f6f-c3081119e6f5` y el log dijo "duplicado: ya estaba guardado; se completa").
- Centro de notificaciones y tiempo real por APIM desde la web y Android, incluido el negotiate.
- Push con FCM (`Firebase--ServiceAccountJson` en Key Vault) y correo con ACS (dominio verificado, `Acs--ConnectionString` en Key Vault, `Email__Sender` en el App Service).
- Solo llamadas de APIM con certificado de cliente (2026-11-06, ver "Solo llamadas de APIM").
- Todas las operaciones conectadas en APIM (`cv-api`); las 3 de plantillas desde el 2026-11-11.

**Pendiente o fuera de esta fase:**
- Push en Android: que la app registre su token de FCM y muestre el aviso queda fuera de esta fase (el backend ya envía).
- Editor de plantillas en la web: falta que el administrador de APIM agregue el correo de Samuel a la lista de administradores para probarlo.
- Preferencias por canal (pregunta abierta 7 del contrato): sin decidir; mientras tanto `job.new` no se envía por correo.
- FIDs de Firebase: FirebaseAdmin 3.7 marca `MulticastMessage.Tokens` como obsoleto en favor de los Firebase Installation IDs. Pasar a FIDs cambia lo que registra Android, así que es un cambio de contrato pendiente de acordar.
- `course.recommended` está confirmado en el contrato, pero Brecha/Cursos todavía no publica el evento.

## Contratos

La fuente de verdad es [`docs/contratos/`](docs/contratos/): `evento-notificacion.schema.json` (mensaje de la cola `notificaciones-eventos`), `openapi.yaml` (endpoints REST) y `README.md` (cómo publicar eventos, plantillas, SignalR, push y preguntas abiertas). **Si el código y el contrato no coinciden, manda el contrato:** cualquier cambio se acuerda con el equipo y se actualiza ahí primero.

- Las copias en `cvexpert-docs` las mantiene Justin en su rama de consolidación (`contratos/notificaciones.yaml`, `eventos/`, `docs/fuentes.md`). Este repo no les abre PR: cuando cambie el contrato, se le avisa el commit de origen.
- `NotificationEventValidator` valida la cola y está escrito a mano: si cambia el esquema, hay que cambiarlo también ahí y en sus pruebas.

```bash
# Validar los contratos (requiere Node)
npx @redocly/cli lint docs/contratos/openapi.yaml
npx ajv-cli validate --spec=draft2020 -c ajv-formats -s docs/contratos/evento-notificacion.schema.json -d <ejemplo>.json
```

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
| `Firebase:ServiceAccountJson` | JSON completo de la service account de Firebase (push a Android) | Push desactivado |
| `Acs:ConnectionString` | Cadena de Azure Communication Services (correo) | Correo desactivado |
| `Email:Sender` | Remitente de un dominio verificado en ACS, por ejemplo `DoNotReply@<dominio>.azurecomm.net`. No es secreto | Correo desactivado |
| `Apim:CertificateThumbprint` | Huellas SHA-1 del certificado de cliente de APIM, separadas por coma (ver "Solo llamadas de APIM"). No es secreto | Se aceptan llamadas que no vienen de APIM (solo para desarrollo y pruebas) |

Sin alguna de estas claves, la API sigue funcionando; solo se registra una advertencia.

```bash
dotnet user-secrets --project src/Notificaciones.Api set "Mongo:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets --project src/Notificaciones.Api set "ServiceBus:ConnectionString" "Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<politica-listen>;SharedAccessKey=<clave>;EntityPath=notificaciones-eventos"
dotnet user-secrets --project src/Notificaciones.Api set "ServiceBus:QueueName" "notificaciones-eventos"
dotnet user-secrets --project src/Notificaciones.Api set "Azure:SignalR:ConnectionString" "Endpoint=https://<recurso>.service.signalr.net;AccessKey=<clave>;Version=1.0;"
dotnet user-secrets --project src/Notificaciones.Api set "Acs:ConnectionString" "endpoint=https://<recurso>.communication.azure.com/;accesskey=<clave>"
dotnet user-secrets --project src/Notificaciones.Api set "Email:Sender" "DoNotReply@<dominio>.azurecomm.net"

# Ver los valores configurados
dotnet user-secrets --project src/Notificaciones.Api list
```

Los valores anteriores son de ejemplo; pide los reales al equipo.

La service account de Firebase es un JSON de varias líneas, con espacios y comillas, y en PowerShell 5.1 no se puede pasar como argumento. Se carga así, desde la raíz del repo, con el archivo descargado **fuera del repo**:

```powershell
$sa = "$HOME\Downloads\service-account.json"   # Consola de Firebase → Configuración del proyecto → Cuentas de servicio
$tmp = "$env:TEMP\firebase-secret.json"
[IO.File]::WriteAllText($tmp, (@{ 'Firebase:ServiceAccountJson' = [IO.File]::ReadAllText($sa) } | ConvertTo-Json -Compress))
cmd /c "dotnet user-secrets --project src/Notificaciones.Api set < `"$tmp`""
Remove-Item $tmp
```

Así se guarda un solo secreto y se conservan los que ya estaban. No uses `Get-Content` ni la tubería de PowerShell: agregan un BOM o propiedades extra, y `dotnet user-secrets` no puede leer el JSON.

- Para Service Bus usa una política de acceso compartido **de la cola**, solo con permiso **Listen**. No uses `RootManageSharedAccessKey`: da control total sobre todo el namespace. Si la cadena incluye `EntityPath`, debe coincidir con `ServiceBus:QueueName`. En Azure, mejor Managed Identity que una cadena SAS.
- En Cosmos DB con API de MongoDB la cadena de conexión debe llevar `retryWrites=false`.
- En Azure las mismas claves van en Key Vault con `--` en lugar de `:` (por ejemplo `ServiceBus--ConnectionString`). Los secretos de los canales son `Firebase--ServiceAccountJson` (el contenido del JSON, por ejemplo con `az keyvault secret set --vault-name kv-cvexpert --name Firebase--ServiceAccountJson --file service-account.json`) y `Acs--ConnectionString`. `Email__Sender` y `Apim__CertificateThumbprint` van como configuración normal del App Service.

## Solo llamadas de APIM (certificado de cliente)

APIM presenta un certificado de cliente al llamar al backend, y el App Service lo pasa en el encabezado `X-ARR-ClientCert` (DER en base64). Con `Apim:CertificateThumbprint` configurado, `ApimCertificateMiddleware` corre antes de la autenticación y responde **403** `{ status, message }` si:
- falta el encabezado o no se puede leer,
- la huella no es ninguna de las configuradas (sin distinguir mayúsculas e ignorando espacios),
- o la fecha actual está fuera de `NotBefore`–`NotAfter`.

El motivo queda en el log (con la huella, nunca el certificado), como advertencia; los pings internos del App Service (`GET /` de Always On y `GET /robots933456.txt` del arranque) también reciben 403, pero se registran en Debug para no llenar el log. `/health` está excluido. Sin la clave, el middleware no se activa y se registra una advertencia.

**Importante:** el certificado de APIM es público. El chequeo solo prueba que la llamada viene de APIM si el App Service está en **Client certificate mode = Required**: así es el App Service quien llena `X-ARR-ClientCert` después de verificar en el TLS que el cliente tiene la clave privada. Sin ese modo, cualquiera podría mandar el encabezado a mano con el certificado público.

### Pasos en Azure (en este orden)

Aplicados en producción el 2026-11-06 con el certificado `cvexpert-client-cert`. Se dejan como guía para rotar el certificado o rehacer el App Service.

1. **APIM** (quien lo administra): subir el certificado de cliente (`.pfx`) y configurar el backend de la API de Notificaciones para que lo presente.
2. **Huella**: obtenerla del certificado público (`.cer`):
   ```powershell
   (New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 "$HOME\Downloads\apim.cer").Thumbprint
   ```
3. **App Service `cvexpert-notificaciones` → Configuración**: agregar `Apim__CertificateThumbprint` = la huella. Es configuración normal, no Key Vault.
4. **App Service → Configuración → General → Certificados de cliente entrantes**: *Client certificate mode* = **Required** y *Certificate exclusion paths* = `/health`. Sin la exclusión, el chequeo de `/health` del workflow de CI/CD y el Health check del portal fallan, porque no presentan certificado.

Si el App Service exige el certificado antes de que APIM lo presente, todas las llamadas fallan. Por eso el paso 1 va primero.

**Rotación:** cuando el certificado de APIM vaya a cambiar, configura las dos huellas (`VIEJA,NUEVA`), cambia el certificado en APIM y después quita la vieja.

### Comprobar después de desplegar

```powershell
$app = "https://cvexpert-notificaciones-adcybhbkf6a0d9ba.eastus-01.azurewebsites.net"
$certPublico = [Convert]::ToBase64String([IO.File]::ReadAllBytes("$HOME\Downloads\apim.cer"))

# 1. /health directo, sin certificado: 200
curl.exe -i "$app/health"

# 2. Directo al App Service, sin certificado pero con un X-ARR-ClientCert falso hecho con el certificado público de APIM:
#    debe rechazarse (403 o error de TLS del App Service). Si responde 200, el App Service no está en modo Required.
curl.exe -i "$app/notifications/unread-count" -H "X-User-Id: prueba" -H "X-ARR-ClientCert: $certPublico"

# 3. A través de APIM, con un token de Google válido: 200
curl.exe -i "https://<apim>.azure-api.net/notifications/unread-count" -H "Authorization: Bearer <token>"
```

## Consumidor de la cola

`NotificationEventsConsumer` lee la cola `notificaciones-eventos` (PeekLock, hasta 4 mensajes a la vez) y por cada mensaje:

1. Lo valida contra [`docs/contratos/evento-notificacion.schema.json`](docs/contratos/evento-notificacion.schema.json). Si no es JSON válido o no cumple el esquema, lo manda a la dead-letter sin reintentar (`InvalidJson` o `SchemaValidationFailed`, con el detalle en la descripción).
2. Lo guarda en la colección `notifications`. Si el `eventId` ya existe es un duplicado: se completa sin hacer nada más.
3. Lo entrega por los canales (ver "Canales de entrega"). Si la entrega falla, el mensaje se completa igual porque ya está guardado.
4. Si Mongo falla, espera 5 s y abandona el mensaje para que Service Bus lo reintente; al agotar `MaxDeliveryCount`, Service Bus lo pasa a la dead-letter.

No consume hasta que existan los índices (ver "Colecciones e índices"): sin el índice único de `eventId` no hay deduplicación.

## Colecciones e índices

`MongoIndexInitializer` crea los índices al arrancar, siempre que `Mongo:*` esté configurado, y reintenta cada 30 s si Mongo no responde. En Cosmos DB un índice único solo se puede crear con la colección vacía, así que el servicio debe arrancar antes de que haya datos.

**Cosmos DB for MongoDB (RU):** todo sort por varios campos necesita un índice compuesto con esos mismos campos y direcciones; si no, la consulta falla con "The order by query does not have a corresponding composite index". El de `GET /notifications` (`createdAt_desc_id_desc`) se creó a mano en producción con el mismo nombre y forma que en el código, así que crearlo de nuevo no hace nada; una prueba compara sus claves con el sort.

Un índice nuevo va en el `EnsureIndexesAsync` de su repositorio (`Infrastructure/Mongo/`), que es lo que corre `MongoIndexInitializer`; un sort nuevo necesita su índice compuesto.

| Colección | Documento | Índices |
|---|---|---|
| `notifications` | `eventId`, `userId`, `type`, `source`, `data`, `occurredAt`, `createdAt`, `readAt` | `eventId` único; `userId` + `createdAt` desc; `userId` + `readAt`; `createdAt` desc + `_id` desc (`createdAt_desc_id_desc`, el orden de `GET /notifications`) |
| `directory` | `_id` = userId, `email`, `language`, `updatedAt` | solo `_id` |
| `devices` | `token`, `userId`, `platform`, `registeredAt` | `token` único; `userId` |
| `templates` | `_id` = `"{type}:{language}"`, `type`, `language`, `title`, `body`, `updatedAt`, `updatedBy` | solo `_id` (la colección se crea con el primer `PUT`) |

## Endpoints REST

Todos requieren `X-User-Id` (si falta, 401) y solo ven o modifican datos de ese usuario. El usuario nunca sale del cuerpo ni de la ruta. La fuente de verdad de rutas, cuerpos y respuestas es [`docs/contratos/openapi.yaml`](docs/contratos/openapi.yaml).

| Ruta | Qué hace |
|---|---|
| `PUT /notifications/directory/me` | `{ language: "es" \| "en" }`. Guarda el idioma y el correo de `X-User-Email` (sin ese encabezado, 401). 200 con `{ userId, email, language }`. |
| `POST /notifications/devices` | `{ token, platform: "android" }`. 201 si el token es nuevo; 200 si ya existía (si era de otro usuario, pasa al actual). |
| `DELETE /notifications/devices/{token}` | 204 siempre. Solo quita el token si es del usuario. El token va codificado para URL. |
| `GET /notifications?page&pageSize&unread` | Más recientes primero, como `{ totalRecords, page, pageSize, items }`. `page` ≥ 1 y `pageSize` de 1 a 100 (por defecto 1 y 10); fuera de rango, 400. `unread=true` solo no leídas, `false` solo leídas. |
| `GET /notifications/unread-count` | `{ count }`. |
| `PATCH /notifications/{id}/read` | 200 con la notificación. Si ya estaba leída conserva su `readAt`. 404 si no existe o es de otro usuario. |
| `POST /notifications/read-all` | 204. Marca como leídas todas las del usuario. |

`title` y `body` de cada notificación se arman al responder, en el idioma del usuario según el directorio (`es` si no está registrado), con las plantillas (ver "Plantillas").

Cosmos DB está en Central US y el App Service en East US, así que cada consulta es un viaje entre regiones. Por eso `GET /notifications` pide en paralelo el total, la página, el idioma y las plantillas, y `PATCH /notifications/{id}/read` pide el idioma y las plantillas mientras marca. Una prueba (`ConsultasEnParaleloTests`) falla si vuelven a ir una tras otra.

### Plantillas (solo administradores)

Exigen además `X-User-Role: admin` (política `Admin`): sin `X-User-Id`, 401; con identidad pero sin ese rol (exacto, en minúsculas), 403 `{ status: 403, message: "Se requiere rol de administrador." }`. En APIM (`cv-api`), `X-User-Role: admin` solo se agrega si el correo del token está en la lista de administradores; si no, APIM responde 403 sin llegar al backend.

| Ruta | Qué hace |
|---|---|
| `GET /notifications/templates` | Las 8 plantillas (4 tipos × `es`/`en`) como arreglo, en orden fijo. |
| `GET /notifications/templates/{type}/{language}` | Una plantilla. `type` o `language` fuera del contrato, 400. |
| `PUT /notifications/templates/{type}/{language}` | `{ title, body }`, los dos obligatorios. `updatedAt` lo fija el servidor y `updatedBy` sale de `X-User-Email` (sin ese encabezado, 401). 200 con la plantilla guardada. |

- **Texto por defecto:** una plantilla que nunca se guardó se devuelve con el texto de `DefaultTemplates`, `updatedAt: 0` y sin `updatedBy`.
- **Marcadores:** `{{campo}}` solo con los campos de `data` de ese tipo (los de `NotificationEventValidator.DataFields`, que una prueba compara con `evento-notificacion.schema.json`). Cualquier otro `{{...}}`, incluidos `{{ title }}` o `{{Title}}`, responde 400 con `errors.title` o `errors.body` nombrando el marcador.
- **Al renderizar** (`TemplateNotificationRenderer`): se usa la plantilla guardada y, si no hay, el texto por defecto. `newStatus`, `previousStatus` y `modality` se traducen al idioma, `isPaid` sale como "gratis"/"de pago" y un marcador sin valor queda vacío. Si no se pueden leer las plantillas, se usa el texto por defecto para no frenar la entrega.
- **Caché:** las plantillas se leen todas juntas y se guardan en memoria 60 s; un `PUT` vacía la caché de la instancia que lo recibe. **Con varias instancias del App Service**, las demás pueden seguir con el texto anterior hasta 60 s.

## Canales de entrega

`CompositeNotificationDispatcher` lee el directorio una vez (idioma y correo; `es` y sin correo si el usuario no está registrado o si no se puede leer el directorio), arma `title` y `body` y llama en paralelo a cada canal activo. Si un canal falla, se registra con su nombre y los demás siguen. Un canal sin su configuración no se registra (advertencia al arrancar). `title` y `body` no se guardan: se arman al enviar o consultar.

| Canal | Cuándo envía | Qué envía |
|---|---|---|
| **SignalR** (`SignalRNotificationChannel`) | Siempre; solo llega si el usuario tiene la web abierta | `notification` con la forma de un item de `GET /notifications` |
| **Push FCM** (`FcmNotificationChannel`) | Con `Firebase:ServiceAccountJson`, a todos los tokens del usuario | `notification { title, body }` y `data { notificationId, type }` (ver sección 6 de [`docs/contratos/README.md`](docs/contratos/README.md)). Borra los tokens que FCM marca como `Unregistered`; `SenderIdMismatch` e `InvalidArgument` solo se registran, porque una service account equivocada no debe borrar tokens válidos. |
| **Correo** (`EmailNotificationChannel`) | Con `Acs:ConnectionString` y `Email:Sender`, si el usuario está en el directorio y el tipo está en `EmailNotificationChannel.EmailTypes` | Asunto = `title`; HTML simple con `title`, `body` y el pie "CVExpert" (`DefaultEmailComposer`). La plantilla es texto plano: `title` y `body` se escapan completos para HTML (también los valores del evento) y los saltos de línea del `body` pasan a `<br>`. ACS lo acepta sin esperar a la entrega. |

`job.new` no se envía por correo hasta decidir las preferencias por canal (pregunta abierta 7 del contrato): para cambiarlo, edita `EmailNotificationChannel.EmailTypes`.

## Tiempo real (SignalR)

El APIM es Consumption y no soporta WebSockets, así que se usa **Azure SignalR Service**:

1. **Negotiate (vía APIM).** La librería cliente hace `POST /notifications/hub/negotiate`. APIM agrega `X-User-Id` y el backend responde con la `url` del SignalR Service y un `accessToken` emitido para ese usuario. Sin `X-User-Id` responde 401.
2. **Conexión (directa).** El cliente se conecta al SignalR Service con esa `url` y ese token, sin pasar por APIM ni por este backend. La identidad viaja dentro del token.
3. **Envío.** Cuando llega una notificación, `SignalRNotificationChannel` invoca `notification` en las conexiones del usuario (`IHubContext<NotificationsHub>.Clients.User(userId)`). El argumento tiene la misma forma que un item de `GET /notifications` (`id`, `type`, `title`, `body`, `data`, `read`, `createdAt`).

`title` y `body` se arman al enviar, en el idioma del directorio (`es` si el usuario no está registrado), con las plantillas (ver "Plantillas").

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

Las pruebas de integración levantan la API en memoria (`NotificacionesApiFactory`, que quita el consumidor de la cola, usa repositorios en memoria, fuerza SignalR local, deja push y correo desactivados y no exige el certificado de APIM), sin Mongo, Service Bus, Azure SignalR, Firebase ni ACS, aunque haya user-secrets configurados. El validador, el handler del consumidor y los canales se prueban con dobles (`IFcmSender` e `IEmailSender` aíslan los SDK). Las consultas reales a Mongo (`Infrastructure/Mongo/`) y los adaptadores de Firebase y ACS no tienen pruebas automáticas.

### Prueba manual

Con el servicio corriendo (`dotnet run --project src/Notificaciones.Api`). En Windows PowerShell 5.1 usa `curl.exe` en vez de `curl`, que ahí es alias de `Invoke-WebRequest`:

```bash
# 200 { "status": "ok" }
curl -i http://localhost:5029/health

# 404 { "status": 404, "message": "Not Found" }
curl -i http://localhost:5029/no-existe

# SignalR sin X-User-Id: 401 { "status": 401, "message": "Falta el encabezado X-User-Id." }
curl -i -X POST "http://localhost:5029/notifications/hub/negotiate?negotiateVersion=1"

# Centro de notificaciones (necesita Mongo configurado)
curl -i "http://localhost:5029/notifications?page=1&pageSize=10" -H "X-User-Id: usuario-1"
curl -i -X PUT http://localhost:5029/notifications/directory/me -H "X-User-Id: usuario-1" -H "X-User-Email: ana@ejemplo.com" -H "Content-Type: application/json" -d '{"language":"en"}'

# Plantillas (necesita Mongo): sin rol admin 403; con rol, 200
curl -i http://localhost:5029/notifications/templates -H "X-User-Id: usuario-1"
curl -i http://localhost:5029/notifications/templates -H "X-User-Id: admin-1" -H "X-User-Role: admin"
curl -i -X PUT http://localhost:5029/notifications/templates/cv.generated/es -H "X-User-Id: admin-1" -H "X-User-Email: admin@ejemplo.com" -H "X-User-Role: admin" -H "Content-Type: application/json" -d '{"title":"Tu CV está listo","body":"Ya puedes ver tu CV \"{{title}}\"."}'

# SignalR con X-User-Id (en producción lo agrega APIM): 200; en local trae connectionToken, con Azure trae url y accessToken
curl -i -X POST "http://localhost:5029/notifications/hub/negotiate?negotiateVersion=1" -H "X-User-Id: usuario-1"
```

## Reglas de diseño y convenciones

- **Sin llamadas entre servicios:** ningún servicio llama a otro; los eventos llegan por la cola. Solo APIM llama a este backend.
- **Identidad**: APIM valida el token de Google y agrega `X-User-Id` (el `sub` del token, el mismo en todos los servicios; APIM lo sobrescribe siempre), `X-User-Email` y, en rutas de administración, `X-User-Role`. Este servicio no valida tokens y solo confía en esos encabezados porque la llamada trae el certificado de APIM. Los encabezados solo llegan si la operación está conectada en APIM. En el código: esquema `ApimHeaders`; las rutas de usuario llevan `[Authorize]`, responden 401 sin `X-User-Id` y leen el usuario con `User.GetUserId()`, nunca del cuerpo ni de la ruta.
- **Fechas**: siempre epoch en milisegundos UTC (`long`), nunca texto.
- **Secretos**: ninguna URL, cadena de conexión ni credencial en el código ni en `appsettings.json` (local: `dotnet user-secrets`; Azure: Key Vault).
- **Errores**: siempre `{ status, message, errors? }`; `errors` (`{ campo: mensaje }`) solo aparece en 400. Las validaciones usan DataAnnotations para salir con este formato.
- **Paginación**: `{ totalRecords, page, pageSize, items }`.
- **SignalR**: hub `/notifications/hub` con `[Authorize]`; mensaje `notification` con la forma de un item de `GET /notifications`. Para enviarle algo a un usuario: `IHubContext<NotificationsHub>.Clients.User(userId)`.

## Estructura

```
cvexpert-ms-notificaciones/
├── .github/workflows/deploy.yml   # CI/CD (ver "CI/CD")
├── docs/contratos/                # Contrato: esquema del evento, openapi.yaml y guía para los productores
├── Notificaciones.sln
├── src/Notificaciones.Api/
│   ├── Auth/              # Certificado de cliente de APIM y autenticación por sus encabezados (esquema ApimHeaders)
│   ├── Contracts/         # Cuerpos y respuestas del contrato (NotificationResponse, TemplateResponse...)
│   ├── Controllers/       # Endpoints HTTP: salud, directorio, dispositivos, centro de notificaciones y plantillas
│   ├── Errors/            # Formato único de errores { status, message, errors? }
│   ├── Hubs/              # Hub de SignalR y canal de tiempo real (SignalRNotificationChannel)
│   ├── Domain/            # Documentos de Mongo (Notification, DirectoryEntry, Device, NotificationTemplate)
│   ├── Infrastructure/    # MongoDB (repositorios e índices), Service Bus (consumidor), Firebase y ACS (adaptadores de los SDK)
│   ├── Options/           # Clases de configuración (patrón Options)
│   ├── Services/          # Lógica de negocio: eventos de la cola, plantillas y textos (Templates/), canales de entrega (Channels/) e interfaces de persistencia
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
└── tests/Notificaciones.Tests/   # Pruebas xUnit
```

## CI/CD

El workflow [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml) (**CI/CD Notificaciones**) corre en GitHub Actions:

- **En cada PR hacia `main`:** restaura, compila en Release, corre las pruebas y publica el artefacto. No despliega.
- **En cada push a `main`** (al fusionar un PR): lo mismo y además despliega en el App Service `cvexpert-notificaciones` y comprueba que `/health` responda 200.
- **A mano:** desde la pestaña Actions → *CI/CD Notificaciones* → *Run workflow*, eligiendo la rama. Sirve para probar una rama en Azure antes de fusionarla, pero despliega sobre el mismo App Service: esa versión queda publicada hasta el siguiente despliegue de `main`.

Los despliegues nunca corren dos a la vez. El único secreto del repo es `AZURE_WEBAPP_PUBLISH_PROFILE`. La configuración (`Mongo__*`, `ServiceBus__*`, `Azure__SignalR__ConnectionString`, `Firebase__ServiceAccountJson`, `Acs__ConnectionString`, `Email__Sender` y `Apim__CertificateThumbprint`) vive en el App Service y en Key Vault (`kv-cvexpert`), no en el repo ni en el workflow; las pruebas no la necesitan.

## Forma de trabajo

- `main` está protegida: no se hace push directo.
- Todo cambio entra por Pull Request y debe ser revisado y aprobado por otro integrante del equipo.
