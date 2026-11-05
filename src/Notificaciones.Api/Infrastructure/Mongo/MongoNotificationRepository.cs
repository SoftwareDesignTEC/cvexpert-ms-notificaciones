using MongoDB.Bson;
using MongoDB.Driver;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Infrastructure.Mongo;

/// <summary>Notificaciones en la colección <c>notifications</c> de MongoDB (Cosmos DB con API de MongoDB).</summary>
public class MongoNotificationRepository(IMongoDatabase database) : INotificationRepository
{
    public const string CollectionName = "notifications";

    private static readonly FilterDefinitionBuilder<Notification> Filter = Builders<Notification>.Filter;

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
            new CreateIndexModel<Notification>(
                keys.Ascending(n => n.UserId).Ascending(n => n.ReadAt),
                new CreateIndexOptions { Name = "userId_readAt" }),
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

    public async Task<Page<Notification>> ListAsync(
        string userId, bool? unread, int page, int pageSize, CancellationToken cancellationToken)
    {
        var filter = Filter.Eq(n => n.UserId, userId);
        if (unread is true) filter &= Filter.Eq(n => n.ReadAt, null);
        if (unread is false) filter &= Filter.Ne(n => n.ReadAt, null);

        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        // Una página más allá de int.MaxValue elementos no puede tener resultados.
        var skip = (long)(page - 1) * pageSize;
        if (skip >= total || skip > int.MaxValue) return new Page<Notification>([], total);

        var items = await _collection.Find(filter)
            .SortByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((int)skip)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new Page<Notification>(items, total);
    }

    public Task<long> CountUnreadAsync(string userId, CancellationToken cancellationToken) =>
        _collection.CountDocumentsAsync(
            Filter.Eq(n => n.UserId, userId) & Filter.Eq(n => n.ReadAt, null), cancellationToken: cancellationToken);

    public async Task<Notification?> MarkReadAsync(string id, string userId, long readAt, CancellationToken cancellationToken)
    {
        // Un id con otro formato no puede existir; se trata igual que uno inexistente (404).
        if (!ObjectId.TryParse(id, out _)) return null;

        var mine = Filter.Eq(n => n.Id, id) & Filter.Eq(n => n.UserId, userId);

        // Solo se escribe si no estaba leída, para conservar el readAt original.
        await _collection.UpdateOneAsync(
            mine & Filter.Eq(n => n.ReadAt, null),
            Builders<Notification>.Update.Set(n => n.ReadAt, readAt),
            cancellationToken: cancellationToken);

        return await _collection.Find(mine).FirstOrDefaultAsync(cancellationToken);
    }

    public Task MarkAllReadAsync(string userId, long readAt, CancellationToken cancellationToken) =>
        _collection.UpdateManyAsync(
            Filter.Eq(n => n.UserId, userId) & Filter.Eq(n => n.ReadAt, null),
            Builders<Notification>.Update.Set(n => n.ReadAt, readAt),
            cancellationToken: cancellationToken);
}
