using StackExchange.Redis;

namespace QueueService.Infrastructure;

public class RedisConnection
{
    private readonly ConnectionMultiplexer _redis;
    public IDatabase Db => _redis.GetDatabase();

    public RedisConnection(IConfiguration config)
    {
        _redis = ConnectionMultiplexer.Connect(
            config.GetConnectionString("Redis"));
    }
}
