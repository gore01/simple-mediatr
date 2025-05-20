using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using simple_mediatr;

class Program
{
    static void Main(string[] args)
    {
        var appbuilder = Host.CreateApplicationBuilder(args);
        appbuilder.Services.Configure<HostOptions>(options =>
        {
            options.ServicesStartConcurrently = true;
            options.ServicesStopConcurrently = true;
        });
        appbuilder.Services.AddSingleton<Mediatr>();
        appbuilder.Services.AddHostedService<BackgroudService>();

        var app = appbuilder.Build();
        Mediatr mediatr = app.Services.GetRequiredService<Mediatr>();

        mediatr.Subscribe<int>(v =>
        {
            Console.WriteLine(v);
            return Task.CompletedTask;
        });

        var disposable = mediatr.Subscribe<int>(v =>
        {
            Console.WriteLine(v);
            return Task.CompletedTask;
        });

        Task.Delay(5000).ContinueWith(async _ =>
        {
            await disposable.DisposeAsync();
        });

        app.Run();

    }
}

class BackgroudService : IHostedService
{
    private Mediatr _mediatr;

    public BackgroudService(Mediatr mediatr)
    {
        _mediatr = mediatr;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        int value = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100);
            _mediatr.Publish<int>(value++);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"{nameof(BackgroudService)} is stopping...");
        return Task.CompletedTask;
    }
}