using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Infrastructure.Mongo;

namespace Notificaciones.Tests;

/// <summary>
/// Cosmos DB for MongoDB (RU) rechaza GET /notifications si el sort de la lista no tiene un índice compuesto con los
/// mismos campos y direcciones. Los índices de notifications los crea MongoIndexInitializer con
/// <see cref="MongoNotificationRepository.EnsureIndexesAsync"/>, a partir de <see cref="MongoNotificationRepository.Indexes"/>.
/// </summary>
public class MongoNotificationRepositoryIndexTests
{
    private static readonly RenderArgs<Notification> Args =
        new(BsonSerializer.LookupSerializer<Notification>(), BsonSerializer.SerializerRegistry);

    [Fact]
    public void OrdenDeLaLista_EsCreatedAtDescYIdDesc()
    {
        Assert.Equal("{ \"createdAt\" : -1, \"_id\" : -1 }", MongoNotificationRepository.ListSort.Render(Args).ToJson());
    }

    [Fact]
    public void OrdenDeLaLista_TieneUnIndiceConLasMismasClavesYDirecciones()
    {
        // Se compara el JSON para que también cuente el orden de los campos.
        var sort = MongoNotificationRepository.ListSort.Render(Args).ToJson();

        var indice = Assert.Single(MongoNotificationRepository.Indexes, i => i.Keys.Render(Args).ToJson() == sort);

        Assert.Equal(MongoNotificationRepository.ListSortIndexName, indice.Options.Name);
        Assert.Equal("createdAt_desc_id_desc", indice.Options.Name);
    }

    [Fact]
    public void IndicesAnteriores_SeConservan()
    {
        var indices = MongoNotificationRepository.Indexes.ToDictionary(i => i.Options.Name, i => i.Keys.Render(Args).ToJson());

        Assert.Equal("{ \"eventId\" : 1 }", indices["eventId_unique"]);
        Assert.Equal("{ \"userId\" : 1, \"createdAt\" : -1 }", indices["userId_createdAt"]);
        Assert.Equal("{ \"userId\" : 1, \"readAt\" : 1 }", indices["userId_readAt"]);
        Assert.True(MongoNotificationRepository.Indexes.Single(i => i.Options.Name == "eventId_unique").Options.Unique);
    }
}
