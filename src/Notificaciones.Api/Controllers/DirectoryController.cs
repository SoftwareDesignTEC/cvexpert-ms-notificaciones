using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notificaciones.Api.Auth;
using Notificaciones.Api.Contracts;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Errors;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Controllers;

[ApiController]
[Authorize]
[Route("notifications/directory")]
public class DirectoryController(IDirectoryRepository directory, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Registra o actualiza el idioma del usuario. El correo sale de <c>X-User-Email</c>, no del cuerpo.</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(DirectoryEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PutMe(DirectoryUpdate update, CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new ErrorResponse(
                StatusCodes.Status401Unauthorized, $"Falta el encabezado {ApimHeadersAuthenticationHandler.UserEmailHeader}."));
        }

        var entry = new DirectoryEntry
        {
            UserId = User.GetUserId(),
            Email = email,
            Language = update.Language!,
            UpdatedAt = timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
        };
        await directory.UpsertAsync(entry, cancellationToken);

        return Ok(new DirectoryEntryResponse(entry.UserId, entry.Email, entry.Language));
    }
}
