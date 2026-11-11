using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Notificaciones.Api.Errors;

namespace Notificaciones.Api.Auth;

/// <summary>
/// Acepta solo llamadas que traen el certificado de cliente de APIM. App Service pone el certificado del TLS en
/// <c>X-ARR-ClientCert</c> (DER en base64). Este chequeo solo es seguro con el App Service en Client certificate mode
/// = Required: el certificado es público, y sin ese modo cualquiera podría poner el encabezado a mano.
/// <c>/health</c> queda excluido (también debe estar excluido en el App Service).
/// </summary>
public class ApimCertificateMiddleware(
    RequestDelegate next,
    IReadOnlySet<string> thumbprints,
    TimeProvider timeProvider,
    ILogger<ApimCertificateMiddleware> logger)
{
    public const string CertificateHeader = "X-ARR-ClientCert";

    /// <summary>
    /// Pings internos del App Service, sin certificado: Always On (<c>GET /</c>, cada 5 minutos) y el de arranque
    /// (<c>GET /robots933456.txt</c>). Se rechazan igual, pero se registran en Debug para no llenar el log de advertencias.
    /// </summary>
    public static readonly IReadOnlySet<string> PlatformPingPaths =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/", "/robots933456.txt" };

    /// <summary>"AB CD, , ef01" → { "ABCD", "EF01" }: separa por coma, quita espacios e ignora entradas vacías.</summary>
    public static IReadOnlySet<string> ParseThumbprints(string? configured) =>
        (configured ?? "")
            .Split(',')
            .Select(entry => string.Concat(entry.Where(c => !char.IsWhiteSpace(c))))
            .Where(entry => entry.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        var rejection = Validate(context.Request.Headers[CertificateHeader].ToString(), out var thumbprint);
        if (rejection is null)
        {
            await next(context);
            return;
        }

        // Solo la huella (pública), nunca el certificado.
        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        logger.Log(
            PlatformPingPaths.Contains(path) ? LogLevel.Debug : LogLevel.Warning,
            "Llamada rechazada a {Method} {Path}: {Reason} (huella {Thumbprint})",
            context.Request.Method, context.Request.Path, rejection, thumbprint ?? "-");

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            new ErrorResponse(StatusCodes.Status403Forbidden, "Solo se aceptan llamadas a través de APIM."));
    }

    /// <summary>Devuelve el motivo del rechazo, o null si el certificado es de APIM y está vigente.</summary>
    private string? Validate(string header, out string? thumbprint)
    {
        thumbprint = null;
        if (string.IsNullOrWhiteSpace(header)) return $"falta el encabezado {CertificateHeader}";

        X509Certificate2 certificate;
        try
        {
            certificate = new X509Certificate2(Convert.FromBase64String(header));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return "el certificado no se puede leer";
        }

        using (certificate)
        {
            thumbprint = certificate.Thumbprint;
            if (!thumbprints.Contains(thumbprint)) return "la huella no es la de APIM";

            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
                return "el certificado está fuera de vigencia";
        }

        return null;
    }
}
