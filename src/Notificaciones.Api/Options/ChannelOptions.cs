namespace Notificaciones.Api.Options;

/// <summary>Sección "Firebase". Sin <see cref="ServiceAccountJson"/> el canal de push queda desactivado.</summary>
public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>JSON completo de la service account de Firebase (secreto: Key Vault <c>Firebase--ServiceAccountJson</c>).</summary>
    public string ServiceAccountJson { get; set; } = string.Empty;
}

/// <summary>Sección "Acs" (Azure Communication Services). Sin cadena de conexión el correo queda desactivado.</summary>
public class AcsOptions
{
    public const string SectionName = "Acs";

    /// <summary>Secreto: Key Vault <c>Acs--ConnectionString</c>.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>Sección "Email". Sin remitente el correo queda desactivado.</summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Remitente de un dominio verificado en ACS, por ejemplo <c>DoNotReply@&lt;dominio&gt;.azurecomm.net</c>. No es secreto.</summary>
    public string Sender { get; set; } = string.Empty;
}
