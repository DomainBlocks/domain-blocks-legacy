using DomainBlocks.Core.Projections;
using DomainBlocks.Core.Serialization;
using Moq;
using NUnit.Framework;

namespace DomainBlocks.Core.Tests.Projections.Experimental;

[TestFixture]
public class StateProjectionSubscriberTests
{
    [Test]
    public async Task OnLiveAfterCheckpointHandlesAlreadyCleanedUpState()
    {
        const string event1 = "Event1";
        var mockResource = new Mock<IDisposable>();
        var mockEventAdapter = new Mock<IReadEventAdapter<string>>();

        mockEventAdapter.Setup(x => x.GetEventName(event1)).Returns(event1);

        mockEventAdapter
            .Setup(x => x.DeserializeEvent(event1, typeof(Event1), CancellationToken.None))
            .Returns(Task.FromResult<object>(new Event1()));

        mockEventAdapter
            .Setup(x => x.DeserializeMetadata(event1))
            .Returns(new Dictionary<string, string>());

        var lastPosition = -1;

        var options = new StateProjectionOptions<string, int, object>()
            .WithStateFactory(() => mockResource.Object, (_, _) => new object())
            .WithOnCheckpoint((_, pos, _) =>
            {
                lastPosition = pos;
                return Task.CompletedTask;
            })
            .WithOnEvent<Event1>((_, _) => { })
            .MapMissingEventTypesWithDefaultNames();

        var projection = new StateProjection<string, int, object>(options, mockEventAdapter.Object);

        await projection.OnStarting(CancellationToken.None);
        await projection.OnCatchingUp(CancellationToken.None);
        await projection.OnEvent(event1, 0, CancellationToken.None);
        await projection.OnCheckpoint(0, CancellationToken.None);
        await projection.OnLive(CancellationToken.None);

        Assert.That(lastPosition, Is.EqualTo(0));
    }

    private class Event1;
}