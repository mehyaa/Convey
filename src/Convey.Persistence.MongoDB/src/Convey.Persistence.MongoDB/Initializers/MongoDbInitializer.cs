using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Convey.Persistence.MongoDB.Initializers;

internal sealed class MongoDbInitializer : IMongoDbInitializer
{
    private static int _initialized;

    private readonly IMongoDatabase _database;
    private readonly IMongoDbSeeder _seeder;
    private readonly bool _seedEnabled;

    public MongoDbInitializer(IMongoDatabase database, IMongoDbSeeder seeder, MongoDbOptions options)
    {
        _database = database;
        _seeder = seeder;
        _seedEnabled = options.Seed;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        if (_seedEnabled)
        {
            await _seeder.SeedAsync(_database, cancellationToken);
        }
    }
}