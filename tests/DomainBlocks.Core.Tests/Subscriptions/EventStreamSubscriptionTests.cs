using DomainBlocks.Core.Subscriptions;
using DomainBlocks.Core.Subscriptions.Concurrency;
using Moq;
using NUnit.Framework;

namespace DomainBlocks.Core.Tests.Subscriptions;

[TestFixture]
[CancelAfter(1000)]
public class EventStreamSubscriptionTests
{
    private Mock<IEventStream<string, int>> _mockEventStream = null!;
    private Mock<IEventStreamConsumer<string, int>> _mockConsumer = null!;
    private ArenaQueue<QueueNotification<string, int>> _queue = null!;
    private EventStreamSubscription<string, int> _subscription = null!;
    private IEventStreamSubscriber<string, int> _subscriber = null!;
    private TaskCompletionSource<IDisposable> _subscribeTaskCompletionSource = null!;
    private Mock<IComparer<int?>> _mockPositionComparer = null!;

    [SetUp]
    public void SetUp()
    {
        _mockEventStream = new Mock<IEventStream<string, int>>();

        _mockConsumer = new Mock<IEventStreamConsumer<string, int>>();
        _mockPositionComparer = new Mock<IComparer<int?>>();
        _mockConsumer.Setup(x => x.CatchUpCheckpointFrequency).Returns(CheckpointFrequency.Default);
        _mockConsumer.Setup(x => x.LiveCheckpointFrequency).Returns(CheckpointFrequency.Default);

        _queue = new ArenaQueue<QueueNotification<string, int>>(1);

        _subscription =
            new EventStreamSubscription<string, int>(
                _mockEventStream.Object,
                [_mockConsumer.Object],
                _queue,
                _mockPositionComparer.Object);

        _subscriber = _subscription;

        _subscribeTaskCompletionSource = new TaskCompletionSource<IDisposable>();

        _mockEventStream
            .Setup(x => x.Subscribe(_subscriber, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(_subscribeTaskCompletionSource.Task);
    }

    [Test]
    public async Task CatchingUpNotificationIsHandled()
    {
        var signal = new TaskCompletionSource();

        _mockConsumer
            .Setup(x => x.OnCatchingUp(It.IsAny<CancellationToken>()))
            .Callback(() => signal.SetResult());

        var startTask = _subscription.StartAsync();
        await _subscriber.OnCatchingUp(CancellationToken.None);
        CompleteSubscribing();
        await startTask;
        await signal.Task;

        _mockConsumer.Verify(x => x.OnCatchingUp(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task EventNotificationIsHandled()
    {
        var signal = new TaskCompletionSource();

        _mockConsumer
            .Setup(x => x.OnEvent(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnEventResult.Processed)
            .Callback(() => signal.SetResult());

        var startTask = _subscription.StartAsync();
        await _subscriber.OnEvent("event1", 0, CancellationToken.None);
        CompleteSubscribing();
        await startTask;
        await signal.Task;

        _mockConsumer.Verify(x => x.OnEvent("event1", 0, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task LiveNotificationIsHandled()
    {
        var signal = new TaskCompletionSource();

        _mockConsumer
            .Setup(x => x.OnLive(It.IsAny<CancellationToken>()))
            .Callback(() => signal.SetResult());

        var startTask = _subscription.StartAsync();
        await _subscriber.OnLive(CancellationToken.None);
        CompleteSubscribing();
        await startTask;
        await signal.Task;

        _mockConsumer.Verify(x => x.OnLive(It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AbortedEventErrorIsHandled()
    {
        var exception = new Exception("Something bad happened");

        _mockConsumer
            .Setup(x => x.OnEvent(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        _mockConsumer
            .Setup(x => x.OnEventError(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventErrorResolution.Abort);

        var startTask = _subscription.StartAsync();
        await _subscriber.OnEvent("event1", 0, CancellationToken.None);

        var actualException = Assert.ThrowsAsync<Exception>(() => startTask);
        Assert.That(actualException, Is.EqualTo(exception));
    }

    [Test]
    public async Task SkippedEventErrorIsHandled()
    {
        var signal = new TaskCompletionSource();
        var exception = new Exception("Error handling event");
        var onEventResults = new Queue<Func<string, int, CancellationToken, Task<OnEventResult>>>();

        // Set first invocation of OnEvent to throw.
        onEventResults.Enqueue((_, _, _) => throw exception);

        // Set second invocation of OnEvent to succeed.
        onEventResults.Enqueue((_, _, _) =>
        {
            signal.SetResult();
            return Task.FromResult(OnEventResult.Processed);
        });

        _mockConsumer
            .Setup(x => x.OnEvent(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, int, CancellationToken>((e, p, ct) => onEventResults.Dequeue()(e, p, ct));

        _mockConsumer
            .Setup(x => x.OnEventError(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventErrorResolution.Skip);

        var startTask = _subscription.StartAsync();
        await _subscriber.OnEvent("event1", 0, CancellationToken.None);

        // Check we can handle a second event after skipping the error.
        await _subscriber.OnEvent("event2", 1, CancellationToken.None);

        CompleteSubscribing();
        await startTask;
        await signal.Task;

        _mockConsumer.Verify(x => x.OnEvent("event2", 1, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task RetriedEventErrorIsHandled()
    {
        var signal = new TaskCompletionSource();
        var exception = new Exception("Error handling event");
        var onEventResults = new Queue<Func<string, int, CancellationToken, Task<OnEventResult>>>();

        // Set first invocation of OnEvent to throw.
        onEventResults.Enqueue((_, _, _) => throw exception);

        // Set second invocation of OnEvent to succeed.
        onEventResults.Enqueue((_, _, _) =>
        {
            signal.SetResult();
            return Task.FromResult(OnEventResult.Processed);
        });

        _mockConsumer
            .Setup(x => x.OnEvent(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, int, CancellationToken>((e, p, ct) => onEventResults.Dequeue()(e, p, ct));

        _mockConsumer
            .Setup(x => x.OnEventError(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EventErrorResolution.Retry);

        var startTask = _subscription.StartAsync();
        await _subscriber.OnEvent("event1", 0, CancellationToken.None);
        CompleteSubscribing();
        await startTask;
        await signal.Task;

        _mockConsumer.Verify(x => x.OnEvent("event1", 0, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task SubscriptionSubscribesFromMinimumStartPositionOfSubscribers()
    {
        const int startPosition1 = 1;
        const int startPosition2 = 2;

        var mockConsumer1 = new Mock<IEventStreamConsumer<string, int>>();
        var mockConsumer2 = new Mock<IEventStreamConsumer<string, int>>();

        mockConsumer1.Setup(x => x.OnStarting(It.IsAny<CancellationToken>())).ReturnsAsync(startPosition1);
        mockConsumer2.Setup(x => x.OnStarting(It.IsAny<CancellationToken>())).ReturnsAsync(startPosition2);

        var subscription = new EventStreamSubscription<string, int>(
            _mockEventStream.Object,
            [mockConsumer1.Object, mockConsumer2.Object],
            _mockPositionComparer.Object);

        var startTask = subscription.StartAsync();
        CompleteSubscribing();
        await startTask;

        _mockEventStream.Verify(x => x.Subscribe(subscription, startPosition1, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task EventBeforeSubscriberStartPositionIsIgnored()
    {
        const int startPosition = 1;

        _mockConsumer.Setup(x => x.OnStarting(It.IsAny<CancellationToken>())).ReturnsAsync(startPosition);

        var startTask = _subscription.StartAsync();

        await _subscriber.OnCatchingUp(CancellationToken.None);
        await _subscriber.OnEvent("event1", 0, CancellationToken.None);
        await _subscriber.OnEvent("event2", 1, CancellationToken.None);
        await _subscriber.OnEvent("event3", 2, CancellationToken.None);
        await _subscriber.OnLive(CancellationToken.None);

        CompleteSubscribing();
        await startTask;

        _queue.Complete();
        await _subscription.WaitForCompletedAsync();

        _mockConsumer.Verify(x => x.OnEvent("event1", 0, It.IsAny<CancellationToken>()), Times.Never);
        _mockConsumer.Verify(x => x.OnEvent("event2", 1, It.IsAny<CancellationToken>()), Times.Never);
        _mockConsumer.Verify(x => x.OnEvent("event3", 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void CompleteSubscribing()
    {
        _subscribeTaskCompletionSource.SetResult(new Mock<IDisposable>().Object);
    }
}