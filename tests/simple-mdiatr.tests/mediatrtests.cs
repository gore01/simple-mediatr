using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using simple_mediatr;

namespace simple_mediatr.tests;

public class MediatrTests
{
    [Fact]
    public async Task Publish_ShouldNotifyAllSubscribers()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();
        var countdown = new CountdownEvent(2);

        // Act
        var subscription1 = mediatr.Subscribe<string>(async msg =>
        {
            notifications.Add($"Handler1: {msg}");
            countdown.Signal();
            await Task.CompletedTask;
        });

        var subscription2 = mediatr.Subscribe<string>(async msg =>
        {
            notifications.Add($"Handler2: {msg}");
            countdown.Signal();
            await Task.CompletedTask;
        });

        mediatr.Publish("Test Message");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Not all handlers were notified");
        Assert.Equal(2, notifications.Count);
        Assert.Contains("Handler1: Test Message", notifications);
        Assert.Contains("Handler2: Test Message", notifications);

        await subscription1.DisposeAsync();
        await subscription2.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldHandleErrors()
    {
        // Arrange
        var mediatr = new Mediatr();
        var errors = new ConcurrentBag<(string Message, Exception Error)>();
        var countdown = new CountdownEvent(1);

        // Act
        var subscription = mediatr.Subscribe<string>(
            async msg => throw new Exception("Test error"),
            (msg, ex) =>
            {
                errors.Add((msg, ex));
                countdown.Signal();
            }
        );

        mediatr.Publish("Error Test");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Error handler was not called");
        Assert.Single(errors);
        Assert.Equal("Error Test", errors.First().Message);
        Assert.Equal("Test error", errors.First().Error.Message);

        await subscription.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldRespectCancellation()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();
        var cts = new CancellationTokenSource();

        // Act
        var subscription = mediatr.Subscribe<string>(
            async msg =>
            {
                notifications.Add(msg);
                await Task.CompletedTask;
            },
            cancellationToken: cts.Token
        );

        mediatr.Publish("Before cancellation");
        cts.Cancel();
        mediatr.Publish("After cancellation");

        // Assert
        await Task.Delay(100); // Give some time for the cancellation to take effect
        Assert.Single(notifications);
        Assert.Equal("Before cancellation", notifications.First());

        await subscription.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldHandleMultipleMessageTypes()
    {
        // Arrange
        var mediatr = new Mediatr();
        var stringNotifications = new ConcurrentBag<string>();
        var intNotifications = new ConcurrentBag<int>();
        var countdown = new CountdownEvent(2);

        // Act
        var stringSubscription = mediatr.Subscribe<string>(async msg =>
        {
            stringNotifications.Add(msg);
            countdown.Signal();
            await Task.CompletedTask;
        });

        var intSubscription = mediatr.Subscribe<int>(async msg =>
        {
            intNotifications.Add(msg);
            countdown.Signal();
            await Task.CompletedTask;
        });

        mediatr.Publish("String message");
        mediatr.Publish(42);

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Not all handlers were notified");
        Assert.Single(stringNotifications);
        Assert.Single(intNotifications);
        Assert.Equal("String message", stringNotifications.First());
        Assert.Equal(42, intNotifications.First());

        await stringSubscription.DisposeAsync();
        await intSubscription.DisposeAsync();
    }
}