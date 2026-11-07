using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Notificaciones.Api.Services.Events;

/// <summary>
/// Valida el cuerpo de un mensaje según docs/contratos/evento-notificacion.schema.json.
/// El sobre es estricto; dentro de data los campos extra se ignoran (no se rechazan ni se guardan).
/// Cualquier cambio al esquema debe reflejarse aquí y en sus pruebas.
/// </summary>
public static partial class NotificationEventValidator
{
    /// <summary>Devuelve el detalle de un valor inválido, o null si es válido.</summary>
    private delegate string? Rule(JsonElement value);

    private record Field(string Name, bool Required, Rule Rule);

    private record EventSpec(string Source, Field[] Fields);

    private static readonly string[] ApplicationStatuses = ["applied", "in-progress", "interview", "rejected"];
    private static readonly string[] Modalities = ["remote", "on-site", "hybrid"];
    private static readonly string[] Sources = ["ofertas-postulaciones", "cvs", "brecha-cursos"];
    private static readonly HashSet<string> EnvelopeFields = ["schemaVersion", "eventId", "type", "userId", "occurredAt", "source", "data"];

    // Por tipo: único publicador permitido y campos de data.
    private static readonly Dictionary<string, EventSpec> Specs = new()
    {
        ["application.status_changed"] = new("ofertas-postulaciones",
        [
            new("applicationId", true, IdString),
            new("jobOfferId", true, IdString),
            new("jobTitle", true, NonEmptyString),
            new("company", true, NonEmptyString),
            new("previousStatus", false, OneOf(ApplicationStatuses)),
            new("newStatus", true, OneOf(ApplicationStatuses)),
        ]),
        ["cv.generated"] = new("cvs",
        [
            new("cvId", true, IdString),
            new("title", true, NonEmptyString),
            new("templateId", false, AnyString),
        ]),
        ["job.new"] = new("ofertas-postulaciones",
        [
            new("jobOfferId", true, IdString),
            new("title", true, NonEmptyString),
            new("company", true, NonEmptyString),
            new("location", true, NonEmptyString),
            new("modality", true, OneOf(Modalities)),
            new("matchScore", true, Score),
        ]),
        ["course.recommended"] = new("brecha-cursos",
        [
            new("title", true, NonEmptyString),
            new("platform", true, NonEmptyString),
            new("url", true, HttpUrl),
            new("isPaid", true, Bool),
            new("duration", false, AnyString),
            new("skill", false, AnyString),
        ]),
    };

    private static readonly Rule KnownType = OneOf([.. Specs.Keys]);
    private static readonly Rule KnownSource = OneOf(Sources);

    /// <summary>Campos de data por tipo, en el orden del esquema. Son también los marcadores permitidos en las plantillas.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DataFields =
        Specs.ToDictionary(spec => spec.Key, spec => (IReadOnlyList<string>)spec.Value.Fields.Select(f => f.Name).ToArray());

