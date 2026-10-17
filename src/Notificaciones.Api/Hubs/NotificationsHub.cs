using Microsoft.AspNetCore.SignalR;

namespace Notificaciones.Api.Hubs;

/// <summary>Hub de SignalR para enviar notificaciones en tiempo real al cliente web.</summary>
public class NotificationsHub : Hub
{
    /// <summary>Rechaza las conexiones que no traen <c>X-User-Id</c>: sin usuario no hay a quién entregarle notificaciones.</summary>
    public override async Task OnConnectedAsync()
    {
        if (Context.UserIdentifier is null)
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }
}
