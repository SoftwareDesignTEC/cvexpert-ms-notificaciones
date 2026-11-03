using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
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
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, HeaderUserIdProvider>();

// Mongo se resuelve solo cuando el consumidor arranca, así la API funciona sin cadena de conexión.
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));
builder.Services.AddSingleton<INotificationRepository, MongoNotificationRepository>();

// Consumidor de la cola de eventos.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<INotificationDispatcher, NullNotificationDispatcher>();
builder.Services.AddSingleton<NotificationEventHandler>();
builder.Services.AddHostedService<NotificationEventsConsumer>();

var app = builder.Build();

app.UseErrorResponses();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sin HTTPS redirection ni autenticación: TLS y validación de tokens los resuelve APIM.
app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();

// Expuesto para WebApplicationFactory en las pruebas de integración.
public partial class Program { }
