using Microsoft.Extensions.AI;
using Pinecone;
using Umbraco.AI.Core.Models;
using Umbraco.AI.Core.Providers;
using Umbraco.Community.AI.Pinecone.Tests.Fakes;

namespace Umbraco.Community.AI.Pinecone.Tests;

public class PineconeEmbeddingCapabilityTests
{
    private readonly FakePineconeHandler _handler = new();

    [Fact]
    public void Dimensions_are_offered_for_llama_text_embed_v2()
    {
        Assert.Empty(CreateCapability().GetSettingsSupport("llama-text-embed-v2").ToMetadata());
    }

    [Theory]
    [InlineData("multilingual-e5-large")]
    [InlineData("a-model-released-later")]
    public void Dimensions_are_hidden_for_other_models(string modelId)
    {
        var metadata = CreateCapability().GetSettingsSupport(modelId).ToMetadata();

        Assert.Equal("dimensions", metadata[AIModelMetadataKeys.ProfileSettingsUnsupported]);
    }

    [Fact]
    public async Task Lists_only_dense_embedding_models()
    {
        IAICapability capability = CreateCapability();

        var models = await capability.GetModelsAsync(TestProvider.Settings(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["llama-text-embed-v2", "multilingual-e5-large"], models.Select(m => m.Model.ModelId));
        Assert.All(models, m => Assert.Equal("pinecone", m.Model.ProviderId));

        var request = Assert.Single(_handler.ModelsRequests);
        Assert.Contains("type=embed", request.Query);
        Assert.Contains("vector_type=dense", request.Query);
    }

    [Fact]
    public async Task Listing_models_checks_the_key_with_an_embed_request()
    {
        // Pinecone's models endpoint is public, so on its own it would accept any key.
        IAICapability capability = CreateCapability();

        await capability.GetModelsAsync(TestProvider.Settings(), cancellationToken: TestContext.Current.CancellationToken);

        var check = Assert.Single(_handler.EmbedRequests);
        Assert.Equal(TestProvider.ApiKey, check.ApiKey);
        Assert.Equal(1, check.Json!.RootElement.GetProperty("inputs").GetArrayLength());
    }

    [Fact]
    public async Task A_rejected_key_fails_the_model_list()
    {
        _handler.RejectApiKey = true;
        IAICapability capability = CreateCapability();

        await Assert.ThrowsAsync<UnauthorizedError>(() => capability.GetModelsAsync(TestProvider.Settings(), cancellationToken: TestContext.Current.CancellationToken));

        // Nothing is cached, so fixing the key works straight away
        _handler.RejectApiKey = false;
        var models = await capability.GetModelsAsync(TestProvider.Settings(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(models);
    }

    [Fact]
    public async Task Model_list_is_cached()
    {
        IAICapability capability = CreateCapability();

        await capability.GetModelsAsync(TestProvider.Settings(), cancellationToken: TestContext.Current.CancellationToken);
        await capability.GetModelsAsync(TestProvider.Settings(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(_handler.ModelsRequests);
        Assert.Single(_handler.EmbedRequests);
    }

    [Fact]
    public async Task Missing_api_key_fails_without_calling_Pinecone()
    {
        IAICapability capability = CreateCapability();

        await Assert.ThrowsAsync<InvalidOperationException>(() => capability.GetModelsAsync(new PineconeProviderSettings(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Dimensions_reach_Pinecone_only_for_models_that_accept_them()
    {
        var options = new EmbeddingGenerationOptions { Dimensions = 384 };
        IAIEmbeddingCapability capability = CreateCapability();

        var llama = await capability.CreateGeneratorAsync(TestProvider.Settings(), "llama-text-embed-v2", cancellationToken: TestContext.Current.CancellationToken);
        var e5 = await capability.CreateGeneratorAsync(TestProvider.Settings(), "multilingual-e5-large", cancellationToken: TestContext.Current.CancellationToken);
        await llama.GenerateAsync(["text"], options, cancellationToken: TestContext.Current.CancellationToken);
        await e5.GenerateAsync(["text"], options, cancellationToken: TestContext.Current.CancellationToken);

        var requests = _handler.EmbedRequests.ToList();
        Assert.Equal(384, requests[0].Parameters.GetProperty("dimension").GetInt32());
        Assert.False(requests[1].Parameters.TryGetProperty("dimension", out _));
    }

    [Fact]
    public async Task Uses_the_default_model_when_the_profile_has_none()
    {
        IAIEmbeddingCapability capability = CreateCapability();

        var generator = await capability.CreateGeneratorAsync(TestProvider.Settings(), modelId: null, cancellationToken: TestContext.Current.CancellationToken);
        await generator.GenerateAsync(["text"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PineconeConstants.DefaultEmbeddingModel, Assert.Single(_handler.EmbedRequests).Json!.RootElement.GetProperty("model").GetString());
    }

    private PineconeEmbeddingCapability CreateCapability()
        => TestProvider.Create(_handler).GetCapability<PineconeEmbeddingCapability>();
}
