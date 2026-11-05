using System.ComponentModel.DataAnnotations;

namespace Notificaciones.Api.Contracts;

/// <summary>Esquema <c>DirectoryUpdate</c>: cuerpo de PUT /notifications/directory/me.</summary>
public class DirectoryUpdate
{
    private const string LanguageError = "Debe ser \"es\" o \"en\".";

    [Required(ErrorMessage = LanguageError)]
    [AllowedValues("es", "en", ErrorMessage = LanguageError)]
    public string? Language { get; set; }
}

/// <summary>Esquema <c>DirectoryEntry</c>: la entrada tal como quedó guardada.</summary>
public record DirectoryEntryResponse(string UserId, string Email, string Language);
