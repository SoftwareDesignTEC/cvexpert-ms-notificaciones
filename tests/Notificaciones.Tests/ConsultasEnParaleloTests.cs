using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Tests;

/// <summary>
/// GET /notifications y PATCH /notifications/{id}/read piden la notificación, el idioma del directorio y las plantillas
/// al mismo tiempo: Cosmos está en otra región y cada consulta en serie suma un viaje de ida y vuelta.
/// Los repositorios de esta prueba solo responden cuando las tres lecturas ya empezaron; si el controller las hiciera
/// una tras otra, la primera esperaría para siempre y la llamada fallaría por tiempo.
/// </summary>
public class ConsultasEnParaleloTests
{
    private const string Usuario = "usuario-paralelo";

    private static NotificacionesApiFactory ApiConCompuerta(Compuerta compuerta) => new()
    {
        ServiciosExtra = servicios =>
        {
            servicios.AddSingleton(compuerta);
            servicios.AddSingleton<InMemoryNotificationRepository>();
            servicios.AddSingleton<InMemoryDirectoryRepository>();
            servicios.AddSingleton<InMemoryTemplateRepository>();
            servicios.AddSingleton<INotificationRepository>(sp =>
                new NotificacionesConCompuerta(sp.GetRequiredService<InMemoryNotificationRepository>(), compuerta));
            servicios.AddSingleton<IDirectoryRepository>(sp =>
                new DirectorioConCompuerta(sp.GetRequiredService<InMemoryDirectoryRepository>(), compuerta));
            servicios.AddSingleton<ITemplateRepository>(sp =>
                new PlantillasConCompuerta(sp.GetRequiredService<InMemoryTemplateRepository>(), compuerta));
        },
    };

    private static async Task<Notification> Sembrar(NotificacionesApiFactory api)
    {
        var notificacion = new Notification
        {
            EventId = Guid.NewGuid().ToString(),
            UserId = Usuario,
            Type = "cv.generated",
            Source = "cvs",
            Data = new BsonDocument { ["cvId"] = "cv-1", ["title"] = "CV para backend" },
            OccurredAt = 1760720400000,
            CreatedAt = 1760720400000,
        };
        await api.Services.GetRequiredService<InMemoryNotificationRepository>().InsertAsync(notificacion, CancellationToken.None);
        return notificacion;
    }

    [Fact]
    public async Task Lista_PidePaginaIdiomaYPlantillasEnParalelo()
    {
        var compuerta = new Compuerta(esperadas: 3);
        await using var api = ApiConCompuerta(compuerta);
        await Sembrar(api);

        var response = await api.CrearCliente(Usuario).GetAsync("/notifications?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Tu CV está listo", body.RootElement.GetProperty("items")[0].GetProperty("title").GetString());
        Assert.Equal(["directorio", "pagina", "plantillas"], compuerta.Llegadas.Order());
    }

    [Fact]
    public async Task MarcarLeida_PideIdiomaYPlantillasMientrasMarca()
    {
        var compuerta = new Compuerta(esperadas: 3);
        await using var api = ApiConCompuerta(compuerta);
        var notificacion = await Sembrar(api);

        var response = await api.CrearCliente(Usuario).PatchAsync($"/notifications/{notificacion.Id}/read", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["directorio", "marcar", "plantillas"], compuerta.Llegadas.Order());
    }

    [Fact]
    public async Task MarcarLeida_DeOtroUsuario_Sigue404()
    {
        var compuerta = new Compuerta(esperadas: 3);
        await using var api = ApiConCompuerta(compuerta);
        var notificacion = await Sembrar(api);

        var response = await api.CrearCliente("otro-usuario").PatchAsync($"/notifications/{notificacion.Id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Deja seguir a las lecturas solo cuando llegaron <paramref name="esperadas"/>; si no llegan, falla a los 5 s.</summary>
    public sealed class Compuerta(int esperadas)
    {
        private readonly object _lock = new();
        private readonly List<string> _llegadas = [];
        private readonly TaskCompletionSource _todas = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> Llegadas
        {
            get { lock (_lock) return _llegadas.ToList(); }
        }

        public Task EsperarAsync(string lectura)
        {
            lock (_lock)
            {
                _llegadas.Add(lectura);
                if (_llegadas.Count >= esperadas) _todas.TrySetResult();
            }
            return _todas.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class NotificacionesConCompuerta(InMemoryNotificationRepository inner, Compuerta compuerta) : INotificationRepository
    {
        public Task EnsureIndexesAsync(CancellationToken cancellationToken) => inner.EnsureIndexesAsync(cancellationToken);

        public Task<InsertResult> InsertAsync(Notification notification, CancellationToken cancellationToken) =>
            inner.InsertAsync(notification, cancellationToken);

        public async Task<Page<Notification>> ListAsync(string userId, bool? unread, int page, int pageSize, CancellationToken cancellationToken)
        {
            await compuerta.EsperarAsync("pagina");
            return await inner.ListAsync(userId, unread, page, pageSize, cancellationToken);
        }

        public Task<long> CountUnreadAsync(string userId, CancellationToken cancellationToken) =>
            inner.CountUnreadAsync(userId, cancellationToken);

        public async Task<Notification?> MarkReadAsync(string id, string userId, long readAt, CancellationToken cancellationToken)
        {
            await compuerta.EsperarAsync("marcar");
            return await inner.MarkReadAsync(id, userId, readAt, cancellationToken);
        }

        public Task MarkAllReadAsync(string userId, long readAt, CancellationToken cancellationToken) =>
            inner.MarkAllReadAsync(userId, readAt, cancellationToken);
    }

    private sealed class DirectorioConCompuerta(InMemoryDirectoryRepository inner, Compuerta compuerta) : IDirectoryRepository
    {
        public Task UpsertAsync(DirectoryEntry entry, CancellationToken cancellationToken) => inner.UpsertAsync(entry, cancellationToken);

        public async Task<DirectoryEntry?> GetAsync(string userId, CancellationToken cancellationToken)
        {
            await compuerta.EsperarAsync("directorio");
            return await inner.GetAsync(userId, cancellationToken);
        }
    }

    private sealed class PlantillasConCompuerta(InMemoryTemplateRepository inner, Compuerta compuerta) : ITemplateRepository
    {
        public async Task<IReadOnlyList<NotificationTemplate>> ListAsync(CancellationToken cancellationToken)
        {
            await compuerta.EsperarAsync("plantillas");
            return await inner.ListAsync(cancellationToken);
        }

        public Task<NotificationTemplate?> GetAsync(string type, string language, CancellationToken cancellationToken) =>
            inner.GetAsync(type, language, cancellationToken);

        public Task UpsertAsync(NotificationTemplate template, CancellationToken cancellationToken) =>
            inner.UpsertAsync(template, cancellationToken);
    }
}
