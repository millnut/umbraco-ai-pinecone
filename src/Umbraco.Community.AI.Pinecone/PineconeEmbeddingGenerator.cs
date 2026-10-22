using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Umbraco.Community.AI.Pinecone.Client;

namespace Umbraco.Community.AI.Pinecone;

/// <summary>
/// An <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> over Pinecone's embed endpoint.
/// </summary>
/// <remarks>
/// Rate limits (429) and server errors are retried with backoff by <see cref="PineconeInferenceClient"/>. Each request is
/// logged at Debug level with its model, input count and input type; the inputs and the API key are not.
/// </remarks>
internal sealed class PineconeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly PineconeInferenceClient _client;
    private readonly string _modelId;
    private readonly string? _truncate;
    private readonly EmbeddingGeneratorMetadata _metadata;
    private readonly ILogger? _logger;

    public PineconeEmbeddingGenerator(PineconeInferenceClient client, string modelId, string? truncate, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        _client = client;
        _modelId = modelId;
        _truncate = string.IsNullOrWhiteSpace(truncate) ? null : truncate.Trim().ToUpperInvariant();
        _metadata = new EmbeddingGeneratorMetadata(PineconeConstants.ProviderId, defaultModelId: modelId);
        _logger = logger;
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputs = values.ToList();
        var result = new GeneratedEmbeddings<Embedding<float>>(inputs.Count);
        if (inputs.Count == 0)
        {
            return result;
        }

        var modelId = options?.ModelId ?? _modelId;
        Dictionary<string, object?> parameters = BuildParameters(options);
        _logger?.LogDebug(
            "Embedding {InputCount} input(s) with Pinecone model {ModelId} as {InputType}",
            inputs.Count,
            modelId,
            parameters["input_type"]);
        long totalTokens = 0;
        var hasUsage = false;

        foreach (string[] batch in inputs.Chunk(PineconeConstants.MaxBatchSize))
        {
            EmbedResponse response = await _client.EmbedAsync(
                new EmbedRequest(modelId, parameters, batch.Select(text => new EmbedInput(text)).ToList()),
                cancellationToken).ConfigureAwait(false);

            List<EmbeddingData> data = response.Data ?? [];
            if (data.Count != batch.Length)
            {
                throw new InvalidOperationException(
                    $"Pinecone returned {data.Count} embeddings for {batch.Length} inputs.");
            }

            foreach (EmbeddingData embedding in data)
            {
                // Sparse models send sparse_values instead of values.
                if (embedding.Values is null || IsSparse(response.VectorType) || IsSparse(embedding.VectorType))
                {
                    throw new InvalidOperationException(
                        $"Pinecone model '{modelId}' returned sparse embeddings. Choose a dense embedding model.");
                }

                result.Add(new Embedding<float>(embedding.Values)
                {
                    ModelId = response.Model,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }

            if (response.Usage?.TotalTokens is { } tokens)
            {
                totalTokens += tokens;
                hasUsage = true;
            }
        }

        if (hasUsage)
        {
            result.Usage = new UsageDetails { InputTokenCount = totalTokens, TotalTokenCount = totalTokens };
        }

        return result;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceKey is not null)
        {
            return null;
        }

        if (serviceType == typeof(EmbeddingGeneratorMetadata))
        {
            return _metadata;
        }

        if (serviceType.IsInstanceOfType(_client))
        {
            return _client;
        }

        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // The HttpClient comes from IHttpClientFactory, which owns its lifetime.
    }

    private static bool IsSparse(string? vectorType) => string.Equals(vectorType, "sparse", StringComparison.OrdinalIgnoreCase);

    private Dictionary<string, object?> BuildParameters(EmbeddingGenerationOptions? options)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["input_type"] = PineconeInputType.Resolve(options),
        };

        if (_truncate is not null)
        {
            parameters["truncate"] = _truncate;
        }

        // Only reaches here for models that accept it: the capability declares Dimensions unsupported for the
        // rest, and Umbraco AI strips it from their requests.
        if (options?.Dimensions is { } dimensions)
        {
            parameters["dimension"] = dimensions;
        }

        return parameters;
    }
}
