using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services.Channels;

/// <summary>
/// Entrega una notificación ya guardada por todos los canales activos, en paralelo. El fallo de un canal se registra
/// y no afecta a los demás ni al mensaje de la cola.
/// </summary>
public class CompositeNotificationDispatcher(
    IEnumerable<INotificationChannel> channels,
    IDirectoryRepository directory,
    INotificationRenderer renderer,
    ILogger<CompositeNotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAsync(Notification notification, CancellationToken cancellationToken)
    {
        var recipient = await GetRecipientAsync(notification, cancellationToken);
        var language = recipient?.Language ?? Languages.Default;
        var text = await renderer.RenderAsync(notification, language, cancellationToken);
        var delivery = new NotificationDelivery(notification, recipient, language, text);

        await Task.WhenAll(channels.Select(channel => SendAsync(channel, delivery, cancellationToken)));
    }

    // Sin directorio no hay correo, pero tiempo real y push siguen saliendo en el idioma por defecto.
    private async Task<DirectoryEntry?> GetRecipientAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            return await directory.GetAsync(notification.UserId, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "No se pudo leer el directorio de {UserId}; la notificación {NotificationId} sale en {Language} y sin correo",
                notification.UserId, notification.Id, Languages.Default);
            return null;
        }
    }

    private async Task SendAsync(INotificationChannel channel, NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        var notification = delivery.Notification;
        try
        {
            await channel.SendAsync(delivery, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex,
                "Falló el canal {Channel} para la notificación {NotificationId} ({Type}) de {UserId}",
                channel.Name, notification.Id, notification.Type, notification.UserId);
        }
    }
}
