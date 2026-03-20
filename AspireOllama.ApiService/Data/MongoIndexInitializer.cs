using MongoDB.Driver;

namespace AspireOllama.ApiService.Data;

public class MongoIndexInitializer(MongoCollections collections, ILogger<MongoIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating MongoDB indexes...");

        // Messages: query by SessionId, sorted by Timestamp
        await collections.Messages.Indexes.CreateOneAsync(
            new CreateIndexModel<ChatMessageDocument>(
                Builders<ChatMessageDocument>.IndexKeys
                    .Ascending(m => m.SessionId)
                    .Ascending(m => m.Timestamp)),
            cancellationToken: cancellationToken);

        // Sessions: query by UserId, sorted by UpdatedAt descending
        await collections.Sessions.Indexes.CreateOneAsync(
            new CreateIndexModel<ChatSessionDocument>(
                Builders<ChatSessionDocument>.IndexKeys
                    .Ascending(s => s.UserId)
                    .Descending(s => s.UpdatedAt)),
            cancellationToken: cancellationToken);

        logger.LogInformation("MongoDB indexes created");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
