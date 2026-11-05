using Microsoft.AspNetCore.SignalR;
using Notificaciones.Api.Contracts;
using Notificaciones.Api.Services.Channels;

namespace Notificaciones.Api.Hubs;

/// <summary>
/// Entrega en tiempo real: invoca <c>notification</c> en las conexiones del usuario con la misma forma que un item de
/// GET /notifications. Si el usuario no tiene la web abierta, no pasa nada; la notificación ya está guardada.
/// </summary>
public class SignalRNotificationChannel(IHubContext<NotificationsHub> hub) : INotificationChannel
{
    public const string MethodName = "notification";

    public string Name => "signalr";

    public Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        var payload = NotificationResponse.From(delivery.Notification, delivery.Text);
        return hub.Clients.User(delivery.Notification.UserId).SendAsync(MethodName, payload, cancellationToken);
    }
}
