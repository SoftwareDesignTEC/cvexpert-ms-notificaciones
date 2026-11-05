using System.Collections.Concurrent;
using Notificaciones.Api.Domain;
using Notificaciones.Api.Services;

namespace Notificaciones.Tests;

// Repositorios en memoria con la misma semántica que los de Mongo, para las pruebas de integración.

public class InMemoryNotificationRepository : INotificationRepository
{
    private readonly object _lock = new();
    private readonly List<Notification> _items = [];

    public Task EnsureIndexesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<InsertResult> InsertAsync(Notification notification, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_items.Any(n => n.EventId == notification.EventId)) return Task.FromResult(InsertResult.Duplicate);
            _items.Add(notification);
            return Task.FromResult(InsertResult.Inserted);
        }
    }

    public Task<Page<Notification>> ListAsync(string userId, bool? unread, int page, int pageSize, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var query = _items.Where(n => n.UserId == userId && (unread is null || (n.ReadAt is null) == unread)).ToList();
            var items = query
                .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id, StringComparer.Ordinal)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Task.FromResult(new Page<Notification>(items, query.Count));
        }
    }

    public Task<long> CountUnreadAsync(string userId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult((long)_items.Count(n => n.UserId == userId && n.ReadAt is null));
        }
    }

    public Task<Notification?> MarkReadAsync(string id, string userId, long readAt, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var notification = _items.FirstOrDefault(n => n.Id == id && n.UserId == userId);
            if (notification is not null) notification.ReadAt ??= readAt;
            return Task.FromResult(notification);
        }
    }

    public Task MarkAllReadAsync(string userId, long readAt, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            foreach (var notification in _items.Where(n => n.UserId == userId && n.ReadAt is null))
                notification.ReadAt = readAt;
            return Task.CompletedTask;
        }
    }
}

public class InMemoryDirectoryRepository : IDirectoryRepository
{
    private readonly ConcurrentDictionary<string, DirectoryEntry> _entries = new();

    public Task UpsertAsync(DirectoryEntry entry, CancellationToken cancellationToken)
    {
        _entries[entry.UserId] = entry;
        return Task.CompletedTask;
    }

    public Task<DirectoryEntry?> GetAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(_entries.GetValueOrDefault(userId));
}

public class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Device> _byToken = [];

    public Device? Find(string token)
    {
        lock (_lock) return _byToken.GetValueOrDefault(token);
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<DeviceRegistrationResult> RegisterAsync(
        string userId, string token, string platform, long now, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_byToken.TryGetValue(token, out var device))
            {
                device = new Device { Token = token, UserId = userId, Platform = platform, RegisteredAt = now };
                _byToken[token] = device;
                return Task.FromResult(new DeviceRegistrationResult(device, Created: true));
            }

            if (device.UserId != userId)
            {
                device.UserId = userId;
                device.RegisteredAt = now;
            }
            device.Platform = platform;
            return Task.FromResult(new DeviceRegistrationResult(device, Created: false));
        }
    }

    public Task<IReadOnlyList<string>> ListTokensAsync(string userId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<string>>(_byToken.Values.Where(d => d.UserId == userId).Select(d => d.Token).ToList());
        }
    }

    public Task DeleteAsync(string userId, string token, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_byToken.TryGetValue(token, out var device) && device.UserId == userId) _byToken.Remove(token);
            return Task.CompletedTask;
        }
    }
}
