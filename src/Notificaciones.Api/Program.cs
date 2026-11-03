using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Notificaciones.Api.Auth;
using Notificaciones.Api.Errors;
using Notificaciones.Api.Hubs;
using Notificaciones.Api.Infrastructure.Mongo;
using Notificaciones.Api.Infrastructure.ServiceBus;
using Notificaciones.Api.Options;
using Notificaciones.Api.Services;
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

// Mongo se resuelve solo cuando el consumidor arranca, así la API funciona sin cadena de conexión.
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));
builder.Services.AddSingleton<INotificationRepository, MongoNotificationRepository>();

// Consumidor de la cola de eventos y entrega por los canales.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<INotificationRenderer, DefaultNotificationRenderer>();
builder.Services.AddSingleton<INotificationDispatcher, SignalRNotificationDispatcher>();
builder.Services.AddSingleton<NotificationEventHandler>();
builder.Services.AddHostedService<NotificationEventsConsumer>();

var app = builder.Build();

if (!useAzureSignalR)
    app.Logger.LogWarning("Falta Azure:SignalR:ConnectionString: se usa SignalR local (solo para desarrollo y pruebas)");

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
