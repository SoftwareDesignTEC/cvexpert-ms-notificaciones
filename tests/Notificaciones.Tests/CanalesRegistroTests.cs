using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Notificaciones.Api.Hubs;
using Notificaciones.Api.Services;
using Notificaciones.Api.Services.Channels;

namespace Notificaciones.Tests;

public class CanalesRegistroTests
{
    [Fact]
    public void SinConfiguracion_SoloQuedaElCanalDeSignalR()
    {
        using var factory = new NotificacionesApiFactory();

        var canales = factory.Services.GetServices<INotificationChannel>().ToList();

        Assert.IsType<SignalRNotificationChannel>(Assert.Single(canales));
        Assert.IsType<CompositeNotificationDispatcher>(factory.Services.GetRequiredService<INotificationDispatcher>());
    }

    [Fact]
    public void ConConfiguracion_SeActivanPushYCorreo()
    {
        // Valores falsos: los SDK se reemplazan por dobles para no tocar Firebase ni ACS.
        using var factory = new NotificacionesApiFactory().WithWebHostBuilder(b =>
        {
            b.UseSetting("Firebase:ServiceAccountJson", "{\"type\":\"service_account\"}");
            b.UseSetting("Acs:ConnectionString", "endpoint=https://falso.communication.azure.com/;accesskey=Zm9v");
            b.UseSetting("Email:Sender", "DoNotReply@falso.azurecomm.net");
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFcmSender>();
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IFcmSender, SinEnvio>();
                services.AddSingleton<IEmailSender, SinEnvio>();
            });
        });

        var canales = factory.Services.GetServices<INotificationChannel>().Select(c => c.Name).ToList();

        Assert.Equal(["signalr", "fcm", "email"], canales);
    }

    [Theory]
    [InlineData("Acs:ConnectionString")]
    [InlineData("Email:Sender")]
    public void CorreoSinUnaDeSusClaves_QuedaDesactivado(string claveConValor)
    {
        using var factory = new NotificacionesApiFactory().WithWebHostBuilder(b => b.UseSetting(claveConValor, "valor"));

        var canales = factory.Services.GetServices<INotificationChannel>().Select(c => c.Name).ToList();

        Assert.DoesNotContain("email", canales);
    }

    private sealed class SinEnvio : IFcmSender, IEmailSender
    {
        public Task<IReadOnlyList<FcmSendResult>> SendAsync(
            IReadOnlyList<string> tokens, string title, string body, IReadOnlyDictionary<string, string> data,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FcmSendResult>>([]);

        public Task SendAsync(string to, ComposedEmail email, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
