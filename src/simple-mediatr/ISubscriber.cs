namespace simple_mediatr;

public interface ISubscriber
{
    IAsyncDisposable Subscribe<T>(Func<T, Task> handler, Action<T, Exception>? errorHandler = default);
}

