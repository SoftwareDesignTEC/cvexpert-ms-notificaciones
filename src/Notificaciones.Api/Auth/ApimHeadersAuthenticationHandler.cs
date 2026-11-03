using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Notificaciones.Api.Auth;

/// <summary>
/// Autentica con los encabezados que agrega APIM después de validar el token de Google.
/// Con <c>X-User-Id</c> crea un usuario (NameIdentifier, y Email y Role si vienen); sin él, la solicitud queda sin autenticar
/// y una ruta con [Authorize] responde 401. Este servicio no valida tokens: solo APIM lo llama.
/// </summary>
public class ApimHeadersAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApimHeaders";
    public const string UserIdHeader = "X-User-Id";
    public const string UserEmailHeader = "X-User-Email";
    public const string UserRoleHeader = "X-User-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers[UserIdHeader].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };

        var email = Request.Headers[UserEmailHeader].ToString();
        if (!string.IsNullOrWhiteSpace(email)) claims.Add(new Claim(ClaimTypes.Email, email));

        var role = Request.Headers[UserRoleHeader].ToString();
        if (!string.IsNullOrWhiteSpace(role)) claims.Add(new Claim(ClaimTypes.Role, role));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
