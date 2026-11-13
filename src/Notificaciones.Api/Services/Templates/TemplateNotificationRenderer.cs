using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services.Templates;

/// <summary>
/// Arma title y body con la plantilla guardada del tipo e idioma o, si no hay, con el texto por defecto.
/// Si no se pueden leer las plantillas, usa el texto por defecto para no frenar la entrega.
/// </summary>
public class TemplateNotificationRenderer(TemplateCache cache, ILogger<TemplateNotificationRenderer> logger) : INotificationRenderer
{
    public async ValueTask<RenderedText> RenderAsync(Notification notification, string language, CancellationToken cancellationToken)
    {
        if (!Languages.All.Contains(language)) language = Languages.Default;
        return Render(notification, language, await GetTextAsync(notification.Type, language, cancellationToken));
    }

    /// <summary>
    /// Carga las plantillas en la caché si venció. Si falla no hace nada: <see cref="RenderAsync"/> vuelve a intentarlo,
    /// lo registra y usa el texto por defecto.
    /// </summary>
    public async ValueTask PrepareAsync(CancellationToken cancellationToken)
    {
        try
        {
            await cache.GetAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>Rellena los marcadores de <paramref name="text"/> con el data de la notificación.</summary>
    public static RenderedText Render(Notification notification, string language, TemplateText text) => new(
        TemplatePlaceholders.Fill(text.Title, notification.Data, language),
        TemplatePlaceholders.Fill(text.Body, notification.Data, language));

    private async ValueTask<TemplateText> GetTextAsync(string type, string language, CancellationToken cancellationToken)
    {
        try
        {
            var saved = await cache.GetAsync(cancellationToken);
            if (saved.TryGetValue(NotificationTemplate.IdFor(type, language), out var template))
                return new TemplateText(template.Title, template.Body);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "No se pudieron leer las plantillas; {Type} ({Language}) usa el texto por defecto", type, language);
        }
        return DefaultTemplates.Get(type, language);
    }
}
