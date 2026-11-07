namespace Notificaciones.Api.Options;

/// <summary>Sección "Apim". Sin huellas, el backend acepta llamadas que no vienen de APIM (solo para desarrollo y pruebas).</summary>
public class ApimOptions
{
    public const string SectionName = "Apim";

    /// <summary>
    /// Huellas SHA-1 de los certificados de cliente que presenta APIM, separadas por coma (dos durante una rotación).
    /// No es secreto: va como configuración normal del App Service (<c>Apim__CertificateThumbprint</c>).
    /// </summary>
    public string CertificateThumbprint { get; set; } = string.Empty;
}
