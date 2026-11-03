using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services;

/// <summary>No entrega nada todavía; se reemplaza cuando existan SignalR, FCM y correo.</summary>
public class NullNotificationDispatcher : INotificationDispatcher
{
    public Task DispatchAsync(Notification notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
