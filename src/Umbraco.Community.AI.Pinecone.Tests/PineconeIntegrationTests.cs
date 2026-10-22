using Microsoft.Extensions.AI;
using Umbraco.AI.Core.Providers;
using Umbraco.Community.AI.Pinecone.Tests.Fakes;

namespace Umbraco.Community.AI.Pinecone.Tests;

/// <summary>
/// Calls the real Pinecone API. Skipped unless the <c>PINECONE_API_KEY</c> environment variable is set.
/// Uses the free inference allowance: a handful of tokens per run.
/// </summary>
public class PineconeIntegrationTests
{
    private static readonly string? ApiKey = Environment.GetEnvironmentVariable("PINECONE_API_KEY");

    [Fact]
    public async Task Embeds_passages_and_queries_with_llama_text_embed_v2()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ApiKey), "Set PINECONE_API_KEY to run against the real Pinecone API.");

        var generator = await CreateGeneratorAsync(PineconeConstants.DefaultEmbeddingModel);

        var passages = await generator.GenerateAsync(["Umbraco is an open source .NET CMS.", "Pinecone is a vector database."], cancellationToken: TestContext.Current.CancellationToken);
        var query = await generator.GenerateAsync(
            ["What is Umbraco?"],
            new EmbeddingGenerationOptions { AdditionalProperties = new() { [PineconeConstants.InputTypeOptionKey] = "query" } }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, passages.Count);
        Assert.All(passages, p => Assert.Equal(1024, p.Vector.Length));
        Assert.Equal(1024, Assert.Single(query).Vector.Length);
        Assert.True(passages.Usage?.TotalTokenCount > 0);
    }

    [Fact]
    public async Task Respects_a_smaller_dimension()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ApiKey), "Set PINECONE_API_KEY to run against the real Pinecone API.");

        var generator = await CreateGeneratorAsync(PineconeConstants.DefaultEmbeddingModel);

        var result = await generator.GenerateAsync(["Umbraco"], new EmbeddingGenerationOptions { Dimensions = 384 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(384, Assert.Single(result).Vector.Length);
    }

    [Fact]
    public async Task Lists_the_hosted_embedding_models()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ApiKey), "Set PINECONE_API_KEY to run against the real Pinecone API.");

        IAICapability capability = CreateProvider().GetCapability<PineconeEmbeddingCapability>();

        var models = await capability.GetModelsAsync(new PineconeProviderSettings { ApiKey = ApiKey }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(models, m => m.Model.ModelId == PineconeConstants.DefaultEmbeddingModel);
    }

    private static Task<IEmbeddingGenerator<string, Embedding<float>>> CreateGeneratorAsync(string modelId)
    {
        IAIEmbeddingCapability capability = CreateProvider().GetCapability<PineconeEmbeddingCapability>();

        return capability.CreateGeneratorAsync(new PineconeProviderSettings { ApiKey = ApiKey }, modelId);
    }

    private static PineconeProvider CreateProvider()
        => new(
            new FakeProviderInfrastructure(),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            new RealHttpClientFactory(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

    private sealed class RealHttpClientFactory : IHttpClientFactory
    {
        private static readonly HttpClient Client = new();

        public HttpClient CreateClient(string name) => Client;
    }
}
