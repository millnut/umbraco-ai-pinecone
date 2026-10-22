using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Umbraco.Community.AI.Pinecone.Client;

/// <summary>
/// A small client for the parts of Pinecone's inference API the provider needs.
/// </summary>
/// <remarks>
/// Calls Pinecone's REST API directly. Every call awaits with <c>ConfigureAwait(false)</c>. Retries are done
/// here rather than with Microsoft.Extensions.Http.Resilience, whose Polly telemetry logs every request at
/// Information level in a default Umbraco site.
/// </remarks>
internal sealed class PineconeInferenceClient
{
    /// <summary>
    /// The Pinecone API version every request asks for.
    /// </summary>
    public const string ApiVersion = "2026-07";

    /// <summary>
    /// Attempts per request, including the first. Embedding and listing models change nothing, so retrying is safe.
    /// </summary>
    public const int MaxAttempts = 4;

    private const string BaseUrl = "https://api.pinecone.io/";

    /// <summary>
    /// Longest wait between attempts, including one Pinecone asks for with <c>Retry-After</c>.
    /// </summary>
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger _logger;

    public PineconeInferenceClient(HttpClient httpClient, string? apiKey, ILogger logger)
    {
        // Checked before any request so a connection without a key fails clearly, not with Pinecone's 401.
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new PineconeException("A Pinecone API key is required. Set it on the Umbraco AI connection.");
        }

        _httpClient = httpClient;
        _apiKey = apiKey;
        _logger = logger;
    }

    /// <summary>
    /// Waits between attempts. Tests replace it so retries don't slow them down.
    /// </summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    public async Task<EmbedResponse> EmbedAsync(EmbedRequest request, CancellationToken cancellationToken)
        => await SendForJsonAsync<EmbedResponse>(HttpMethod.Post, "embed", request, "embed", cancellationToken).ConfigureAwait(false)
            ?? throw new PineconeException("Pinecone returned an empty embed response.");

    /// <summary>
    /// Lists Pinecone's dense embedding models. This endpoint is public, so it says nothing about the API key.
    /// </summary>
    public async Task<IReadOnlyList<ModelInfo>> ListEmbeddingModelsAsync(CancellationToken cancellationToken)
    {
        ModelListResponse? response = await SendForJsonAsync<ModelListResponse>(
            HttpMethod.Get, "models?type=embed&vector_type=dense", null, "list models", cancellationToken).ConfigureAwait(false);

        return response?.Models ?? [];
    }

    private async Task<T?> SendForJsonAsync<T>(HttpMethod method, string path, object? body, string operation, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(method, new Uri(BaseUrl + path), body, operation, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a request with the API key and version headers, retrying transient failures. Throws
    /// <see cref="PineconeException"/> on failure. The caller disposes the response.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri uri, object? body, string operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            // A request message can only be sent once, so each attempt builds its own.
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Add("Api-Key", _apiKey);
            request.Headers.Add("X-Pinecone-Api-Version", ApiVersion);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex, cancellationToken))
            {
                await WaitBeforeRetryAsync(operation, attempt, null, ex.GetType().Name, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            using (response)
            {
                if (attempt < MaxAttempts && IsTransient(response.StatusCode))
                {
                    await WaitBeforeRetryAsync(operation, attempt, response.Headers.RetryAfter, $"{(int)response.StatusCode}", cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw await CreateExceptionAsync(response, operation, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    /// <summary>
    /// A network failure, or the client timing out. Not the caller cancelling.
    /// </summary>
    private static bool IsTransient(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private async Task WaitBeforeRetryAsync(string operation, int attempt, RetryConditionHeaderValue? retryAfter, string reason, CancellationToken cancellationToken)
    {
        // Exponential backoff with jitter (0.5s, 1s, 2s ...), unless Pinecone says how long to wait.
        TimeSpan delay = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null)
            ?? TimeSpan.FromMilliseconds((250 << attempt) + Random.Shared.Next(0, 250));
        delay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay > MaxRetryDelay ? MaxRetryDelay : delay;

        _logger.LogWarning(
            "Pinecone {Operation} failed ({Reason}), retrying in {Delay} ms (attempt {Attempt} of {MaxAttempts})",
            operation, reason, (int)delay.TotalMilliseconds, attempt + 1, MaxAttempts);

        await Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<PineconeException> CreateExceptionAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var detail = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
        if (detail.Length > 500)
        {
            detail = detail[..500];
        }

        var status = (int)response.StatusCode;
        var message = response.StatusCode == HttpStatusCode.Unauthorized
            ? $"Pinecone rejected the API key ({status}) on {operation}. Check the API key on the Umbraco AI connection."
            : $"Pinecone {operation} failed with {status} {response.ReasonPhrase}.";

        return new PineconeException(detail.Length == 0 ? message : $"{message} {detail}", response.StatusCode);
    }
}
