using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Notificaciones.Api.Auth;

namespace Notificaciones.Tests;

/// <summary>
/// API con Apim:CertificateThumbprint configurado. La configuración incluye las huellas del certificado vencido y del
/// que aún no es vigente, para que el rechazo se deba a la fecha y no a la huella. La del válido va en minúsculas y con
/// espacios, y hay entradas vacías, para probar que se normalizan.
/// </summary>
public sealed class ApimConfigurado : IDisposable
{
    private readonly NotificacionesApiFactory _base = new();

    public X509Certificate2 Valido { get; } = Crear(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    public X509Certificate2 Vencido { get; } = Crear(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1));
    public X509Certificate2 AunNoVigente { get; } = Crear(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(30));
    public X509Certificate2 DeOtro { get; } = Crear(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

    public WebApplicationFactory<Program> Factory { get; }

    public ApimConfigurado()
    {
        var conEspacios = string.Join(' ', Valido.Thumbprint.ToLowerInvariant().Chunk(2).Select(par => new string(par)));
        var huellas = $" , {Vencido.Thumbprint}, {conEspacios} ,, {AunNoVigente.Thumbprint} ";
        Factory = _base.WithWebHostBuilder(b => b.UseSetting("Apim:CertificateThumbprint", huellas));
    }

    public static X509Certificate2 Crear(DateTimeOffset desde, DateTimeOffset hasta)
    {
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest("CN=apim-prueba", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return solicitud.CreateSelfSigned(desde, hasta);
    }

    public void Dispose()
    {
        Factory.Dispose();
        _base.Dispose();
        Valido.Dispose();
        Vencido.Dispose();
        AunNoVigente.Dispose();
        DeOtro.Dispose();
    }
}

public class ApimCertificateMiddlewareTests : IClassFixture<ApimConfigurado>
{
    private const string RutaProtegida = "/notifications/unread-count";
    private readonly ApimConfigurado _apim;

    public ApimCertificateMiddlewareTests(ApimConfigurado apim)
    {
        _apim = apim;
    }

    private Task<HttpResponseMessage> Llamar(string ruta, string? encabezado)
    {
        var client = _apim.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApimHeadersAuthenticationHandler.UserIdHeader, "usuario-1");
        if (encabezado is not null) client.DefaultRequestHeaders.Add(ApimCertificateMiddleware.CertificateHeader, encabezado);
        return client.GetAsync(ruta);
    }

    // App Service manda el certificado como DER en base64.
    private static string Encabezado(X509Certificate2 certificado) => Convert.ToBase64String(certificado.RawData);

    private static async Task AssertRechazado(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(403, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Solo se aceptan llamadas a través de APIM.", body.RootElement.GetProperty("message").GetString());
        Assert.False(body.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task CertificadoValido_Pasa()
    {
        var response = await Llamar(RutaProtegida, Encabezado(_apim.Valido));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CertificadoDeOtro_Responde403()
    {
        await AssertRechazado(await Llamar(RutaProtegida, Encabezado(_apim.DeOtro)));
    }

    [Fact]
    public async Task CertificadoVencido_Responde403()
    {
        await AssertRechazado(await Llamar(RutaProtegida, Encabezado(_apim.Vencido)));
    }

    [Fact]
    public async Task CertificadoAunNoVigente_Responde403()
    {
        await AssertRechazado(await Llamar(RutaProtegida, Encabezado(_apim.AunNoVigente)));
    }

    [Fact]
    public async Task SinEncabezado_Responde403()
    {
        await AssertRechazado(await Llamar(RutaProtegida, encabezado: null));
    }

    [Theory]
    [InlineData("esto no es base64")]
    [InlineData("AAAA")]
    public async Task EncabezadoIlegible_Responde403(string encabezado)
    {
        await AssertRechazado(await Llamar(RutaProtegida, encabezado));
    }

    [Fact]
    public async Task SinCertificado_TampocoLlegaAlNegotiate()
    {
        var client = _apim.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApimHeadersAuthenticationHandler.UserIdHeader, "usuario-1");

        await AssertRechazado(await client.PostAsync("/notifications/hub/negotiate?negotiateVersion=1", null));
    }

    [Fact]
    public async Task Health_SiemprePasa()
    {
        var response = await _apim.Factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class ApimCertificateSinConfiguracionTests : IClassFixture<NotificacionesApiFactory>
{
    private readonly NotificacionesApiFactory _factory;

    public ApimCertificateSinConfiguracionTests(NotificacionesApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SinHuellas_DejaPasarSinCertificado()
    {
        var response = await _factory.CrearCliente("usuario-1").GetAsync("/notifications/unread-count");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("", new string[0])]
    [InlineData(" , ,, ", new string[0])]
    [InlineData("AB CD, , ef01", new[] { "ABCD", "EF01" })]
    public void ParseThumbprints_SeparaPorComaQuitaEspaciosEIgnoraVacias(string? configurado, string[] esperado)
    {
        var huellas = ApimCertificateMiddleware.ParseThumbprints(configurado);

        Assert.Equal(esperado.Length, huellas.Count);
        Assert.All(esperado, h => Assert.Contains(h, huellas));
    }
}
