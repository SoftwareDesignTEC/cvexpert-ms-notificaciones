using Microsoft.AspNetCore.SignalR;
using Notificaciones.Api.Contracts;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Hubs;

/// <summary>
/// Entrega en tiempo real: invoca <c>notification</c> en las conexiones del usuario con la misma forma que un item de
/// GET /notifications. Si el usuario no tiene la web abierta, no pasa nada; la notificación ya está guardada.
/// </summary>
public class SignalRNotificationDispatcher(IHubContext<NotificationsHub> hub, INotificationRenderer renderer) : INotificationDispatcher
{
    public const string MethodName = "notification";

    public Task DispatchAsync(Notification notification, CancellationToken cancellationToken)
    {
        // Mientras no exista el directorio de usuarios, todo sale en el idioma por defecto.
        var payload = NotificationResponse.From(notification, renderer.Render(notification, Languages.Default));
        return hub.Clients.User(notification.UserId).SendAsync(MethodName, payload, cancellationToken);
    }
}
