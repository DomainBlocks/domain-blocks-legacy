namespace DomainBlocks.Core.Serialization;

public interface IEventConverter<in TReadEvent, out TWriteEvent> :
    IReadEventConverter<TReadEvent>,
    IWriteEventConverter<TWriteEvent>;