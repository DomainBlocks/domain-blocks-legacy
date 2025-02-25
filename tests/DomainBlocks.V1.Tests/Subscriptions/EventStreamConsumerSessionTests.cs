using DomainBlocks.V1.Abstractions.Subscriptions;
using DomainBlocks.V1.Subscriptions;
using Moq;

namespace DomainBlocks.V1.Tests.Subscriptions;

[TestFixture]
public class EventStreamConsumerSessionTests
{
    private Mock<IEventStreamConsumer> _mockConsumer = null!;
    private Mock<IEventStreamSubscriptionStatusSource> _mockSubscriptionStatusSource = null!;
    private EventStreamConsumerSession _session = null!;

    [SetUp]
    public void SetUp()
    {
        _mockConsumer = new Mock<IEventStreamConsumer>();
        _mockSubscriptionStatusSource = new Mock<IEventStreamSubscriptionStatusSource>();
        _session = new EventStreamConsumerSession(_mockConsumer.Object, _mockSubscriptionStatusSource.Object);
    }

    [Test]
    public async Task InitializeAsync_InvokesConsumerOnInitializing()
    {
        await _session.InitializeAsync(CancellationToken.None);
        _mockConsumer.Verify(x => x.OnInitializingAsync(CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task InitializeAsync_WhenCalledMoreThanOnce_ThrowsInvalidOperationException()
    {
        await _session.InitializeAsync(CancellationToken.None);

        await Assert.ThatAsync(
            () => _session.InitializeAsync(CancellationToken.None),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task StartAsync_ReturnsPositionFromConsumerOnRestoreAsync()
    {
        var position = new SubscriptionPosition(42);
        _mockConsumer.Setup(x => x.OnRestoreAsync(CancellationToken.None)).ReturnsAsync(new SubscriptionPosition(42));

        await _session.InitializeAsync();
        var result = await _session.StartAsync(CancellationToken.None);

        Assert.That(result, Is.EqualTo(position));
    }

    [Test]
    public async Task StartAsync_StartsReadingMessages()
    {
        await _session.InitializeAsync();
        await _session.StartAsync();
        await _session.FlushAsync();
    }

    [Test]
    public async Task Resume_WhenIsFaulted_ResumesFromFaultedMessage()
    {
        var consumer = new RandomlyFailingConsumer();
        var session = new EventStreamConsumerSession(consumer, _mockSubscriptionStatusSource.Object);

        await session.InitializeAsync();
        await session.StartAsync();

        var events = Enumerable
            .Range(0, 1000)
            .Select(i =>
            {
                var position = new SubscriptionPosition((ulong)i);
                return (new TestEvent(), position);
            })
            .ToArray();

        foreach (var (e, pos) in events)
        {
            await session.NotifyEventReceivedAsync(e, pos);
        }

        var flushTask = session.FlushAsync();

        while (consumer.HandledEvents.Count < events.Length)
        {
            var suspendedTask = session.WaitForCompletedAsync();
            var completedTask = await Task.WhenAny(suspendedTask, flushTask);

            if (completedTask == suspendedTask)
            {
                Assert.That(session.Status, Is.EqualTo(EventStreamConsumerSessionStatus.Suspended));
                Assert.That(session.IsFaulted, Is.True);
                Assert.That(session.Error, Is.Not.Null);
                Assert.That(session.FaultedMessage, Is.Not.Null);

                session.Resume();
            }
            else
            {
                // The flush task completed. We're done.
                break;
            }
        }

        Assert.That(consumer.HandledEvents, Has.Count.EqualTo(events.Length));
        Assert.That(consumer.ErrorCount, Is.GreaterThan(1));
    }

    private class RandomlyFailingConsumer : IEventStreamConsumer, IEventHandler<TestEvent>
    {
        private readonly Random _random = new();
        private readonly List<TestEvent> _handledEvents = [];

        public async Task OnEventAsync(EventHandlerContext<TestEvent> context)
        {
            // Enforce asynchronicity so we properly test the async behaviour.
            await Task.Yield();

            var isError = _random.Next(2) == 1;
            if (isError)
            {
                ErrorCount++;
                throw new Exception("Random error.");
            }

            _handledEvents.Add(context.Event);
        }

        public IReadOnlyCollection<TestEvent> HandledEvents => _handledEvents;
        public int ErrorCount { get; private set; }
    }

    private record TestEvent;
}