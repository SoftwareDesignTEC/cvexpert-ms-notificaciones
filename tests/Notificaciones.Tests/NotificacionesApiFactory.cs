using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notificaciones.Api.Infrastructure.ServiceBus;

namespace Notificaciones.Tests;

/// <summary>
/// API en memoria sin el consumidor de la cola y con SignalR local: aunque haya user-secrets con cadenas reales,
/// las pruebas no se conectan a Service Bus, Mongo ni Azure SignalR Service.
/// </summary>
public class NotificacionesApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Azure:SignalR:ConnectionString", "");

        builder.ConfigureTestServices(services =>
        {
            var consumidor = services.Single(d =>
                d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(NotificationEventsConsumer));
            services.Remove(consumidor);
        });
    }
}