    public static EventValidationResult Validate(BinaryData body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body.ToMemory());
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return EventValidationResult.Invalid(DeadLetterReasons.InvalidJson, $"El cuerpo no es JSON válido: {ex.Message}");
        }

        using (document)
        {
            return Validate(document.RootElement);
        }
    }

    private static EventValidationResult Validate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return EventValidationResult.Invalid(DeadLetterReasons.SchemaValidationFailed, "El cuerpo debe ser un objeto JSON.");

        var errors = new List<string>();

        foreach (var property in root.EnumerateObject())
        {
            if (!EnvelopeFields.Contains(property.Name))
                errors.Add($"{property.Name}: campo no permitido fuera de data");
        }

        TryRead(root, "", "schemaVersion", SchemaVersion, errors, out _);
        TryRead(root, "", "eventId", Uuid, errors, out var eventId);
        var hasType = TryRead(root, "", "type", KnownType, errors, out var type);
        TryRead(root, "", "userId", NonEmptyString, errors, out var userId);
        TryRead(root, "", "occurredAt", EpochMillis, errors, out var occurredAt);
        var hasSource = TryRead(root, "", "source", KnownSource, errors, out var source);
        var hasData = TryRead(root, "", "data", JsonObjectRule, errors, out var dataElement);

        var data = new JsonObject();
        if (hasType)
        {
            var spec = Specs[type.GetString()!];

            if (hasSource && source.GetString() != spec.Source)
                errors.Add($"source: \"{source.GetString()}\" no corresponde al type \"{type.GetString()}\" (debe ser \"{spec.Source}\")");

            if (hasData)
                data = ReadData(dataElement, spec, errors);
        }

        if (errors.Count > 0)
            return EventValidationResult.Invalid(DeadLetterReasons.SchemaValidationFailed, string.Join("; ", errors));

        // Sin errores, todos los campos del sobre se leyeron y son válidos.
        return EventValidationResult.Valid(new NotificationEvent(
            eventId.GetString()!,
            type.GetString()!,
            userId.GetString()!,
            occurredAt.GetInt64(),
            source.GetString()!,
            data));
    }

    // Copia solo los campos conocidos del tipo; los opcionales aceptan null.
    private static JsonObject ReadData(JsonElement element, EventSpec spec, List<string> errors)
    {
        var data = new JsonObject();
        foreach (var field in spec.Fields)
        {
            if (!element.TryGetProperty(field.Name, out var value))
            {
                if (field.Required) errors.Add($"data.{field.Name}: es obligatorio");
                continue;
            }

            if (!field.Required && value.ValueKind == JsonValueKind.Null)
            {
                data[field.Name] = null;
                continue;
            }

            var error = field.Rule(value);
            if (error is not null)
            {
                errors.Add($"data.{field.Name}: {error}");
                continue;
            }

            data[field.Name] = JsonNode.Parse(value.GetRawText());
        }

        return data;
    }

    private static bool TryRead(JsonElement parent, string prefix, string name, Rule rule, List<string> errors, out JsonElement value)
    {
        if (!parent.TryGetProperty(name, out value))
        {
            errors.Add($"{prefix}{name}: es obligatorio");
            return false;
        }

        var error = rule(value);
        if (error is null) return true;

        errors.Add($"{prefix}{name}: {error}");
        return false;
    }

    private static string? SchemaVersion(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var version) && version == 1
            ? null
            : "debe ser 1";

    private static string? Uuid(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && UuidV4().IsMatch(value.GetString()!)
            ? null
            : "debe ser un UUID v4";

    private static string? EpochMillis(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var millis) && millis >= 0
            ? null
            : "debe ser un entero: epoch en milisegundos UTC";

    private static string? JsonObjectRule(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object ? null : "debe ser un objeto";

    private static string? NonEmptyString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString()!.Length > 0 ? null : "debe ser texto no vacío";

    private static string? IdString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString()!.Length > 0
            ? null
            : "debe ser texto no vacío (los ids van como texto, aunque sean numéricos)";

    private static string? AnyString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? null : "debe ser texto o null";

    private static string? Bool(JsonElement value) =>
        value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : "debe ser true o false";

    private static string? Score(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.GetDouble() is >= 0 and <= 100
            ? null
            : "debe ser un número de 0 a 100";

    private static string? HttpUrl(JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        var valid = (text.StartsWith("http://", StringComparison.Ordinal) || text.StartsWith("https://", StringComparison.Ordinal))
            && Uri.TryCreate(text, UriKind.Absolute, out _);
        return valid ? null : "debe ser un enlace absoluto http:// o https://";
    }

    private static Rule OneOf(string[] allowed) => value =>
        value.ValueKind != JsonValueKind.String ? $"debe ser uno de: {string.Join(", ", allowed)}"
        : allowed.Contains(value.GetString()) ? null
        : $"\"{value.GetString()}\" no es un valor permitido";

    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-4[0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$")]
    private static partial Regex UuidV4();
}
