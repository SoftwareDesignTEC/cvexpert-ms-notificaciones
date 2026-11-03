using MongoDB.Driver;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Infrastructure.Mongo;

/// <summary>Notificaciones en la colección <c>notifications</c> de MongoDB (Cosmos DB con API de MongoDB).</summary>
public class MongoNotificationRepository(IMongoDatabase database) : INotificationRepository
{
    public const string CollectionName = "notifications";

    private readonly IMongoCollection<Notification> _collection = database.GetCollection<Notification>(CollectionName);

    // En Cosmos DB un índice único solo se puede crear con la colección vacía: por eso se crea al arrancar.
    public Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<Notification>.IndexKeys;
        return _collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Notification>(
                keys.Ascending(n => n.EventId),
                new CreateIndexOptions { Name = "eventId_unique", Unique = true }),
            new CreateIndexModel<Notification>(
                keys.Ascending(n => n.UserId).Descending(n => n.CreatedAt),
                new CreateIndexOptions { Name = "userId_createdAt" }),
        ], cancellationToken);
    }

    public async Task<InsertResult> InsertAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(notification, cancellationToken: cancellationToken);
            return InsertResult.Inserted;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return InsertResult.Duplicate;
        }
    }
}
