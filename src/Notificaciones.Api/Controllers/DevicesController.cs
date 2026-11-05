using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notificaciones.Api.Auth;
using Notificaciones.Api.Contracts;
using Notificaciones.Api.Errors;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Controllers;

[ApiController]
[Authorize]
[Route("notifications/devices")]
public class DevicesController(IDeviceRepository devices, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Registra el token de FCM para el usuario: 201 si es nuevo; 200 si ya existía (de este usuario o de otro,
    /// y en ese caso pasa a este).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register(DeviceRegistration registration, CancellationToken cancellationToken)
    {
        var result = await devices.RegisterAsync(
            User.GetUserId(),
            registration.Token!,
            registration.Platform!,
            timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            cancellationToken);

        var response = DeviceResponse.From(result.Device);
        return result.Created ? StatusCode(StatusCodes.Status201Created, response) : Ok(response);
    }

    /// <summary>Deja de enviar push a ese token. Idempotente: 204 aunque no exista o sea de otro usuario (entonces no cambia nada).</summary>
    [HttpDelete("{token}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(string token, CancellationToken cancellationToken)
    {
        await devices.DeleteAsync(User.GetUserId(), token, cancellationToken);
        return NoContent();
    }
}
