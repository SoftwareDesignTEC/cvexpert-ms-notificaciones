namespace Notificaciones.Api.Services.Channels;

/// <summary>
/// Push a Android por FCM: a todos los tokens del usuario, con notification { title, body } en su idioma y
/// data { notificationId, type }. Borra solo los tokens que FCM marca como Unregistered; los demás errores
/// (SenderIdMismatch, InvalidArgument) solo se registran, porque pueden deberse a la configuración y no al token.
/// </summary>
public class FcmNotificationChannel(
    IFcmSender sender,
    IDeviceRepository devices,
    ILogger<FcmNotificationChannel> logger) : INotificationChannel
{
    public string Name => "fcm";

    public async Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        var notification = delivery.Notification;
        var tokens = await devices.ListTokensAsync(notification.UserId, cancellationToken);
        if (tokens.Count == 0) return;

        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id,
            ["type"] = notification.Type,
        };
        var results = await sender.SendAsync(tokens, delivery.Text.Title, delivery.Text.Body, data, cancellationToken);

        foreach (var result in results)
        {
            switch (result.Status)
            {
                case FcmSendStatus.Unregistered:
                    await devices.DeleteAsync(notification.UserId, result.Token, cancellationToken);
                    logger.LogInformation(
                        "Token {Token} de {UserId} ya no es válido en FCM; se borró", Shorten(result.Token), notification.UserId);
                    break;
                case FcmSendStatus.Failed:
                    logger.LogWarning(
                        "FCM rechazó la notificación {NotificationId} para el token {Token} de {UserId}: {Error}",
                        notification.Id, Shorten(result.Token), notification.UserId, result.Error);
                    break;
            }
        }
    }

    // Los tokens dan acceso a enviar push a un dispositivo: en los logs solo va el inicio.
    private static string Shorten(string token) => token.Length <= 8 ? token : token[..8] + "…";
}
