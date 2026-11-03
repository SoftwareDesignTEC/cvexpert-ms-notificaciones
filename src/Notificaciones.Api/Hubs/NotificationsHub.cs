using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Notificaciones.Api.Hubs;

/// <summary>
/// Hub de SignalR para enviar notificaciones en tiempo real al cliente web.
/// [Authorize] hace que el negotiate sin <c>X-User-Id</c> responda 401. El usuario de la conexión es el NameIdentifier
/// (con Azure SignalR Service viaja dentro del token que entrega el negotiate), así que se le envía con <c>Clients.User(userId)</c>.
/// </summary>
[Authorize]
public class NotificationsHub : Hub
{
    public const string Path = "/notifications/hub";
}
