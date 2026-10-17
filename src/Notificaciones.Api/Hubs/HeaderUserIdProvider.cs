using Microsoft.AspNetCore.SignalR;

namespace Notificaciones.Api.Hubs;

/// <summary>
/// Identifica al usuario de una conexión de SignalR con el encabezado <c>X-User-Id</c> que agrega APIM,
/// para poder enviarle mensajes con <c>Clients.User(userId)</c>.
/// </summary>
public class HeaderUserIdProvider : IUserIdProvider
{
    public const string UserIdHeader = "X-User-Id";

    public string? GetUserId(HubConnectionContext connection)
    {
        var userId = connection.GetHttpContext()?.Request.Headers[UserIdHeader].ToString();
        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }
}
