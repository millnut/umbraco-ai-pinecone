using Microsoft.Extensions.Caching.Memory;
using Umbraco.Community.AI.Pinecone.Tests.Fakes;

namespace Umbraco.Community.AI.Pinecone.Tests;

internal static class TestProvider
{
    public const string ApiKey = "test-api-key";

    public static PineconeProviderSettings Settings(string? truncate = null) => new() { ApiKey = ApiKey, Truncate = truncate };

    public static PineconeProvider Create(FakePineconeHandler handler)
        => new(new FakeProviderInfrastructure(), new MemoryCache(new MemoryCacheOptions()), new FakeHttpClientFactory(handler));
}
