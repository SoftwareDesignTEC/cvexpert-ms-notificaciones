using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services;

/// <summary>Entrega una notificación ya guardada por los canales (tiempo real, push y correo).</summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(Notification notification, CancellationToken cancellationToken);
}
