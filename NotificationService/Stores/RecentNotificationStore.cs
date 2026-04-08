using System.Collections.Concurrent;
using NotificationService.Models;

namespace NotificationService.Stores;

public class RecentNotificationStore
{
    private const int MaxPerUser = 50;
    private readonly ConcurrentDictionary<Guid, LinkedList<NotificationEvent>> _store = new();

    public void Add(Guid userId, string eventType, string message)
    {
        var entry = new NotificationEvent(Guid.NewGuid(), eventType, message, DateTime.UtcNow, false);

        _store.AddOrUpdate(
            userId,
            _ =>
            {
                var list = new LinkedList<NotificationEvent>();
                list.AddFirst(entry);
                return list;
            },
            (_, list) =>
            {
                lock (list)
                {
                    list.AddFirst(entry);
                    while (list.Count > MaxPerUser)
                        list.RemoveLast();
                }
                return list;
            });
    }

    public IReadOnlyList<NotificationEvent> GetRecent(Guid userId, int count = 5)
    {
        if (!_store.TryGetValue(userId, out var list))
            return Array.Empty<NotificationEvent>();

        lock (list)
        {
            return list.Take(count).ToList();
        }
    }
}
