using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notificaciones.Api.Auth;
using Notificaciones.Api.Contracts;
using Notificaciones.Api.Errors;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Controllers;

/// <summary>Centro de notificaciones del usuario. title y body se arman en el idioma del directorio.</summary>
[ApiController]
[Authorize]
[Route("notifications")]
public class NotificationsController(
    INotificationRepository notifications,
    IDirectoryRepository directory,
    INotificationRenderer renderer,
    TimeProvider timeProvider) : ControllerBase
{
    public const int MaxPageSize = 100;

    /// <summary>Página de notificaciones, más recientes primero. unread: true solo no leídas, false solo leídas, sin valor todas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [Range(1, int.MaxValue, ErrorMessage = "Debe ser 1 o mayor.")] int page = 1,
        [Range(1, MaxPageSize, ErrorMessage = "Debe estar entre 1 y 100.")] int pageSize = 10,
        bool? unread = null,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        var result = await notifications.ListAsync(userId, unread, page, pageSize, cancellationToken);
        var language = await directory.GetLanguageAsync(userId, cancellationToken);

        var items = result.Items.Select(n => NotificationResponse.From(n, renderer.Render(n, language))).ToList();
        return Ok(new PagedResponse<NotificationResponse>(result.TotalRecords, page, pageSize, items));
    }

    /// <summary>Cantidad de notificaciones no leídas, para la insignia.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken) =>
        Ok(new UnreadCountResponse(await notifications.CountUnreadAsync(User.GetUserId(), cancellationToken)));

    /// <summary>Marca una notificación como leída. Idempotente: si ya lo estaba, conserva su readAt. 404 si no existe o es de otro.</summary>
    [HttpPatch("{id}/read")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(string id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var notification = await notifications.MarkReadAsync(
            id, userId, timeProvider.GetUtcNow().ToUnixTimeMilliseconds(), cancellationToken);
        if (notification is null)
            return NotFound(new ErrorResponse(StatusCodes.Status404NotFound, "Notificación no encontrada."));

        var language = await directory.GetLanguageAsync(userId, cancellationToken);
        return Ok(NotificationResponse.From(notification, renderer.Render(notification, language)));
    }

    /// <summary>Marca como leídas todas las notificaciones no leídas del usuario. Idempotente.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(User.GetUserId(), timeProvider.GetUtcNow().ToUnixTimeMilliseconds(), cancellationToken);
        return NoContent();
    }
}
