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
        var subscription1 = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add($"Handler1: {msg}");
            countdown.Signal();
            return Task.CompletedTask;
        });

        var subscription2 = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add($"Handler2: {msg}");
            countdown.Signal();
            return Task.CompletedTask;
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
            msg => throw new Exception("Test error"),
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

        // Act
        var subscription = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add(msg);
            return Task.CompletedTask;
        });

        mediatr.Publish("Before cancellation");
        await subscription.DisposeAsync();
        mediatr.Publish("After cancellation");

        // Assert
        Assert.Single(notifications);
        Assert.Equal("Before cancellation", notifications.First());
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
        var stringSubscription = mediatr.Subscribe<string>(msg =>
        {
            stringNotifications.Add(msg);
            countdown.Signal();
            return Task.CompletedTask;
        });

        var intSubscription = mediatr.Subscribe<int>(msg =>
        {
            intNotifications.Add(msg);
            countdown.Signal();
            return Task.CompletedTask;
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

    [Fact]
    public async Task Subscribe_ShouldHandleMultipleSubscriptionsForSameType()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();
        var countdown = new CountdownEvent(3);

        // Act
        var subscription1 = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add($"Handler1: {msg}");
            countdown.Signal();
            return Task.CompletedTask;
        });

        var subscription2 = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add($"Handler2: {msg}");
            countdown.Signal();
            return Task.CompletedTask;
        });

        var subscription3 = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add($"Handler3: {msg}");
            countdown.Signal();
            return Task.CompletedTask;
        });

        mediatr.Publish("Test Message");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Not all handlers were notified");
        Assert.Equal(3, notifications.Count);
        Assert.Contains("Handler1: Test Message", notifications);
        Assert.Contains("Handler2: Test Message", notifications);
        Assert.Contains("Handler3: Test Message", notifications);

        await subscription1.DisposeAsync();
        await subscription2.DisposeAsync();
        await subscription3.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldHandleDisposalOfOneSubscription()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();
        var countdown = new CountdownEvent(1);

        // Act
        var subscription1 = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add($"Handler1: {msg}");
            countdown.Signal();
            return Task.CompletedTask;
        });

        var subscription2 = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add($"Handler2: {msg}");
            return Task.CompletedTask;
        });

        await subscription2.DisposeAsync();
        mediatr.Publish("Test Message");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Handler was not notified");
        Assert.Single(notifications);
        Assert.Contains("Handler1: Test Message", notifications);

        await subscription1.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldHandleMultipleMessages()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();
        var countdown = new CountdownEvent(3);

        // Act
        var subscription = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add(msg);
            countdown.Signal();
            return Task.CompletedTask;
        });

        mediatr.Publish("Message 1");
        mediatr.Publish("Message 2");
        mediatr.Publish("Message 3");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Not all messages were processed");
        Assert.Equal(3, notifications.Count);
        Assert.Contains("Message 1", notifications);
        Assert.Contains("Message 2", notifications);
        Assert.Contains("Message 3", notifications);

        await subscription.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldHandleNullMessages()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string?>();
        var countdown = new CountdownEvent(1);

        // Act
        var subscription = mediatr.Subscribe<string?>(msg =>
        {
            notifications.Add(msg);
            countdown.Signal();
            return Task.CompletedTask;
        });

        mediatr.Publish<string?>(null);

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Handler was not notified");
        Assert.Single(notifications);
        Assert.Null(notifications.First());

        await subscription.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldHandleConcurrentPublishes()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<int>();
        var countdown = new CountdownEvent(100);

        // Act
        var subscription = mediatr.Subscribe<int>(msg =>
        {
            notifications.Add(msg);
            countdown.Signal();
            return Task.CompletedTask;
        });

        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => mediatr.Publish(i)))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Not all messages were processed");
        Assert.Equal(100, notifications.Count);
        Assert.Equal(Enumerable.Range(0, 100).Sum(), notifications.Sum());

        await subscription.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldAllowResubscriptionAfterDispose()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();
        var countdown = new CountdownEvent(1);

        // Act
        var subscription = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add(msg);
            countdown.Signal();
            return Task.CompletedTask;
        });

        await subscription.DisposeAsync();

        // Subscribe again
        var newSubscription = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add(msg);
            countdown.Signal();
            return Task.CompletedTask;
        });

        mediatr.Publish("Test Message");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Handler was not notified");
        Assert.Single(notifications);
        Assert.Equal("Test Message", notifications.First());

        await newSubscription.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldHandleDisposeWithoutPublish()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();

        // Act
        var subscription = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add(msg);
            return Task.CompletedTask;
        });

        // Assert
        await subscription.DisposeAsync();
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task Subscribe_ShouldHandlePublishAfterAllDisposed()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();

        // Act
        var subscription1 = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add($"Handler1: {msg}");
            return Task.CompletedTask;
        });

        var subscription2 = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add($"Handler2: {msg}");
            return Task.CompletedTask;
        });

        await subscription1.DisposeAsync();
        await subscription2.DisposeAsync();

        mediatr.Publish("Test Message");

        // Assert
        Assert.Empty(notifications);
    }

    [Fact]
    public void Subscribe_ShouldHandleSelfDisposal()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();
        var countdown = new CountdownEvent(1);
        IAsyncDisposable? subscription = null;

        // Act
        subscription = mediatr.Subscribe<string>(msg =>
        {
            notifications.Add(msg);
            countdown.Signal();
            if (subscription != null)
                return subscription.DisposeAsync().AsTask();
            return Task.CompletedTask;
        });

        mediatr.Publish("First Message");
        mediatr.Publish("Second Message");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Handler was not called");
        Assert.Single(notifications);
        Assert.Equal("First Message", notifications.First());
    }

    [Fact]
    public async Task Subscribe_ShouldHandleErrorInErrorHandler()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();
        var countdown = new CountdownEvent(1);

        // Act
        var subscription = mediatr.Subscribe<string>(
            msg => throw new Exception("Test error"),
            (msg, ex) =>
            {
                notifications.Add($"Error: {msg}");
                countdown.Signal();
                throw new Exception("Error in error handler");
            }
        );

        mediatr.Publish("Test Message");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Error handler was not called");
        Assert.Single(notifications);
        Assert.Equal("Error: Test Message", notifications.First());

        await subscription.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldHandleMultipleErrorHandlers()
    {
        // Arrange
        var mediatr = new Mediatr();
        var errors = new ConcurrentBag<(string Message, Exception Error)>();
        var countdown = new CountdownEvent(2);

        // Act
        var subscription1 = mediatr.Subscribe<string>(
            msg => throw new Exception("Error 1"),
            (msg, ex) =>
            {
                errors.Add((msg, ex));
                countdown.Signal();
            }
        );

        var subscription2 = mediatr.Subscribe<string>(
            msg => throw new Exception("Error 2"),
            (msg, ex) =>
            {
                errors.Add((msg, ex));
                countdown.Signal();
            }
        );

        mediatr.Publish("Test Message");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Not all error handlers were called");
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Error.Message == "Error 1");
        Assert.Contains(errors, e => e.Error.Message == "Error 2");

        await subscription1.DisposeAsync();
        await subscription2.DisposeAsync();
    }

    [Fact]
    public async Task RaceCondition_PublishAndDisposeAsync()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<int>();
        var subscriptions = Enumerable.Range(0, 10)
            .Select(_ => mediatr.Subscribe<int>(msg => { notifications.Add(msg); return Task.CompletedTask; }))
            .ToArray();

        // Act
        var publishTasks = Enumerable.Range(0, 1000)
            .Select(i => Task.Run(() => mediatr.Publish(i)))
            .ToArray();
        var disposeTasks = subscriptions.Select(s => Task.Run(async () => await s.DisposeAsync())).ToArray();

        await Task.WhenAll(publishTasks.Concat(disposeTasks));

        // Assert
        // Не должно быть исключений и падений, количество уведомлений <= 1000*10
        Assert.All(notifications, n => Assert.InRange(n, 0, 999));
    }

    [Fact]
    public async Task RaceCondition_SubscribeAndPublish()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<int>();
        var countdown = new CountdownEvent(1000);

        // Act
        var subscribeTasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => mediatr.Subscribe<int>(msg => { notifications.Add(msg); countdown.Signal(); return Task.CompletedTask; })))
            .ToArray();
        var publishTasks = Enumerable.Range(0, 1000)
            .Select(i => Task.Run(() => mediatr.Publish(i)))
            .ToArray();

        await Task.WhenAll(subscribeTasks.Concat(publishTasks));
        countdown.Wait(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(notifications.Count >= 1000, $"Expected at least 1000 notifications, got {notifications.Count}");
    }

    [Fact(Skip = "Long running load test")]
    public async Task LoadTest_ManySubscribersAndPublishes()
    {
        // Arrange
        var mediatr = new Mediatr();
        const int subscriberCount = 200;
        const int messageCount = 100_000;
        var notifications = new ConcurrentBag<int>();
        var countdown = new CountdownEvent(subscriberCount * messageCount);
        var subscriptions = Enumerable.Range(0, subscriberCount)
            .Select(_ => mediatr.Subscribe<int>(msg => { notifications.Add(msg); countdown.Signal(); return Task.CompletedTask; }))
            .ToArray();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var publishTasks = Enumerable.Range(0, messageCount)
            .Select(i => Task.Run(() => mediatr.Publish(i)))
            .ToArray();
        await Task.WhenAll(publishTasks);

        // Ждем максимум 10 секунд, чтобы не зависнуть
        countdown.Wait(TimeSpan.FromSeconds(10));
        sw.Stop();

        // Assert
        Assert.True(countdown.CurrentCount == 0, $"Not all messages were processed: {countdown.CurrentCount} left");
        Assert.Equal(subscriberCount * messageCount, notifications.Count);
        System.Console.WriteLine($"Processed {notifications.Count} notifications in {sw.ElapsedMilliseconds} ms");

        foreach (var s in subscriptions)
            await s.DisposeAsync();
    }

    [Fact(Skip = "Long running load test")]
    public async Task LoadTest_MultipleTypesAndManySubscribers()
    {
        // Arrange
        var mediatr = new Mediatr();
        const int subscriberCount = 500; // Увеличили количество подписчиков
        const int messageCount = 50_000;

        var intNotifications = new ConcurrentBag<int>();
        var stringNotifications = new ConcurrentBag<string>();
        var dateNotifications = new ConcurrentBag<DateTime>();

        var intCountdown = new CountdownEvent(subscriberCount * messageCount);
        var stringCountdown = new CountdownEvent(subscriberCount * messageCount);
        var dateCountdown = new CountdownEvent(subscriberCount * messageCount);

        // Создаем подписчиков для каждого типа
        var intSubscriptions = Enumerable.Range(0, subscriberCount)
            .Select(_ => mediatr.Subscribe<int>(msg =>
            {
                intNotifications.Add(msg);
                intCountdown.Signal();
                return Task.CompletedTask;
            }))
            .ToArray();

        var stringSubscriptions = Enumerable.Range(0, subscriberCount)
            .Select(_ => mediatr.Subscribe<string>(msg =>
            {
                stringNotifications.Add(msg);
                stringCountdown.Signal();
                return Task.CompletedTask;
            }))
            .ToArray();

        var dateSubscriptions = Enumerable.Range(0, subscriberCount)
            .Select(_ => mediatr.Subscribe<DateTime>(msg =>
            {
                dateNotifications.Add(msg);
                dateCountdown.Signal();
                return Task.CompletedTask;
            }))
            .ToArray();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act - публикуем сообщения разных типов параллельно
        var publishTasks = new List<Task>();

        // Публикуем int
        publishTasks.AddRange(Enumerable.Range(0, messageCount)
            .Select(i => Task.Run(() => mediatr.Publish(i))));

        // Публикуем string
        publishTasks.AddRange(Enumerable.Range(0, messageCount)
            .Select(i => Task.Run(() => mediatr.Publish($"Message {i}"))));

        // Публикуем DateTime
        publishTasks.AddRange(Enumerable.Range(0, messageCount)
            .Select(i => Task.Run(() => mediatr.Publish(DateTime.Now.AddMinutes(i)))));

        await Task.WhenAll(publishTasks);

        // Ждем обработки всех сообщений (максимум 30 секунд)
        var intCompleted = intCountdown.Wait(TimeSpan.FromSeconds(30));
        var stringCompleted = stringCountdown.Wait(TimeSpan.FromSeconds(30));
        var dateCompleted = dateCountdown.Wait(TimeSpan.FromSeconds(30));

        sw.Stop();

        // Assert
        Assert.True(intCompleted, $"Not all int messages were processed: {intCountdown.CurrentCount} left");
        Assert.True(stringCompleted, $"Not all string messages were processed: {stringCountdown.CurrentCount} left");
        Assert.True(dateCompleted, $"Not all DateTime messages were processed: {dateCountdown.CurrentCount} left");

        Assert.Equal(subscriberCount * messageCount, intNotifications.Count);
        Assert.Equal(subscriberCount * messageCount, stringNotifications.Count);
        Assert.Equal(subscriberCount * messageCount, dateNotifications.Count);

        var totalNotifications = intNotifications.Count + stringNotifications.Count + dateNotifications.Count;
        System.Console.WriteLine($"Processed {totalNotifications} notifications in {sw.ElapsedMilliseconds} ms");
        System.Console.WriteLine($"Average throughput: {totalNotifications / (sw.ElapsedMilliseconds / 1000.0):N0} notifications/second");

        // Cleanup
        foreach (var s in intSubscriptions.Concat(stringSubscriptions).Concat(dateSubscriptions))
            await s.DisposeAsync();
    }

    [Fact]
    public async Task Subscribe_ShouldContinueWorkingAfterUnhandledException()
    {
        // Arrange
        var mediatr = new Mediatr();
        var notifications = new ConcurrentBag<string>();
        var countdown = new CountdownEvent(1);
        var exceptionThrown = false;

        // Act
        var subscription = mediatr.Subscribe<string>(msg =>
        {
            if (!exceptionThrown)
            {
                exceptionThrown = true;
                throw new Exception("Test error");
            }
            notifications.Add(msg);
            countdown.Signal();
            return Task.CompletedTask;
        });

        // First message should throw exception
        mediatr.Publish("First Message");

        // Second message should be processed normally
        mediatr.Publish("Second Message");

        // Assert
        var completed = countdown.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "Handler was not called after exception");
        Assert.Single(notifications);
        Assert.Equal("Second Message", notifications.First());

        await subscription.DisposeAsync();
    }
}