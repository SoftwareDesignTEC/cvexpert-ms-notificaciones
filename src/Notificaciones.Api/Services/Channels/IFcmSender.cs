namespace Notificaciones.Api.Services.Channels;

/// <summary>Envío por Firebase Cloud Messaging, aislado del SDK para poder probar el canal.</summary>
public interface IFcmSender
{
    /// <summary>Envía el mismo mensaje a cada token. Devuelve un resultado por token, en el mismo orden.</summary>
    Task<IReadOnlyList<FcmSendResult>> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken);
}

public enum FcmSendStatus
{
    Sent,

    /// <summary>FCM dice que el token ya no es válido (app desinstalada, token rotado): se borra.</summary>
    Unregistered,

    /// <summary>Cualquier otro error (SenderIdMismatch, InvalidArgument...): solo se registra.</summary>
    Failed,
}

public record FcmSendResult(string Token, FcmSendStatus Status, string? Error = null);
