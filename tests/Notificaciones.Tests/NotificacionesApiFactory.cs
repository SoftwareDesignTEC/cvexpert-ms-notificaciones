using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Notificaciones.Api.Auth;
using Notificaciones.Api.Infrastructure.ServiceBus;
using Notificaciones.Api.Services;

namespace Notificaciones.Tests;

/// <summary>
/// API en memoria sin el consumidor de la cola, con repositorios en memoria, SignalR local y sin push ni correo:
/// aunque haya user-secrets con cadenas reales, las pruebas no se conectan a Service Bus, Mongo, Azure SignalR Service,
/// Firebase ni Azure Communication Services.
/// </summary>
public class NotificacionesApiFactory : WebApplicationFactory<Program>
{
    public InMemoryNotificationRepository Notificaciones => (InMemoryNotificationRepository)Services.GetRequiredService<INotificationRepository>();
    public InMemoryDirectoryRepository Directorio => (InMemoryDirectoryRepository)Services.GetRequiredService<IDirectoryRepository>();
    public InMemoryDeviceRepository Dispositivos => (InMemoryDeviceRepository)Services.GetRequiredService<IDeviceRepository>();
    public InMemoryTemplateRepository Plantillas => (InMemoryTemplateRepository)Services.GetRequiredService<ITemplateRepository>();

    /// <summary>Servicios extra para una prueba (por ejemplo, un canal que captura las entregas).</summary>
    public Action<IServiceCollection>? ServiciosExtra { get; init; }

    /// <summary>Cliente con los encabezados que agregaría APIM; sin userId, sin identidad.</summary>
    public HttpClient CrearCliente(string? userId, string? email = null, string? role = null)
    {
        var client = CreateClient();
        if (userId is not null) client.DefaultRequestHeaders.Add(ApimHeadersAuthenticationHandler.UserIdHeader, userId);
        if (email is not null) client.DefaultRequestHeaders.Add(ApimHeadersAuthenticationHandler.UserEmailHeader, email);
        if (role is not null) client.DefaultRequestHeaders.Add(ApimHeadersAuthenticationHandler.UserRoleHeader, role);
        return client;
    }

    /// <summary>Cliente de un administrador, con id, correo y rol admin.</summary>
    public HttpClient CrearAdmin(string email = "admin@ejemplo.com") => CrearCliente("admin-1", email, "admin");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Azure:SignalR:ConnectionString", "");
        builder.UseSetting("Firebase:ServiceAccountJson", "");
        builder.UseSetting("Acs:ConnectionString", "");
        builder.UseSetting("Email:Sender", "");
        builder.UseSetting("Apim:CertificateThumbprint", "");

        builder.ConfigureTestServices(services =>
        {
            var consumidor = services.Single(d =>
                d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(NotificationEventsConsumer));
            services.Remove(consumidor);

            services.RemoveAll<INotificationRepository>();
            services.RemoveAll<IDirectoryRepository>();
            services.RemoveAll<IDeviceRepository>();
            services.RemoveAll<ITemplateRepository>();
            services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
            services.AddSingleton<IDirectoryRepository, InMemoryDirectoryRepository>();
            services.AddSingleton<IDeviceRepository, InMemoryDeviceRepository>();
            services.AddSingleton<ITemplateRepository, InMemoryTemplateRepository>();

            ServiciosExtra?.Invoke(services);
        });
    }
}
