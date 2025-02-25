namespace DomainBlocks.Core.Benchmarks;

public static class TestEvents
{
    public static IEnumerable<IEvent> Generate(int count)
    {
        IEvent event1 = new Event1();
        IEvent event2 = new Event2();
        IEvent event3 = new Event3();

        return Enumerable
            .Range(1, count)
            .Select(x => (x % 3) switch
            {
                0 => event3,
                1 => event1,
                2 => event2,
                _ => throw new ArgumentOutOfRangeException()
            })
            .ToArray();
    }

    public interface IEvent;

    public class Event1 : IEvent
    {
        public Event1() => Value = 1;
        public int Value { get; }
    }

    public class Event2 : IEvent
    {
        public Event2() => Value = 2;
        public int Value { get; }
    }

    public class Event3 : IEvent
    {
        public Event3() => Value = 3;
        public int Value { get; }
    }
}