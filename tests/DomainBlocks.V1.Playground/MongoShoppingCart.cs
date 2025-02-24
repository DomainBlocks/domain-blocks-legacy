using MongoDB.Bson.Serialization.Attributes;

namespace DomainBlocks.V1.Playground;

public class MongoShoppingCart
{
    [BsonId] public Guid SessionId { get; set; }
    public List<string> Items { get; set; } = [];
}