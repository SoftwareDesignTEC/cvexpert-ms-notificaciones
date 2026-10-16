using Notificaciones.Api.Hubs;
using Notificaciones.Api.Options;

var builder = WebApplication.CreateBuilder(args);

// Configuración (valores reales en user-secrets o Key Vault, nunca en appsettings.json).
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection(ServiceBusOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

var app = builder.Build();

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
