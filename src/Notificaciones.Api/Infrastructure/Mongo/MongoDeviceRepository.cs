using MongoDB.Driver;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Infrastructure.Mongo;

/// <summary>Dispositivos en la colección <c>devices</c>, con índice único en <c>token</c> e índice en <c>userId</c>.</summary>
public class MongoDeviceRepository(IMongoDatabase database) : IDeviceRepository
{
    public const string CollectionName = "devices";

    private readonly IMongoCollection<Device> _collection = database.GetCollection<Device>(CollectionName);

    // En Cosmos DB un índice único solo se puede crear con la colección vacía: por eso se crea al arrancar.
    public Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<Device>.IndexKeys;
        return _collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Device>(keys.Ascending(d => d.Token), new CreateIndexOptions { Name = "token_unique", Unique = true }),
            new CreateIndexModel<Device>(keys.Ascending(d => d.UserId), new CreateIndexOptions { Name = "userId" }),
        ], cancellationToken);
    }

    public async Task<DeviceRegistrationResult> RegisterAsync(
        string userId, string token, string platform, long now, CancellationToken cancellationToken)
    {
        var existing = await _collection.Find(d => d.Token == token).FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            var device = new Device { Token = token, UserId = userId, Platform = platform, RegisteredAt = now };
            try
            {
                await _collection.InsertOneAsync(device, cancellationToken: cancellationToken);
                return new DeviceRegistrationResult(device, Created: true);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                // Otra solicitud lo registró al mismo tiempo: se trata como uno que ya existía.
                existing = await _collection.Find(d => d.Token == token).FirstAsync(cancellationToken);
            }
        }

        if (existing.UserId != userId)
        {
            existing.UserId = userId;
            existing.RegisteredAt = now;
        }
        existing.Platform = platform;

        await _collection.ReplaceOneAsync(d => d.Id == existing.Id, existing, cancellationToken: cancellationToken);
        return new DeviceRegistrationResult(existing, Created: false);
    }

    public async Task<IReadOnlyList<string>> ListTokensAsync(string userId, CancellationToken cancellationToken) =>
        await _collection.Find(d => d.UserId == userId).Project(d => d.Token).ToListAsync(cancellationToken);

    public Task DeleteAsync(string userId, string token, CancellationToken cancellationToken) =>
        _collection.DeleteOneAsync(d => d.Token == token && d.UserId == userId, cancellationToken);
}
