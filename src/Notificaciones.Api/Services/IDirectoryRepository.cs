using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services;

/// <summary>Directorio de usuarios: correo e idioma, que el cliente registra con PUT /notifications/directory/me.</summary>
public interface IDirectoryRepository
{
    /// <summary>Crea o reemplaza la entrada del usuario.</summary>
    Task UpsertAsync(DirectoryEntry entry, CancellationToken cancellationToken);

    Task<DirectoryEntry?> GetAsync(string userId, CancellationToken cancellationToken);
}

public static class DirectoryRepositoryExtensions
{
    /// <summary>Idioma del usuario según el directorio; <see cref="Languages.Default"/> si no está registrado.</summary>
    public static async Task<string> GetLanguageAsync(
        this IDirectoryRepository directory, string userId, CancellationToken cancellationToken) =>
        (await directory.GetAsync(userId, cancellationToken))?.Language ?? Languages.Default;
}
