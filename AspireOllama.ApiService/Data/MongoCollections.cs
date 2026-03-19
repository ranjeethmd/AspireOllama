using MongoDB.Driver;

namespace AspireOllama.ApiService.Data;

public class MongoCollections
{
    public IMongoCollection<ChatSessionDocument> Sessions { get; }
    public IMongoCollection<ChatMessageDocument> Messages { get; }

    public MongoCollections(IMongoDatabase database)
    {
        Sessions = database.GetCollection<ChatSessionDocument>("chat_sessions");
        Messages = database.GetCollection<ChatMessageDocument>("chat_messages");
    }
}
