using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Notificaciones.Api.Auth;
using Notificaciones.Api.Contracts;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Errors;
using Notificaciones.Api.Services;
using Notificaciones.Api.Services.Templates;

namespace Notificaciones.Api.Controllers;

/// <summary>Plantillas de título y cuerpo por tipo e idioma. Solo administradores (<c>X-User-Role: admin</c>).</summary>
[ApiController]
[Authorize(Policy = AdminPolicy.Name)]
[Route("notifications/templates")]
public class TemplatesController(ITemplateRepository templates, TemplateCache cache, TimeProvider timeProvider) : ControllerBase
{
    private const string TypeError = "Debe ser application.status_changed, cv.generated, job.new o course.recommended.";
    private const string LanguageError = "Debe ser \"es\" o \"en\".";

    /// <summary>Las 8 plantillas (4 tipos × 2 idiomas); las que nunca se editaron, con el texto por defecto.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TemplateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var saved = (await templates.ListAsync(cancellationToken)).ToDictionary(t => t.Id, StringComparer.Ordinal);
        var items = NotificationTypes.All
            .SelectMany(type => Languages.All.Select(language =>
                ToResponse(type, language, saved.GetValueOrDefault(NotificationTemplate.IdFor(type, language)))))
            .ToList();
        return Ok(items);
    }

    /// <summary>Una plantilla. Si nunca se editó, el texto por defecto con updatedAt = 0 y sin updatedBy.</summary>
    [HttpGet("{type}/{language}")]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        [AllowedValues(NotificationTypes.ApplicationStatusChanged, NotificationTypes.CvGenerated, NotificationTypes.JobNew,
            NotificationTypes.CourseRecommended, ErrorMessage = TypeError)] string type,
        [AllowedValues(Languages.Spanish, Languages.English, ErrorMessage = LanguageError)] string language,
        CancellationToken cancellationToken) =>
        Ok(ToResponse(type, language, await templates.GetAsync(type, language, cancellationToken)));

    /// <summary>
    /// Reemplaza title y body. Solo se aceptan los marcadores {{campo}} del data de ese tipo; updatedBy sale de X-User-Email.
    /// </summary>
    [HttpPut("{type}/{language}")]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Put(
        [AllowedValues(NotificationTypes.ApplicationStatusChanged, NotificationTypes.CvGenerated, NotificationTypes.JobNew,
            NotificationTypes.CourseRecommended, ErrorMessage = TypeError)] string type,
        [AllowedValues(Languages.Spanish, Languages.English, ErrorMessage = LanguageError)] string language,
        TemplateUpdate update,
        CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new ErrorResponse(
                StatusCodes.Status401Unauthorized, $"Falta el encabezado {ApimHeadersAuthenticationHandler.UserEmailHeader}."));
        }

        var errors = new Dictionary<string, string>();
        AddMarkerError(errors, "title", type, update.Title!);
        AddMarkerError(errors, "body", type, update.Body!);
        if (errors.Count > 0)
            return BadRequest(new ErrorResponse(StatusCodes.Status400BadRequest, "La solicitud tiene datos inválidos.", errors));

        var template = new NotificationTemplate
        {
            Id = NotificationTemplate.IdFor(type, language),
            Type = type,
            Language = language,
            Title = update.Title!,
            Body = update.Body!,
            UpdatedAt = timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            UpdatedBy = email,
        };
        await templates.UpsertAsync(template, cancellationToken);
        cache.Invalidate();

        return Ok(ToResponse(type, language, template));
    }

    private static void AddMarkerError(Dictionary<string, string> errors, string field, string type, string text)
    {
        var notAllowed = TemplatePlaceholders.FindNotAllowed(type, text);
        if (notAllowed.Count == 0) return;

        var allowed = string.Join(", ", TemplatePlaceholders.Allowed(type).Select(f => $"{{{{{f}}}}}"));
        errors[field] = notAllowed.Count == 1
            ? $"Marcador no permitido para {type}: {notAllowed[0]}. Se permiten: {allowed}."
            : $"Marcadores no permitidos para {type}: {string.Join(", ", notAllowed)}. Se permiten: {allowed}.";
    }

    private static TemplateResponse ToResponse(string type, string language, NotificationTemplate? saved)
    {
        if (saved is not null)
            return new TemplateResponse(type, language, saved.Title, saved.Body, saved.UpdatedAt, saved.UpdatedBy);

        var text = DefaultTemplates.Get(type, language);
        return new TemplateResponse(type, language, text.Title, text.Body, UpdatedAt: 0, UpdatedBy: null);
    }
}
