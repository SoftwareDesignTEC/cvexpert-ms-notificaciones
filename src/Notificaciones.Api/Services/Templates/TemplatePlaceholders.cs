using System.Globalization;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using Notificaciones.Api.Services.Events;

namespace Notificaciones.Api.Services.Templates;

/// <summary>
/// Marcadores <c>{{campo}}</c> de las plantillas: cuáles se permiten por tipo (los campos de data del esquema del evento)
/// y cómo se rellenan con el data de una notificación.
/// </summary>
public static partial class TemplatePlaceholders
{
    // Valores de lista traducidos en vez de mostrarse crudos (pregunta abierta 6 del contrato).
    private static readonly Dictionary<(string Value, string Language), string> ListValues = new()
    {
        [("applied", Languages.Spanish)] = "Enviada",
        [("applied", Languages.English)] = "Applied",
        [("in-progress", Languages.Spanish)] = "En proceso",
        [("in-progress", Languages.English)] = "In progress",
        [("interview", Languages.Spanish)] = "Entrevista",
        [("interview", Languages.English)] = "Interview",
        [("rejected", Languages.Spanish)] = "Rechazada",
        [("rejected", Languages.English)] = "Rejected",
        [("remote", Languages.Spanish)] = "Remota",
        [("remote", Languages.English)] = "Remote",
        [("on-site", Languages.Spanish)] = "Presencial",
        [("on-site", Languages.English)] = "On-site",
        [("hybrid", Languages.Spanish)] = "Híbrida",
        [("hybrid", Languages.English)] = "Hybrid",
    };

    private static readonly HashSet<string> ListFields = ["newStatus", "previousStatus", "modality"];

    /// <summary>Marcadores permitidos para un tipo: los campos de su data en evento-notificacion.schema.json.</summary>
    public static IReadOnlyList<string> Allowed(string type) => NotificationEventValidator.DataFields[type];

    /// <summary>
    /// Marcadores de <paramref name="text"/> que no están permitidos para el tipo, tal como se escribieron y sin repetir.
    /// Cuenta cualquier <c>{{...}}</c>: <c>{{ title }}</c> o <c>{{Title}}</c> tampoco son válidos.
    /// </summary>
    public static IReadOnlyList<string> FindNotAllowed(string type, string text)
    {
        var allowed = Allowed(type);
        return AnyMarker().Matches(text)
            .Where(match => !allowed.Contains(match.Groups[1].Value, StringComparer.Ordinal))
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Rellena los marcadores con el data. Uno sin valor (opcional omitido o null) queda como texto vacío.</summary>
    public static string Fill(string template, BsonDocument data, string language) =>
        Marker().Replace(template, match =>
        {
            var field = match.Groups[1].Value;
            return Format(field, data.GetValue(field, BsonNull.Value), language);
        });

    private static string Format(string field, BsonValue value, string language) => value switch
    {
        BsonString text when ListFields.Contains(field) => ListValues.GetValueOrDefault((text.Value, language), text.Value),
        BsonString text => text.Value,
        BsonBoolean isPaid => (isPaid.Value, language) switch
        {
            (true, Languages.English) => "paid",
            (false, Languages.English) => "free",
            (true, _) => "de pago",
            (false, _) => "gratis",
        },
        _ when value.IsNumeric => value.ToDouble().ToString("0.##", CultureInfo.GetCultureInfo(language)),
        _ => "",
    };

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Marker();

    [GeneratedRegex(@"\{\{(.*?)\}\}", RegexOptions.Singleline)]
    private static partial Regex AnyMarker();
}
