using Microsoft.Extensions.AI;
using Umbraco.Community.AI.Pinecone.Tests.Fakes;

namespace Umbraco.Community.AI.Pinecone.Tests;

public class PineconeEmbeddingGeneratorTests
{
    private readonly FakePineconeHandler _handler = new();

    [Fact]
    public async Task Sends_model_inputs_and_passage_by_default()
    {
        await CreateGenerator().GenerateAsync(["first", "second"], cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(_handler.EmbedRequests);
        var root = request.Json!.RootElement;
        Assert.Equal(TestProvider.ApiKey, request.ApiKey);
        Assert.Equal("llama-text-embed-v2", root.GetProperty("model").GetString());
        Assert.Equal(["first", "second"], root.GetProperty("inputs").EnumerateArray().Select(i => i.GetProperty("text").GetString()));
        Assert.Equal("passage", request.Parameters.GetProperty("input_type").GetString());
    }

    [Fact]
    public async Task Sends_query_when_the_options_say_so()
    {
        var options = new EmbeddingGenerationOptions
        {
            AdditionalProperties = new() { [PineconeConstants.InputTypeOptionKey] = "query" },
        };

        await CreateGenerator().GenerateAsync(["what is pinecone"], options, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("query", Assert.Single(_handler.EmbedRequests).Parameters.GetProperty("input_type").GetString());
    }

    [Fact]
    public async Task Model_in_the_options_overrides_the_bound_model()
    {
        await CreateGenerator().GenerateAsync(["text"], new EmbeddingGenerationOptions { ModelId = "multilingual-e5-large" }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("multilingual-e5-large", Assert.Single(_handler.EmbedRequests).Json!.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task Dimension_and_truncate_are_only_sent_when_set()
    {
        await CreateGenerator().GenerateAsync(["text"], cancellationToken: TestContext.Current.CancellationToken);
        await CreateGenerator(truncate: "start").GenerateAsync(["text"], new EmbeddingGenerationOptions { Dimensions = 384 }, cancellationToken: TestContext.Current.CancellationToken);

        var requests = _handler.EmbedRequests.ToList();
        Assert.False(requests[0].Parameters.TryGetProperty("dimension", out _));
        Assert.False(requests[0].Parameters.TryGetProperty("truncate", out _));
        Assert.Equal(384, requests[1].Parameters.GetProperty("dimension").GetInt32());
        Assert.Equal("START", requests[1].Parameters.GetProperty("truncate").GetString());
    }

    [Fact]
    public async Task Large_inputs_are_split_into_batches_of_96_in_order()
    {
        var inputs = Enumerable.Range(0, 200).Select(i => $"chunk {i}").ToList();

        var result = await CreateGenerator().GenerateAsync(inputs, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([96, 96, 8], _handler.EmbedRequests.Select(r => r.Json!.RootElement.GetProperty("inputs").GetArrayLength()));
        Assert.Equal(200, result.Count);
        Assert.All(result.Select((embedding, index) => (embedding, index)), x => Assert.Equal(x.index, x.embedding.Vector.Span[0]));
    }

    [Fact]
    public async Task Usage_is_totalled_across_batches()
    {
        var result = await CreateGenerator().GenerateAsync(Enumerable.Range(0, 100).Select(i => $"chunk {i}"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(100, result.Usage?.InputTokenCount);
        Assert.Equal(100, result.Usage?.TotalTokenCount);
    }

    [Fact]
    public async Task No_inputs_means_no_request()
    {
        var result = await CreateGenerator().GenerateAsync([], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Sparse_embeddings_are_rejected()
    {
        _handler.ReturnSparse = true;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateGenerator().GenerateAsync(["text"], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("sparse", exception.Message);
    }

    [Fact]
    public async Task Logs_the_input_type_but_not_the_inputs()
    {
        var logger = new RecordingLogger();
        PineconeProvider provider = TestProvider.Create(_handler);
        var generator = new PineconeEmbeddingGenerator(provider.CreateClient(TestProvider.Settings()), PineconeConstants.DefaultEmbeddingModel, null, logger);
        var options = new EmbeddingGenerationOptions
        {
            AdditionalProperties = new() { [PineconeConstants.InputTypeOptionKey] = "query" },
        };

        await generator.GenerateAsync(["a secret search"], options, cancellationToken: TestContext.Current.CancellationToken);

        var (level, message) = Assert.Single(logger.Entries);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Debug, level);
        Assert.Equal("Embedding 1 input(s) with Pinecone model llama-text-embed-v2 as query", message);
        Assert.DoesNotContain("secret", message);
        Assert.DoesNotContain(TestProvider.ApiKey, message);
    }

    [Fact]
    public void Reports_its_metadata()
    {
        var metadata = CreateGenerator().GetService<EmbeddingGeneratorMetadata>();

        Assert.Equal("pinecone", metadata?.ProviderName);
        Assert.Equal("llama-text-embed-v2", metadata?.DefaultModelId);
    }

    private PineconeEmbeddingGenerator CreateGenerator(string? truncate = null)
    {
        PineconeProvider provider = TestProvider.Create(_handler);

        return new PineconeEmbeddingGenerator(provider.CreateClient(TestProvider.Settings()), PineconeConstants.DefaultEmbeddingModel, truncate);
    }
}
