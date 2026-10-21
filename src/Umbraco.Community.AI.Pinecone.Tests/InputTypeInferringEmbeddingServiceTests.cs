using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.AI.Core.Embeddings;
using Umbraco.Community.AI.Pinecone.Composers;
using Umbraco.Community.AI.Pinecone.Embeddings;
using Umbraco.Community.AI.Pinecone.Tests.Fakes;

namespace Umbraco.Community.AI.Pinecone.Tests;

public class InputTypeInferringEmbeddingServiceTests
{
    private readonly RecordingEmbeddingService _inner = new();

    [Fact]
    public async Task One_value_is_embedded_as_a_query()
    {
        await Service.GenerateEmbeddingAsync(b => b.WithAlias("ai-search-query").AsPassThrough(), "search text", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("query", InferredInputType());
    }

    [Fact]
    public async Task Several_values_are_embedded_as_passages()
    {
        await Service.GenerateEmbeddingsAsync(b => b.WithAlias("ai-search-indexer").AsPassThrough(), ["chunk 1", "chunk 2"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("passage", InferredInputType());
    }

    [Fact]
    public async Task Callers_own_options_replace_the_hint()
    {
        var callerOptions = new EmbeddingGenerationOptions { Dimensions = 512 };

        await Service.GenerateEmbeddingAsync(b => b.WithAlias("custom").WithEmbeddingOptions(callerOptions), "text", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(callerOptions, _inner.LastOptions);
    }

    [Fact]
    public async Task Reusable_generators_are_not_tagged()
    {
        Action<AIEmbeddingBuilder> configure = b => b.WithAlias("custom");

        await Assert.ThrowsAsync<NotSupportedException>(() => Service.CreateEmbeddingGeneratorAsync(configure, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(configure, _inner.LastConfigure);
    }

#pragma warning disable CS0618 // The deprecated overloads are still part of the interface the decorator implements

    [Fact]
    public async Task Legacy_overloads_are_tagged_and_keep_the_callers_properties()
    {
        var callerOptions = new EmbeddingGenerationOptions
        {
            Dimensions = 256,
            AdditionalProperties = new() { ["custom"] = "value" },
        };

        await Service.GenerateEmbeddingAsync("text", callerOptions, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("query", InferredInputType());
        Assert.Equal(256, _inner.LastOptions?.Dimensions);
        Assert.Equal("value", _inner.LastOptions?.AdditionalProperties?["custom"]);

        await Service.GenerateEmbeddingsAsync(Guid.NewGuid(), ["a", "b"], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("passage", InferredInputType());

        // The caller's instance is never changed
        Assert.False(callerOptions.AdditionalProperties.ContainsKey(PineconeConstants.InputTypeOptionKey));
    }

    [Fact]
    public async Task Legacy_overloads_keep_an_inferred_type_the_caller_already_set()
    {
        var callerOptions = new EmbeddingGenerationOptions
        {
            AdditionalProperties = new() { [PineconeConstants.InputTypeOptionKey] = "passage" },
        };

        await Service.GenerateEmbeddingAsync("text", callerOptions, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("passage", InferredInputType());
    }

#pragma warning restore CS0618

    [Fact]
    public void Decorating_wraps_the_registered_service_and_keeps_its_lifetime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAIEmbeddingService, RecordingEmbeddingService>();

        PineconeComposer.DecorateEmbeddingService(services);
        using ServiceProvider provider = services.BuildServiceProvider();

        var service = Assert.IsType<InputTypeInferringEmbeddingService>(provider.GetRequiredService<IAIEmbeddingService>());
        Assert.IsType<RecordingEmbeddingService>(service.Inner);
        Assert.Same(service, provider.GetRequiredService<IAIEmbeddingService>());
        Assert.Single(services, d => d.ServiceType == typeof(IAIEmbeddingService));
    }

    [Fact]
    public void Decorating_without_a_registration_does_nothing()
    {
        var services = new ServiceCollection();

        PineconeComposer.DecorateEmbeddingService(services);

        Assert.Empty(services);
    }

    private InputTypeInferringEmbeddingService Service => new(_inner);

    private string? InferredInputType()
        => _inner.LastOptions?.AdditionalProperties?.TryGetValue(PineconeConstants.InputTypeOptionKey, out var value) == true
            ? value as string
            : null;
}
