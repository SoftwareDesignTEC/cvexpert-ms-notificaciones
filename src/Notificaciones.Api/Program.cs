using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Notificaciones.Api.Auth;
using Notificaciones.Api.Errors;
using Notificaciones.Api.Hubs;
using Notificaciones.Api.Infrastructure.Email;
using Notificaciones.Api.Infrastructure.Firebase;
using Notificaciones.Api.Infrastructure.Mongo;
using Notificaciones.Api.Infrastructure.ServiceBus;
using Notificaciones.Api.Options;
using Notificaciones.Api.Services;
using Notificaciones.Api.Services.Channels;
using Notificaciones.Api.Services.Events;

var builder = WebApplication.CreateBuilder(args);

// Configuración (valores reales en user-secrets o Key Vault, nunca en appsettings.json).
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection(ServiceBusOptions.SectionName));

builder.Services.AddControllers().AddErrorResponses();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Identidad por los encabezados de APIM (X-User-Id, X-User-Email, X-User-Role).
builder.Services.AddAuthentication(ApimHeadersAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApimHeadersAuthenticationHandler>(ApimHeadersAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

// Con Azure:SignalR:ConnectionString se usa Azure SignalR Service (el negotiate entrega su URL y un token);
// sin ella, SignalR local para desarrollo y pruebas.
var azureSignalRConnectionString = builder.Configuration["Azure:SignalR:ConnectionString"];
var useAzureSignalR = !string.IsNullOrWhiteSpace(azureSignalRConnectionString);
var signalR = builder.Services.AddSignalR();
if (useAzureSignalR) signalR.AddAzureSignalR(azureSignalRConnectionString!);

// Mongo se resuelve la primera vez que se usa, así la API arranca sin cadena de conexión.
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));
builder.Services.AddSingleton<INotificationRepository, MongoNotificationRepository>();
builder.Services.AddSingleton<IDirectoryRepository, MongoDirectoryRepository>();
builder.Services.AddSingleton<IDeviceRepository, MongoDeviceRepository>();
builder.Services.AddSingleton<MongoIndexInitializer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MongoIndexInitializer>());

// Canales de entrega: tiempo real siempre; push y correo solo con su configuración.
builder.Services.AddSingleton<INotificationChannel, SignalRNotificationChannel>();

var firebase = builder.Configuration.GetSection(FirebaseOptions.SectionName).Get<FirebaseOptions>() ?? new();
var useFcm = !string.IsNullOrWhiteSpace(firebase.ServiceAccountJson);
if (useFcm)
{
    builder.Services.AddSingleton<IFcmSender>(_ => new FirebaseFcmSender(firebase.ServiceAccountJson));
    builder.Services.AddSingleton<INotificationChannel, FcmNotificationChannel>();
}

var acs = builder.Configuration.GetSection(AcsOptions.SectionName).Get<AcsOptions>() ?? new();
var email = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new();
var useEmail = !string.IsNullOrWhiteSpace(acs.ConnectionString) && !string.IsNullOrWhiteSpace(email.Sender);
if (useEmail)
{
    builder.Services.AddSingleton<IEmailSender>(_ => new AcsEmailSender(acs.ConnectionString, email.Sender));
    builder.Services.AddSingleton<IEmailComposer, DefaultEmailComposer>();
    builder.Services.AddSingleton<INotificationChannel, EmailNotificationChannel>();
}

// Consumidor de la cola de eventos y entrega por los canales.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<INotificationRenderer, DefaultNotificationRenderer>();
builder.Services.AddSingleton<INotificationDispatcher, CompositeNotificationDispatcher>();
builder.Services.AddSingleton<NotificationEventHandler>();
builder.Services.AddHostedService<NotificationEventsConsumer>();

var app = builder.Build();

if (!useAzureSignalR)
    app.Logger.LogWarning("Falta Azure:SignalR:ConnectionString: se usa SignalR local (solo para desarrollo y pruebas)");
if (!useFcm)
    app.Logger.LogWarning("Falta Firebase:ServiceAccountJson: el push por FCM está desactivado");
if (!useEmail)
    app.Logger.LogWarning("Falta Acs:ConnectionString o Email:Sender: el correo está desactivado");

app.UseErrorResponses();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sin HTTPS redirection: TLS y validación de tokens los resuelve APIM.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>(NotificationsHub.Path);

app.Run();

// Expuesto para WebApplicationFactory en las pruebas de integración.
public partial class Program { }
