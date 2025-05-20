using System.Collections.Concurrent;

namespace simple_mediatr;

public partial class Mediatr
{
    private readonly ConcurrentDictionary<Type, object> _subscriptions = new();

    public void Publish<T>(T notification)
    {
        if (_subscriptions.TryGetValue(typeof(T), out var subscription))
        {
            ((Subscription<T>)subscription).Publish(notification);
        }
    }

    public IAsyncDisposable Subscribe<T>(Func<T, Task> handler, Action<T, Exception>? errorHandler = default)
    {
        var subscription =
            _subscriptions.AddOrUpdate(typeof(T), new Subscription<T>(), (_, v) => v);

        return ((Subscription<T>)subscription).Subscribe(handler, errorHandler);
    }
}