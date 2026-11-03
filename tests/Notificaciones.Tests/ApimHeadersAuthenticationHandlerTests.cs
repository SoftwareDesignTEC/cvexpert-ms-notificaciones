using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notificaciones.Api.Auth;

namespace Notificaciones.Tests;

public class ApimHeadersAuthenticationHandlerTests
{
    private static async Task<AuthenticateResult> Autenticar(Dictionary<string, string> encabezados)
    {
        var context = new DefaultHttpContext();
        foreach (var (nombre, valor) in encabezados) context.Request.Headers[nombre] = valor;

        var handler = new ApimHeadersAuthenticationHandler(new OpcionesFijas(), NullLoggerFactory.Instance, UrlEncoder.Default);
        await handler.InitializeAsync(
            new AuthenticationScheme(ApimHeadersAuthenticationHandler.SchemeName, null, typeof(ApimHeadersAuthenticationHandler)),
            context);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task SinUserId_NoAutentica()
    {
        var resultado = await Autenticar(new() { ["X-User-Email"] = "ana@ejemplo.com" });

        Assert.True(resultado.None);
    }

    [Fact]
    public async Task UserIdVacio_NoAutentica()
    {
        var resultado = await Autenticar(new() { ["X-User-Id"] = "  " });

        Assert.True(resultado.None);
    }

    [Fact]
    public async Task SoloUserId_CreaUsuarioSinEmailNiRol()
    {
        var resultado = await Autenticar(new() { ["X-User-Id"] = "usuario-1" });

        Assert.True(resultado.Succeeded);
        var usuario = resultado.Principal!;
        Assert.True(usuario.Identity!.IsAuthenticated);
        Assert.Equal(ApimHeadersAuthenticationHandler.SchemeName, usuario.Identity.AuthenticationType);
        Assert.Equal("usuario-1", usuario.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Null(usuario.FindFirst(ClaimTypes.Email));
        Assert.Null(usuario.FindFirst(ClaimTypes.Role));
    }

    [Fact]
    public async Task ConTodosLosEncabezados_IncluyeEmailYRol()
    {
        var resultado = await Autenticar(new()
        {
            ["X-User-Id"] = "usuario-1",
            ["X-User-Email"] = "ana@ejemplo.com",
            ["X-User-Role"] = "admin",
        });

        var usuario = resultado.Principal!;
        Assert.Equal("usuario-1", usuario.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("ana@ejemplo.com", usuario.FindFirstValue(ClaimTypes.Email));
        Assert.True(usuario.IsInRole("admin"));
    }

    private sealed class OpcionesFijas : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();
        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
