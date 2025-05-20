using BenchmarkDotNet.Attributes;

public class Benchmarks
    {
        private Mediatr _mediatr;
        private readonly Ping _request = new Ping {Message = "Hello World"};
        [GlobalSetup]
        public void GlobalSetup()
        {
            _mediatr = new Mediatr();
            _mediatr.Subscribe<Ping>(HandlePing);
        }

    private static Task HandlePing(Ping ping) => Task.CompletedTask;

    [Benchmark]
        public void SendingRequests()
        {
            _mediatr.Publish(_request);
        }
    }

    public class Ping
    {
        public string Message { get; set; }
    }