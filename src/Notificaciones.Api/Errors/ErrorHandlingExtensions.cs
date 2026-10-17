using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Notificaciones.Api.Errors;

/// <summary>Hace que todos los errores de la API respondan con <see cref="ErrorResponse"/> en lugar de ProblemDetails.</summary>
public static class ErrorHandlingExtensions
{
    /// <summary>Reemplaza la respuesta 400 automática de [ApiController] por <c>{ status, message, errors }</c>.</summary>
    public static IMvcBuilder AddErrorResponses(this IMvcBuilder builder) =>
        builder.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .ToDictionary(
                        entry => ToFieldName(entry.Key),
                        entry => entry.Value!.Errors[0].ErrorMessage);

                return new BadRequestObjectResult(
                    new ErrorResponse(StatusCodes.Status400BadRequest, "La solicitud tiene datos inválidos.", errors));
            };
        });

    /// <summary>
    /// Excepciones no controladas → 500, y respuestas de error sin cuerpo (404, 405, 415...) → <c>{ status, message }</c>.
    /// </summary>
    public static WebApplication UseErrorResponses(this WebApplication app)
    {
        // El middleware ya registra la excepción en el log; aquí solo se arma la respuesta.
        app.UseExceptionHandler(errorApp => errorApp.Run(context =>
            WriteAsync(context, StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado.")));

        app.UseStatusCodePages(context =>
        {
            var status = context.HttpContext.Response.StatusCode;
            return WriteAsync(context.HttpContext, status, ReasonPhrases.GetReasonPhrase(status));
        });

        return app;
    }

    private static Task WriteAsync(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ErrorResponse(status, message));
    }

    // "Datos.CorreoElectronico" → "datos.correoElectronico"; "$.email" (error de JSON) → "email".
    private static string ToFieldName(string key)
    {
        if (key.StartsWith("$.")) key = key[2..];
        return string.Join('.', key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}
