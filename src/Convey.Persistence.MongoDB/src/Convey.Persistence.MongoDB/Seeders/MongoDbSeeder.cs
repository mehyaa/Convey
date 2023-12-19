using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.Persistence.MongoDB.Seeders;

internal class MongoDbSeeder : IMongoDbSeeder
{
    public Task SeedAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}