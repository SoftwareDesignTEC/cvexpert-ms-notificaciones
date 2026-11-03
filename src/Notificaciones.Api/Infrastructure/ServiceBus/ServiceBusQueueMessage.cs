using Azure.Messaging.ServiceBus;
using Notificaciones.Api.Services.Events;

namespace Notificaciones.Api.Infrastructure.ServiceBus;

/// <summary>Adapta un mensaje del <see cref="ServiceBusProcessor"/> a <see cref="IQueueMessage"/>.</summary>
public class ServiceBusQueueMessage(ProcessMessageEventArgs args) : IQueueMessage
{
    public string MessageId => args.Message.MessageId;
    public string? Subject => args.Message.Subject;
    public int DeliveryCount => args.Message.DeliveryCount;
    public BinaryData Body => args.Message.Body;

    public Task CompleteAsync(CancellationToken cancellationToken) =>
        args.CompleteMessageAsync(args.Message, cancellationToken);

    public Task DeadLetterAsync(string reason, string description, CancellationToken cancellationToken) =>
        args.DeadLetterMessageAsync(args.Message, reason, description, cancellationToken);

    public Task AbandonAsync(CancellationToken cancellationToken) =>
        args.AbandonMessageAsync(args.Message, cancellationToken: cancellationToken);
}
