namespace ClassifiedAds.Infrastructure.Caching;

public class CachingOptions
{
    public InMemoryCacheOptions InMemory { get; set; }

    public DistributedCacheOptions Distributed { get; set; }
}

public class InMemoryCacheOptions
{
    public long? SizeLimit { get; set; }
}

public class DistributedCacheOptions
{
    public string Provider { get; set; }

    public InMemoryCacheOptions InMemory { get; set; }

    public RedisOptions Redis { get; set; }
}

public class RedisOptions
{
    public string Configuration { get; set; }

    public string InstanceName { get; set; }
}
