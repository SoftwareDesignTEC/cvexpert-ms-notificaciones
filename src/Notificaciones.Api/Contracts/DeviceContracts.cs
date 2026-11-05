using System.ComponentModel.DataAnnotations;
using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Contracts;

/// <summary>Esquema <c>DeviceRegistration</c>: cuerpo de POST /notifications/devices.</summary>
public class DeviceRegistration
{
    [Required(ErrorMessage = "Es obligatorio.")]
    public string? Token { get; set; }

    [Required(ErrorMessage = "Debe ser \"android\".")]
    [AllowedValues("android", ErrorMessage = "Debe ser \"android\".")]
    public string? Platform { get; set; }
}

/// <summary>Esquema <c>Device</c>.</summary>
public record DeviceResponse(string Token, string Platform, long RegisteredAt)
{
    public static DeviceResponse From(Device device) => new(device.Token, device.Platform, device.RegisteredAt);
}
