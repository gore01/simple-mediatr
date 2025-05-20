using BenchmarkDotNet.Attributes;

namespace simple_mediatr;

public class Benchmarks
{
    private Mediatr _mediatr = null!;
    private readonly Ping _request = new Ping { Message = "Hello World" };
    [GlobalSetup]
    public void GlobalSetup()
    {
        _mediatr = new Mediatr();
        _mediatr.Subscribe<Ping>(HandlePing);
    }

    private static async Task HandlePing(Ping ping) => await Task.CompletedTask;

    [Benchmark]
    public void SendingRequests()
    {
        _mediatr.Publish(_request);
    }
}

public class Ping
{
    public required string Message { get; set; }
}