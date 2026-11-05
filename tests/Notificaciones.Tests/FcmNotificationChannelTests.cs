using Microsoft.Extensions.Logging.Abstractions;
using Notificaciones.Api.Services.Channels;

namespace Notificaciones.Tests;

public class FcmNotificationChannelTests
{
    private readonly InMemoryDeviceRepository _dispositivos = new();
    private readonly FcmFalso _fcm = new();
    private readonly FcmNotificationChannel _canal;

    public FcmNotificationChannelTests()
    {
        _canal = new FcmNotificationChannel(_fcm, _dispositivos, NullLogger<FcmNotificationChannel>.Instance);
    }

    private Task Registrar(string usuario, string token) =>
        _dispositivos.RegisterAsync(usuario, token, "android", 1760720400000, CancellationToken.None);

    [Fact]
    public async Task EnviaATodosLosTokensDelUsuarioConTextosYData()
    {
        await Registrar("usuario-1", "token-a");
        await Registrar("usuario-1", "token-b");
        await Registrar("otro", "token-de-otro");

        await _canal.SendAsync(EntregasDePrueba.Entrega(destinatario: EntregasDePrueba.Ana("en")), CancellationToken.None);

        var envio = Assert.Single(_fcm.Envios);
        Assert.Equal(["token-a", "token-b"], envio.Tokens.Order());
        Assert.Equal("Your application was updated", envio.Title);
        Assert.Equal("Your application for Desarrollador .NET at Acme changed to \"Interview\".", envio.Body);
        Assert.Equal("6711a2f0c3e4b5a6d7e8f901", envio.Data["notificationId"]);
        Assert.Equal("application.status_changed", envio.Data["type"]);
    }

    [Fact]
    public async Task SinTokens_NoLlamaAFcm()
    {
        await _canal.SendAsync(EntregasDePrueba.Entrega(), CancellationToken.None);

        Assert.Empty(_fcm.Envios);
    }

    [Fact]
    public async Task BorraSoloLosTokensUnregistered()
    {
        await Registrar("usuario-1", "valido");
        await Registrar("usuario-1", "desinstalado");
        await Registrar("usuario-1", "otro-proyecto");
        await Registrar("usuario-1", "argumento-invalido");
        _fcm.Resultados = new()
        {
            ["valido"] = (FcmSendStatus.Sent, null),
            ["desinstalado"] = (FcmSendStatus.Unregistered, "Unregistered"),
            ["otro-proyecto"] = (FcmSendStatus.Failed, "SenderIdMismatch"),
            ["argumento-invalido"] = (FcmSendStatus.Failed, "InvalidArgument"),
        };

        await _canal.SendAsync(EntregasDePrueba.Entrega(), CancellationToken.None);

        Assert.Null(_dispositivos.Find("desinstalado"));
        Assert.NotNull(_dispositivos.Find("valido"));
        Assert.NotNull(_dispositivos.Find("otro-proyecto"));
        Assert.NotNull(_dispositivos.Find("argumento-invalido"));
    }

    [Fact]
    public async Task ErrorDeFcm_SePropagaParaQueElCompuestoLoRegistre()
    {
        await Registrar("usuario-1", "token-a");
        _fcm.Error = new InvalidOperationException("Credencial inválida");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _canal.SendAsync(EntregasDePrueba.Entrega(), CancellationToken.None));
        Assert.NotNull(_dispositivos.Find("token-a"));
    }

    private sealed record Envio(IReadOnlyList<string> Tokens, string Title, string Body, IReadOnlyDictionary<string, string> Data);

    private sealed class FcmFalso : IFcmSender
    {
        public List<Envio> Envios { get; } = [];
        public Dictionary<string, (FcmSendStatus Estado, string? Error)> Resultados { get; set; } = [];
        public Exception? Error { get; set; }

        public Task<IReadOnlyList<FcmSendResult>> SendAsync(
            IReadOnlyList<string> tokens, string title, string body, IReadOnlyDictionary<string, string> data,
            CancellationToken cancellationToken)
        {
            if (Error is not null) throw Error;
            Envios.Add(new Envio(tokens, title, body, data));
            IReadOnlyList<FcmSendResult> resultados = tokens
                .Select(t => Resultados.TryGetValue(t, out var r)
                    ? new FcmSendResult(t, r.Estado, r.Error)
                    : new FcmSendResult(t, FcmSendStatus.Sent))
                .ToList();
            return Task.FromResult(resultados);
        }
    }
}
