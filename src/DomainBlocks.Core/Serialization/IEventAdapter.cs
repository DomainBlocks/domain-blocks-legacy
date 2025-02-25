namespace DomainBlocks.Core.Serialization;

public interface IEventAdapter<in TReadEvent, out TWriteEvent> :
    IReadEventAdapter<TReadEvent>,
    IWriteEventAdapter<TWriteEvent>;