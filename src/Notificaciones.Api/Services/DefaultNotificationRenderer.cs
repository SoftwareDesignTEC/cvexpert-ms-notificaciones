using System.Globalization;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services;

/// <summary>
/// Textos fijos por tipo e idioma, con marcadores <c>{{campo}}</c> que se rellenan con el data del evento.
/// Se reemplaza por las plantillas editables (/notifications/templates) cuando existan.
/// </summary>
public partial class DefaultNotificationRenderer : INotificationRenderer
{
    private static readonly Dictionary<(string Type, string Language), (string Title, string Body)> Templates = new()
    {
        [("application.status_changed", Languages.Spanish)] =
            ("Tu postulación cambió de estado", "Tu postulación a {{jobTitle}} en {{company}} cambió a \"{{newStatus}}\"."),
        [("application.status_changed", Languages.English)] =
            ("Your application was updated", "Your application for {{jobTitle}} at {{company}} changed to \"{{newStatus}}\"."),
        [("cv.generated", Languages.Spanish)] =
            ("Tu CV está listo", "Ya puedes ver tu CV \"{{title}}\"."),
        [("cv.generated", Languages.English)] =
            ("Your CV is ready", "Your CV \"{{title}}\" is ready to view."),
        [("job.new", Languages.Spanish)] =
            ("Nueva oferta para ti", "{{title}} en {{company}} · {{location}} · {{modality}}. Compatibilidad: {{matchScore}} %."),
        [("job.new", Languages.English)] =
            ("A new job for you", "{{title}} at {{company}} · {{location}} · {{modality}}. Match: {{matchScore}}%."),
        [("course.recommended", Languages.Spanish)] =
            ("Curso recomendado", "{{title}} en {{platform}} ({{isPaid}})."),
        [("course.recommended", Languages.English)] =
            ("Recommended course", "{{title}} on {{platform}} ({{isPaid}})."),
    };

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

    public RenderedText Render(Notification notification, string language)
    {
        if (!Templates.ContainsKey((notification.Type, language))) language = Languages.Default;

        var (title, body) = Templates[(notification.Type, language)];
        return new RenderedText(Fill(title, notification.Data, language), Fill(body, notification.Data, language));
    }

    // Un marcador sin valor (opcional omitido o null) se reemplaza por texto vacío.
    private static string Fill(string template, BsonDocument data, string language) =>
        Placeholder().Replace(template, match =>
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
    private static partial Regex Placeholder();
}
