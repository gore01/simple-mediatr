public class Mediatr {
    private readonly ConcurrentDictionary<Type, object> _subscriptions = new();

    public void Publish<T>(T notification) {
        if (_subscriptions.TryGetValue(typeof(T), out var subscription)) {
            ((Subscription<T>)subscription).Publish(notification);
        }
    }

    public IAsyncDisposable Subscribe<T>(Func<T, Task> handler, Action<T, Exception>? errorHandler = default, CancellationToken cancellationToken = default) {
        var subscription = 
            _subscriptions.AddOrUpdate(typeof(T), new Subscription<T>(), (_, v) => v);
        
        return ((Subscription<T>)subscription).Subscribe(handler, errorHandler, cancellationToken);
    }

    private class Subscription<TPayload> {
        private ConcurrentDictionary<Guid, Channel<TPayload>> _channels = new();

        public void Publish(TPayload payload) {
            foreach (var channel in _channels.Values) {
                channel.Writer.TryWrite(payload);
            }
        }
        
        public IAsyncDisposable Subscribe(Func<TPayload, Task> handler, Action<TPayload, Exception>? errorHandler = default, CancellationToken cancellationToken = default) {
            var channelId = Guid.NewGuid();
            var internalHandler = async () => {
                var channel = Channel.CreateUnbounded<TPayload>();
                _channels.AddOrUpdate(channelId, channel, (_, _) => channel);
                var reader = channel.Reader;
                try {
                    while (!cancellationToken.IsCancellationRequested) {
                        await foreach (var item in reader.ReadAllAsync(cancellationToken)) {
                            try {
                                await handler(item);
                            } catch (Exception ex) {
                                errorHandler?.Invoke(item, ex);
                            }
                        }
                    }
                } catch (TaskCanceledException) {
                    // ignore
                } catch {
                    if (!(reader.Completion.IsCanceled || reader.Completion.IsCompleted)) {
                        throw;
                    }
                }
            };

            var workTask = internalHandler();

            return new AsyncDisposable(async () => {
                if (_channels.Remove(channelId, out var channel)) {
                    channel.Writer.Complete();
                }
                
                try {
                    await workTask;
                } catch {}
            });
        
        }
    }

    private class AsyncDisposable : IAsyncDisposable
    {
        private readonly Func<ValueTask> _disposeHandler;

        public AsyncDisposable(Func<ValueTask> disposeHandler) { 
            _disposeHandler = disposeHandler;
        }

        public async ValueTask DisposeAsync() => await _disposeHandler();
    }
}