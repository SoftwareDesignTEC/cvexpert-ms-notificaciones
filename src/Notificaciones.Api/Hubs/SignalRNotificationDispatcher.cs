using Microsoft.AspNetCore.SignalR;
using Notificaciones.Api.Contracts;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Hubs;

/// <summary>
/// Entrega en tiempo real: invoca <c>notification</c> en las conexiones del usuario con la misma forma que un item de
/// GET /notifications, en el idioma del directorio. Si el usuario no tiene la web abierta, no pasa nada; la notificación ya está guardada.
/// </summary>
public class SignalRNotificationDispatcher(
    IHubContext<NotificationsHub> hub,
    IDirectoryRepository directory,
    INotificationRenderer renderer) : INotificationDispatcher
{
    public const string MethodName = "notification";

    public async Task DispatchAsync(Notification notification, CancellationToken cancellationToken)
    {
        var language = await directory.GetLanguageAsync(notification.UserId, cancellationToken);
        var payload = NotificationResponse.From(notification, renderer.Render(notification, language));
        await hub.Clients.User(notification.UserId).SendAsync(MethodName, payload, cancellationToken);
    }
}
