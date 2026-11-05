using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services;

/// <summary>Tokens de FCM de los dispositivos de cada usuario.</summary>
public interface IDeviceRepository
{
    /// <summary>Crea los índices de la colección (token único; userId).</summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Asocia el token al usuario. Si no existía lo crea (<c>Created = true</c>); si existía, de este usuario o de otro,
    /// pasa a ser de este usuario. <c>registeredAt</c> solo cambia cuando el token cambia de usuario.
    /// </summary>
    Task<DeviceRegistrationResult> RegisterAsync(
        string userId, string token, string platform, long now, CancellationToken cancellationToken);

    /// <summary>Tokens de todos los dispositivos del usuario.</summary>
    Task<IReadOnlyList<string>> ListTokensAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Quita el token solo si es del usuario; si no existe o es de otro, no hace nada.</summary>
    Task DeleteAsync(string userId, string token, CancellationToken cancellationToken);
}

public record DeviceRegistrationResult(Device Device, bool Created);
