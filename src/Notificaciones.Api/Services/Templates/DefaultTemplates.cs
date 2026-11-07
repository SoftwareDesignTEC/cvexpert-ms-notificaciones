namespace Notificaciones.Api.Services.Templates;

/// <summary>Tipos de evento del contrato, en el orden en que se listan las plantillas.</summary>
public static class NotificationTypes
{
    public const string ApplicationStatusChanged = "application.status_changed";
    public const string CvGenerated = "cv.generated";
    public const string JobNew = "job.new";
    public const string CourseRecommended = "course.recommended";

    public static readonly IReadOnlyList<string> All = [ApplicationStatusChanged, CvGenerated, JobNew, CourseRecommended];
}

/// <summary>Título y cuerpo de una plantilla, todavía con los marcadores <c>{{campo}}</c> sin rellenar.</summary>
public record TemplateText(string Title, string Body);

/// <summary>Texto por defecto de cada tipo e idioma: se usa mientras un administrador no guarde una plantilla.</summary>
public static class DefaultTemplates
{
    private static readonly Dictionary<(string Type, string Language), TemplateText> Texts = new()
    {
        [(NotificationTypes.ApplicationStatusChanged, Languages.Spanish)] =
            new("Tu postulación cambió de estado", "Tu postulación a {{jobTitle}} en {{company}} cambió a \"{{newStatus}}\"."),
        [(NotificationTypes.ApplicationStatusChanged, Languages.English)] =
            new("Your application was updated", "Your application for {{jobTitle}} at {{company}} changed to \"{{newStatus}}\"."),
        [(NotificationTypes.CvGenerated, Languages.Spanish)] =
            new("Tu CV está listo", "Ya puedes ver tu CV \"{{title}}\"."),
        [(NotificationTypes.CvGenerated, Languages.English)] =
            new("Your CV is ready", "Your CV \"{{title}}\" is ready to view."),
        [(NotificationTypes.JobNew, Languages.Spanish)] =
            new("Nueva oferta para ti", "{{title}} en {{company}} · {{location}} · {{modality}}. Compatibilidad: {{matchScore}} %."),
        [(NotificationTypes.JobNew, Languages.English)] =
            new("A new job for you", "{{title}} at {{company}} · {{location}} · {{modality}}. Match: {{matchScore}}%."),
        [(NotificationTypes.CourseRecommended, Languages.Spanish)] =
            new("Curso recomendado", "{{title}} en {{platform}} ({{isPaid}})."),
        [(NotificationTypes.CourseRecommended, Languages.English)] =
            new("Recommended course", "{{title}} on {{platform}} ({{isPaid}})."),
    };

    /// <summary>Texto por defecto. Un idioma no soportado usa <see cref="Languages.Default"/>.</summary>
    public static TemplateText Get(string type, string language) =>
        Texts.TryGetValue((type, language), out var text) ? text : Texts[(type, Languages.Default)];
}
