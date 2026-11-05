using MongoDB.Driver;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Api.Infrastructure.Mongo;

/// <summary>Directorio en la colección <c>directory</c>. El <c>_id</c> es el userId, así que no necesita más índices.</summary>
public class MongoDirectoryRepository(IMongoDatabase database) : IDirectoryRepository
{
    public const string CollectionName = "directory";

    private readonly IMongoCollection<DirectoryEntry> _collection = database.GetCollection<DirectoryEntry>(CollectionName);

    public Task UpsertAsync(DirectoryEntry entry, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(
            d => d.UserId == entry.UserId, entry, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public async Task<DirectoryEntry?> GetAsync(string userId, CancellationToken cancellationToken) =>
        await _collection.Find(d => d.UserId == userId).FirstOrDefaultAsync(cancellationToken);
}
