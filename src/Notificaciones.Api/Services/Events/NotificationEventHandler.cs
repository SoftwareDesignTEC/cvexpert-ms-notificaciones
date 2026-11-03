using MongoDB.Bson;
using Notificaciones.Api.Domain;

namespace Notificaciones.Api.Services.Events;

/// <summary>
/// Procesa un mensaje de la cola: valida → guarda → entrega → completa.
/// Inválido → dead-letter sin reintentar. Duplicado → completa. Error al guardar → abandona para que Service Bus reintente.
/// </summary>
public class NotificationEventHandler(
    INotificationRepository repository,
    INotificationDispatcher dispatcher,
    TimeProvider timeProvider,
    ILogger<NotificationEventHandler> logger)
{
    // Límite prudente para la descripción de la dead-letter (viaja en las propiedades del mensaje).
    private const int MaxDescriptionLength = 1000;

    /// <summary>
    /// Espera antes de abandonar tras un error transitorio. Sin ella, con la base caída Service Bus reentrega al
    /// instante y el mensaje agota MaxDeliveryCount en segundos. Debe ser menor que el lock de la cola.
    /// </summary>
    public TimeSpan AbandonDelay { get; init; } = TimeSpan.FromSeconds(5);

    public async Task HandleAsync(IQueueMessage message, CancellationToken cancellationToken)
    {
        var validation = NotificationEventValidator.Validate(message.Body);
        if (!validation.IsValid)
        {
            var description = validation.Description.Length > MaxDescriptionLength
                ? validation.Description[..MaxDescriptionLength] + "…"
                : validation.Description;

            logger.LogWarning(
                "Mensaje {MessageId} enviado a la dead-letter: {DeadLetterReason}: {DeadLetterDescription}",
                message.MessageId, validation.Reason, description);
            await message.DeadLetterAsync(validation.Reason, description, cancellationToken);
            return;
        }

        var notificationEvent = validation.Event;
        if (message.MessageId != notificationEvent.EventId || message.Subject != notificationEvent.Type)
        {
            logger.LogWarning(
                "El mensaje {MessageId} (subject {Subject}) no coincide con el evento {EventId} ({Type}) de {UserId}; se procesa igual",
                message.MessageId, message.Subject, notificationEvent.EventId, notificationEvent.Type, notificationEvent.UserId);
        }

        var notification = ToNotification(notificationEvent);

        InsertResult result;
        try
        {
            result = await repository.InsertAsync(notification, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "No se pudo guardar el evento {EventId} ({Type}) de {UserId} (entrega {DeliveryCount}); se abandona para reintentar",
                notificationEvent.EventId, notificationEvent.Type, notificationEvent.UserId, message.DeliveryCount);
            await Task.Delay(AbandonDelay, cancellationToken);
            await message.AbandonAsync(cancellationToken);
            return;
        }

        if (result == InsertResult.Duplicate)
        {
            logger.LogInformation(
                "Evento {EventId} ({Type}) de {UserId} duplicado: ya estaba guardado; se completa",
                notificationEvent.EventId, notificationEvent.Type, notificationEvent.UserId);
            await message.CompleteAsync(cancellationToken);
            return;
        }

        // Ya está guardado: un fallo al entregar no debe hacer que el mensaje se reintente.
        try
        {
            await dispatcher.DispatchAsync(notification, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex,
                "Evento {EventId} ({Type}) de {UserId} guardado, pero falló la entrega por los canales",
                notificationEvent.EventId, notificationEvent.Type, notificationEvent.UserId);
        }

        await message.CompleteAsync(cancellationToken);
        logger.LogInformation(
            "Evento {EventId} ({Type}) de {UserId} guardado como notificación {NotificationId}",
            notificationEvent.EventId, notificationEvent.Type, notificationEvent.UserId, notification.Id);
    }

    private Notification ToNotification(NotificationEvent notificationEvent) => new()
    {
        EventId = notificationEvent.EventId,
        UserId = notificationEvent.UserId,
        Type = notificationEvent.Type,
        Source = notificationEvent.Source,
        Data = BsonDocument.Parse(notificationEvent.Data.ToJsonString()),
        OccurredAt = notificationEvent.OccurredAt,
        CreatedAt = timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
        ReadAt = null,
    };
}
