using System.Collections.Concurrent;
using System.Threading.Channels;

namespace simple_mediatr;

partial class Mediatr
{
    private class Subscription<TPayload>
    {
        private ConcurrentDictionary<Guid, Channel<TPayload>> _channels = new();

        public void Publish(TPayload payload)
        {
            foreach (var channel in _channels.Values)
            {
                channel.Writer.TryWrite(payload);
            }
        }

        public IAsyncDisposable Subscribe(Func<TPayload, Task> handler, Action<TPayload, Exception>? errorHandler = default, CancellationToken cancellationToken = default)
        {
            var channelId = Guid.NewGuid();
            var channel = Channel.CreateUnbounded<TPayload>();
            _channels.AddOrUpdate(channelId, channel, (_, _) => channel);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tokenToUse = cts.Token;
            tokenToUse.Register(() => channel.Writer.Complete());

            var internalHandler = async () =>
            {
                var reader = channel.Reader;
                try
                {
                    while (!tokenToUse.IsCancellationRequested)
                    {
                        await foreach (var item in reader.ReadAllAsync(tokenToUse))
                        {
                            try
                            {
                                await handler(item);
                            }
                            catch (Exception ex)
                            {
                                errorHandler?.Invoke(item, ex);
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // ignore
                }
                catch
                {
                    if (!(reader.Completion.IsCanceled || reader.Completion.IsCompleted))
                    {
                        throw;
                    }
                }
            };

            var workTask = internalHandler();

            return new AsyncDisposable(async () =>
            {
                cts.Cancel();
                _channels.TryRemove(channelId, out var _);

                try
                {
                    await workTask;
                }
                catch { }
            });
        }

        private class AsyncDisposable : IAsyncDisposable
        {
            private readonly Func<ValueTask> _disposeHandler;

            public AsyncDisposable(Func<ValueTask> disposeHandler)
            {
                _disposeHandler = disposeHandler;
            }

            public async ValueTask DisposeAsync() => await _disposeHandler();
        }
    }
}