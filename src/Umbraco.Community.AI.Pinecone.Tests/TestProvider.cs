using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Community.AI.Pinecone.Client;
using Umbraco.Community.AI.Pinecone.Tests.Fakes;

namespace Umbraco.Community.AI.Pinecone.Tests;

internal static class TestProvider
{
    public const string ApiKey = "test-api-key";

    public static PineconeProviderSettings Settings(string? truncate = null) => new() { ApiKey = ApiKey, Truncate = truncate };

    public static PineconeProvider Create(FakePineconeHandler handler)
        => new(new FakeProviderInfrastructure(), new MemoryCache(new MemoryCacheOptions()), new FakeHttpClientFactory(handler), NullLoggerFactory.Instance);

    /// <summary>
    /// A client whose retry waits are recorded in <paramref name="delays"/> instead of slept.
    /// </summary>
    public static PineconeInferenceClient CreateClient(FakePineconeHandler handler, string? apiKey = ApiKey, List<TimeSpan>? delays = null)
        => new(new HttpClient(handler, disposeHandler: false), apiKey, NullLogger<PineconeInferenceClient>.Instance)
        {
            Delay = (delay, _) =>
            {
                delays?.Add(delay);
                return Task.CompletedTask;
            },
        };
}
