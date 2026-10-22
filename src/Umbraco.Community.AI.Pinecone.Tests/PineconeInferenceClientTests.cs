using System.Net;
using System.Net.Http.Headers;
using Umbraco.Community.AI.Pinecone.Client;
using Umbraco.Community.AI.Pinecone.Tests.Fakes;

namespace Umbraco.Community.AI.Pinecone.Tests;

public class PineconeInferenceClientTests
{
    private readonly FakePineconeHandler _handler = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sends_the_api_key_and_version_on_every_request()
    {
        PineconeInferenceClient client = TestProvider.CreateClient(_handler);

        await client.EmbedAsync(Request(), Ct);
        await client.ListEmbeddingModelsAsync(Ct);

        Assert.Equal(2, _handler.Requests.Count);
        Assert.All(_handler.Requests, r =>
        {
            Assert.Equal(TestProvider.ApiKey, r.ApiKey);
            Assert.Equal(PineconeInferenceClient.ApiVersion, r.ApiVersion);
        });
    }

    [Fact]
    public async Task Sends_the_body_in_snake_case()
    {
        await TestProvider.CreateClient(_handler).EmbedAsync(Request(), Ct);

        var root = Assert.Single(_handler.EmbedRequests).Json!.RootElement;
        Assert.Equal("llama-text-embed-v2", root.GetProperty("model").GetString());
        Assert.Equal("passage", root.GetProperty("parameters").GetProperty("input_type").GetString());
        Assert.Equal("text", Assert.Single(root.GetProperty("inputs").EnumerateArray()).EnumerateObject().Single().Name);
    }

    [Fact]
    public async Task Reads_the_embed_response()
    {
        EmbedResponse response = await TestProvider.CreateClient(_handler).EmbedAsync(Request(), Ct);

        Assert.Equal("dense", response.VectorType);
        float[]? values = Assert.Single(response.Data!).Values;
        Assert.NotNull(values);
        Assert.Equal([0f, 0.25f, 0.5f], values);
        Assert.Equal(1, response.Usage?.TotalTokens);
    }

    [Fact]
    public async Task A_rejected_key_is_reported_without_the_key()
    {
        _handler.RejectApiKey = true;

        PineconeException exception = await Assert.ThrowsAsync<PineconeException>(
            () => TestProvider.CreateClient(_handler, apiKey: "wrong-secret-key").EmbedAsync(Request(), Ct));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("rejected the API key", exception.Message);
        Assert.DoesNotContain("wrong-secret-key", exception.Message);
        Assert.Single(_handler.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void A_missing_key_fails_before_any_request(string? apiKey)
    {
        Assert.Throws<PineconeException>(() => TestProvider.CreateClient(_handler, apiKey: apiKey));

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Server_errors_are_retried_then_reported_with_status_and_detail()
    {
        for (var i = 0; i < PineconeInferenceClient.MaxAttempts; i++)
        {
            _handler.FailNext.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("Something went wrong") });
        }

        PineconeException exception = await Assert.ThrowsAsync<PineconeException>(
            () => TestProvider.CreateClient(_handler).EmbedAsync(Request(), Ct));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Contains("embed", exception.Message);
        Assert.Contains("Something went wrong", exception.Message);
        Assert.Equal(PineconeInferenceClient.MaxAttempts, _handler.EmbedRequests.Count());
    }

    [Fact]
    public async Task Rate_limits_are_retried_after_the_wait_pinecone_asks_for()
    {
        var delays = new List<TimeSpan>();
        var tooMany = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("Too many requests") };
        tooMany.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
        _handler.FailNext.Enqueue(tooMany);
        _handler.FailNext.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        EmbedResponse response = await TestProvider.CreateClient(_handler, delays: delays).EmbedAsync(Request(), Ct);

        Assert.NotNull(response.Data);
        Assert.Equal(3, _handler.EmbedRequests.Count());
        Assert.Equal(TimeSpan.FromSeconds(2), delays[0]);
        Assert.InRange(delays[1], TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1250));
    }

    [Fact]
    public async Task Client_errors_are_not_retried()
    {
        _handler.FailNext.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"code":"INVALID_ARGUMENT","message":"Invalid value 'bogus' for input_type"},"status":400}"""),
        });

        PineconeException exception = await Assert.ThrowsAsync<PineconeException>(
            () => TestProvider.CreateClient(_handler).EmbedAsync(Request(), Ct));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("INVALID_ARGUMENT", exception.Message);
        Assert.Single(_handler.EmbedRequests);
    }

    [Fact]
    public async Task Lists_models_with_the_dense_embedding_filter()
    {
        IReadOnlyList<ModelInfo> models = await TestProvider.CreateClient(_handler).ListEmbeddingModelsAsync(Ct);

        Assert.Contains(models, m => m.Model == "llama-text-embed-v2" && m.VectorType == "dense");
        var request = Assert.Single(_handler.ModelsRequests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("type=embed", request.Query);
        Assert.Contains("vector_type=dense", request.Query);
    }

    private static EmbedRequest Request()
        => new(PineconeConstants.DefaultEmbeddingModel, new Dictionary<string, object?> { ["input_type"] = "passage" }, [new EmbedInput("text")]);
}
