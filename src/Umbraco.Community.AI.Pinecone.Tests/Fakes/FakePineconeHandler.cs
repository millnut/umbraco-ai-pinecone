using System.Net;
using System.Text;
using System.Text.Json;

namespace Umbraco.Community.AI.Pinecone.Tests.Fakes;

/// <summary>
/// Stands in for the Pinecone API. Records every request and answers <c>/embed</c> and <c>/models</c>.
/// </summary>
/// <remarks>
/// Each embedding's first value is the position of its input across all requests, so a test can check
/// that batching keeps the order. Usage reports one token per input.
/// </remarks>
internal sealed class FakePineconeHandler : HttpMessageHandler
{
    private int _embedded;

    public List<RecordedRequest> Requests { get; } = [];

    public bool ReturnSparse { get; set; }

    /// <summary>
    /// Answers <c>/embed</c> with 401, as Pinecone does for a bad key. <c>/models</c> is public, so it still
    /// succeeds, which is also what Pinecone does.
    /// </summary>
    public bool RejectApiKey { get; set; }

    public IEnumerable<RecordedRequest> EmbedRequests => Requests.Where(r => r.Path == "/embed");

    public IEnumerable<RecordedRequest> ModelsRequests => Requests.Where(r => r.Path == "/models");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!.AbsolutePath,
            request.RequestUri.Query,
            request.Headers.TryGetValues("Api-Key", out var keys) ? keys.Single() : null,
            body);
        Requests.Add(recorded);

        return recorded.Path switch
        {
            "/embed" when RejectApiKey => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":{"code":"UNAUTHENTICATED","message":"Invalid API key"},"status":401}""", Encoding.UTF8, "application/json"),
            },
            "/embed" => Json(EmbedResponse(recorded.Json!.RootElement)),
            "/models" => Json(ModelsResponse),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private string EmbedResponse(JsonElement request)
    {
        var count = request.GetProperty("inputs").GetArrayLength();
        var data = Enumerable.Range(0, count).Select(_ => ReturnSparse
            ? (object)new { vector_type = "sparse", sparse_values = new[] { 0.5f }, sparse_indices = new[] { 1L } }
            : new { vector_type = "dense", values = new[] { (float)_embedded++, 0.25f, 0.5f } });

        return JsonSerializer.Serialize(new
        {
            model = request.GetProperty("model").GetString(),
            vector_type = ReturnSparse ? "sparse" : "dense",
            data,
            usage = new { total_tokens = count },
        });
    }

    private const string ModelsResponse = """
        {
          "models": [
            { "model": "multilingual-e5-large", "short_description": "e5", "type": "embed", "vector_type": "dense", "supported_parameters": [] },
            { "model": "llama-text-embed-v2", "short_description": "llama", "type": "embed", "vector_type": "dense", "supported_parameters": [] },
            { "model": "pinecone-sparse-english-v0", "short_description": "sparse", "type": "embed", "vector_type": "sparse", "supported_parameters": [] },
            { "model": "bge-reranker-v2-m3", "short_description": "reranker", "type": "rerank", "supported_parameters": [] }
          ]
        }
        """;

    private static HttpResponseMessage Json(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    internal sealed record RecordedRequest(HttpMethod Method, string Path, string Query, string? ApiKey, string? Body)
    {
        public JsonDocument? Json { get; } = Body is null ? null : JsonDocument.Parse(Body);

        public JsonElement Parameters => Json!.RootElement.GetProperty("parameters");
    }
}
