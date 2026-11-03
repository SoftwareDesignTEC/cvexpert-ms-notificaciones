using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services;

/// <summary>Arma el título y el cuerpo de una notificación en un idioma. No se guardan: se arman al enviar o consultar.</summary>
public interface INotificationRenderer
{
    RenderedText Render(Notification notification, string language);
}

public record RenderedText(string Title, string Body);

/// <summary>Idiomas del contrato (<c>es</c> | <c>en</c>).</summary>
public static class Languages
{
    public const string Spanish = "es";
    public const string English = "en";

    /// <summary>Idioma cuando el usuario no está en el directorio o pide uno no soportado.</summary>
    public const string Default = Spanish;
}
