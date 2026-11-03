namespace Notificaciones.Api.Services.Events;

/// <summary>Mensaje recibido de la cola y las acciones para cerrarlo. Aísla al handler de los tipos de Service Bus.</summary>
public interface IQueueMessage
{
    string MessageId { get; }
    string? Subject { get; }
    int DeliveryCount { get; }
    BinaryData Body { get; }

    Task CompleteAsync(CancellationToken cancellationToken);

    /// <summary>Mueve el mensaje a la dead-letter sin reintentar.</summary>
    Task DeadLetterAsync(string reason, string description, CancellationToken cancellationToken);

    /// <summary>Libera el mensaje para que Service Bus lo vuelva a entregar.</summary>
    Task AbandonAsync(CancellationToken cancellationToken);
}
