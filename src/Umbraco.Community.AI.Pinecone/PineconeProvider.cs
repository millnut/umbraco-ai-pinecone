using Microsoft.Extensions.Caching.Memory;
using Pinecone;
using Pinecone.Inference;
using Umbraco.AI.Core.Providers;
using Umbraco.AI.Core.Providers.Errors;

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
    private const string CacheKeyPrefix = "Pinecone_EmbeddingModels_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;

    public PineconeProvider(
        IAIProviderInfrastructure infrastructure,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory)
        : base(infrastructure)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        WithCapability<PineconeEmbeddingCapability>();
    }

    /// <inheritdoc />
    public override AIProviderErrorInfo ClassifyError(Exception exception)
        => exception is PineconeApiException apiException
            ? ProviderErrorMapping.FromHttpStatus(apiException.StatusCode, apiException.Message)
            : base.ClassifyError(exception);

    /// <summary>
    /// Creates a client for the connection's API key.
    /// </summary>
    internal PineconeClient CreateClient(PineconeProviderSettings settings)
    {
        // Checked here because the SDK silently falls back to the PINECONE_API_KEY environment variable
        // when given no key, which would use an account the connection never named.
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("A Pinecone API key is required.");
        }

        return new PineconeClient(settings.ApiKey, new ClientOptions
        {
            HttpClient = _httpClientFactory.CreateClient(PineconeConstants.HttpClientName),
        });
    }

    /// <summary>
    /// Lists Pinecone's dense embedding models, cached per API key, after checking the key works.
    /// </summary>
    /// <remarks>
    /// Testing a connection in the backoffice lists models, so this is what validates the key. Pinecone's
    /// models endpoint is public and accepts any key, so a one-word embed request does the checking. It
    /// costs a token or two, and only when the list isn't cached.
    /// </remarks>
    internal async Task<IReadOnlyList<string>> GetEmbeddingModelIdsAsync(
        PineconeProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        PineconeClient client = CreateClient(settings);

        var cacheKey = $"{CacheKeyPrefix}{settings.ApiKey!.GetHashCode()}";
        if (_cache.TryGetValue<IReadOnlyList<string>>(cacheKey, out var cachedModels) && cachedModels is not null)
        {
            return cachedModels;
        }

        await client.Inference.EmbedAsync(
            new EmbedRequest
            {
                Model = PineconeConstants.DefaultEmbeddingModel,
                Inputs = [new EmbedRequestInputsItem { Text = "test" }],
                Parameters = new Dictionary<string, object?> { ["input_type"] = PineconeConstants.InputTypes.Query },
            },
            cancellationToken: cancellationToken);

        ModelInfoList result = await client.Inference.Models.ListAsync(
            new ListModelsRequest { Type = ModelType.Embed, VectorType = VectorType.Dense },
            cancellationToken: cancellationToken);

        IReadOnlyList<string> modelIds = (result.Models ?? [])
            .Where(m => m.Type == ModelType.Embed && m.VectorType == VectorType.Dense)
            .Select(m => m.Model)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        _cache.Set(cacheKey, modelIds, CacheDuration);

        return modelIds;
    }
}
