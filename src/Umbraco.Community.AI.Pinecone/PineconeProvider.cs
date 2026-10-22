using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbraco.AI.Core.Providers;
using Umbraco.AI.Core.Providers.Errors;
using Umbraco.Community.AI.Pinecone.Client;

namespace Umbraco.Community.AI.Pinecone;

/// <summary>
/// Umbraco AI provider for Pinecone's hosted embedding models (the Inference API).
/// </summary>
/// <remarks>
/// Umbraco AI discovers providers by their <see cref="AIProviderAttribute"/>, so no registration is needed.
/// </remarks>
[AIProvider(PineconeConstants.ProviderId, PineconeConstants.ProviderName)]
public class PineconeProvider : AIProviderBase<PineconeProviderSettings>
{
    private const string CacheKey = "Pinecone_EmbeddingModels";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PineconeInferenceClient> _clientLogger;

    public PineconeProvider(
        IAIProviderInfrastructure infrastructure,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
        : base(infrastructure)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _clientLogger = loggerFactory.CreateLogger<PineconeInferenceClient>();
        WithCapability<PineconeEmbeddingCapability>();
    }

    /// <inheritdoc />
    public override AIProviderErrorInfo ClassifyError(Exception exception)
        => exception is PineconeException { StatusCode: { } statusCode }
            ? ProviderErrorMapping.FromHttpStatus((int)statusCode, exception.Message)
            : base.ClassifyError(exception);

    /// <summary>
    /// Creates a client for the connection's API key. Throws <see cref="PineconeException"/> when there is no key.
    /// </summary>
    internal PineconeInferenceClient CreateClient(PineconeProviderSettings settings)
        => new(_httpClientFactory.CreateClient(PineconeConstants.HttpClientName), settings.ApiKey, _clientLogger);

    /// <summary>
    /// Checks the API key works, then lists Pinecone's dense embedding models.
    /// </summary>
    /// <remarks>
    /// Testing a connection in the backoffice lists models, so this is what validates the key. Pinecone's
    /// models endpoint is public and accepts any key, so a one-word embed request does the checking, costing a
    /// token or two. It runs every time, so a revoked key fails straight away. Only the model list, which is
    /// the same for every key, is cached.
    /// </remarks>
    internal async Task<IReadOnlyList<string>> GetEmbeddingModelIdsAsync(
        PineconeProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        PineconeInferenceClient client = CreateClient(settings);

        await client.EmbedAsync(
            new EmbedRequest(
                PineconeConstants.DefaultEmbeddingModel,
                new Dictionary<string, object?> { ["input_type"] = PineconeConstants.InputTypes.Query },
                [new EmbedInput("test")]),
            cancellationToken);

        if (_cache.TryGetValue<IReadOnlyList<string>>(CacheKey, out var cachedModels) && cachedModels is not null)
        {
            return cachedModels;
        }

        IReadOnlyList<ModelInfo> models = await client.ListEmbeddingModelsAsync(cancellationToken);

        IReadOnlyList<string> modelIds = models
            .Where(m => string.Equals(m.Type, "embed", StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.VectorType, "dense", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Model)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        _cache.Set(CacheKey, modelIds, CacheDuration);

        return modelIds;
    }
}
