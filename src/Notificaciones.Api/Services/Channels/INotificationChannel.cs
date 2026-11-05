using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services.Channels;

/// <summary>Un canal de entrega (tiempo real, push, correo). Los llama <see cref="CompositeNotificationDispatcher"/>.</summary>
public interface INotificationChannel
{
    /// <summary>Nombre corto para los logs (<c>signalr</c>, <c>fcm</c>, <c>email</c>).</summary>
    string Name { get; }

    Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken);
}

/// <summary>
/// Lo que necesita cada canal, resuelto una sola vez por notificación: la entrada del directorio (null si el usuario
/// no está registrado), el idioma y el título y cuerpo ya armados en ese idioma.
/// </summary>
public record NotificationDelivery(Notification Notification, DirectoryEntry? Recipient, string Language, RenderedText Text);
