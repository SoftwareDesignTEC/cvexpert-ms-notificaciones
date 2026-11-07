namespace Notificaciones.Api.Auth;

/// <summary>
/// Política de las rutas de administración: exige <c>X-User-Role: admin</c> (exacto, en minúsculas).
/// Sin <c>X-User-Id</c> responde 401; con identidad pero sin ese rol, 403.
/// </summary>
public static class AdminPolicy
{
    public const string Name = "Admin";
    public const string Role = "admin";
}
