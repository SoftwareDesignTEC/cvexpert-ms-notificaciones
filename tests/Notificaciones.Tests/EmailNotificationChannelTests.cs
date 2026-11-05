using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Notificaciones.Api.Services;
using Notificaciones.Api.Services.Channels;

namespace Notificaciones.Tests;

public class EmailNotificationChannelTests
{
    private readonly CorreoFalso _correo = new();
    private readonly EmailNotificationChannel _canal;

    public EmailNotificationChannelTests()
    {
        _canal = new EmailNotificationChannel(_correo, new DefaultEmailComposer(), NullLogger<EmailNotificationChannel>.Instance);
    }

    [Fact]
    public async Task SinDirectorio_NoEnviaCorreo()
    {
        await _canal.SendAsync(EntregasDePrueba.Entrega(destinatario: null), CancellationToken.None);

        Assert.Empty(_correo.Enviados);
    }

    [Theory]
    [InlineData("es", "Tu postulación cambió de estado")]
    [InlineData("en", "Your application was updated")]
    public async Task ConDirectorio_EnviaAlCorreoEnSuIdioma(string idioma, string asunto)
    {
        await _canal.SendAsync(EntregasDePrueba.Entrega(destinatario: EntregasDePrueba.Ana(idioma)), CancellationToken.None);

        var (para, correo) = Assert.Single(_correo.Enviados);
        Assert.Equal("ana@ejemplo.com", para);
        Assert.Equal(asunto, correo.Subject);
        Assert.Contains($"lang=\"{idioma}\"", correo.Html);
    }

    [Fact]
    public async Task JobNew_NoSeEnviaPorCorreo()
    {
        var entrega = EntregasDePrueba.Entrega(EntregasDePrueba.Notificacion("job.new"), EntregasDePrueba.Ana());

        await _canal.SendAsync(entrega, CancellationToken.None);

        Assert.Empty(_correo.Enviados);
    }

    [Theory]
    [InlineData("application.status_changed")]
    [InlineData("cv.generated")]
    [InlineData("course.recommended")]
    public void TiposConCorreo(string tipo)
    {
        Assert.Contains(tipo, EmailNotificationChannel.EmailTypes);
    }

    private sealed class CorreoFalso : IEmailSender
    {
        public List<(string Para, ComposedEmail Correo)> Enviados { get; } = [];

        public Task SendAsync(string to, ComposedEmail email, CancellationToken cancellationToken)
        {
            Enviados.Add((to, email));
            return Task.CompletedTask;
        }
    }
}

public class DefaultEmailComposerTests
{
    [Fact]
    public void AsuntoEsElTituloYElCuerpoLlevaTituloTextoYPie()
    {
        var entrega = EntregasDePrueba.Entrega(destinatario: EntregasDePrueba.Ana());

        var correo = new DefaultEmailComposer().Compose(entrega);

        Assert.Equal("Tu postulación cambió de estado", correo.Subject);
        Assert.Contains("Tu postulación cambió de estado", correo.Html);
        Assert.Contains("CVExpert", correo.Html);
        Assert.Equal(
            "Tu postulación cambió de estado\n\nTu postulación a Desarrollador .NET en Acme cambió a \"Entrevista\".\n\n--\nCVExpert",
            correo.PlainText);
    }

    [Fact]
    public void DatosDeOtrosServicios_SeEscapanEnElHtml()
    {
        var notificacion = EntregasDePrueba.Notificacion();
        notificacion.Data["jobTitle"] = new BsonString("<script>alert(1)</script>");
        var entrega = new NotificationDelivery(
            notificacion, EntregasDePrueba.Ana(), "es", new DefaultNotificationRenderer().Render(notificacion, "es"));

        var correo = new DefaultEmailComposer().Compose(entrega);

        Assert.DoesNotContain("<script>", correo.Html);
        Assert.Contains("&lt;script&gt;", correo.Html);
    }
}
