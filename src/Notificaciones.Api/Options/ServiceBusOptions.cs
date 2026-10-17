namespace Notificaciones.Api.Options;

/// <summary>Sección "ServiceBus" de la configuración.</summary>
public class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
}
