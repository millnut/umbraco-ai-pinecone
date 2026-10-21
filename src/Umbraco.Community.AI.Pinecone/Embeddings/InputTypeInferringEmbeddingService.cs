using Microsoft.Extensions.AI;
using Umbraco.AI.Core.Embeddings;

namespace Umbraco.Community.AI.Pinecone.Embeddings;

/// <summary>
/// Wraps Umbraco AI's embedding service to tag each call as a query or a passage.
/// </summary>
/// <remarks>
/// <para>
/// Umbraco AI Search embeds content in batches (<see cref="GenerateEmbeddingsAsync(Action{AIEmbeddingBuilder}, IEnumerable{string}, CancellationToken)"/>)
/// and search queries one at a time (<see cref="GenerateEmbeddingAsync(Action{AIEmbeddingBuilder}, string, CancellationToken)"/>),
/// without saying which is which. This uses that shape: a single value is a query, several are passages.
/// For other callers it is a guess, so an explicit <c>input_type</c> always wins (see <see cref="PineconeInputType"/>).
/// </para>
/// <para>
/// The hint is set before the caller's own configuration runs, so a caller that sets its own
/// <see cref="EmbeddingGenerationOptions"/> replaces it. Other providers ignore the namespaced key.
/// </para>
/// </remarks>
internal sealed class InputTypeInferringEmbeddingService(IAIEmbeddingService inner) : IAIEmbeddingService
{
    internal IAIEmbeddingService Inner => inner;

    public Task<Embedding<float>> GenerateEmbeddingAsync(
        Action<AIEmbeddingBuilder> configure,
        string value,
        CancellationToken cancellationToken = default)
        => inner.GenerateEmbeddingAsync(WithHint(configure, PineconeConstants.InputTypes.Query), value, cancellationToken);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateEmbeddingsAsync(
        Action<AIEmbeddingBuilder> configure,
        IEnumerable<string> values,
        CancellationToken cancellationToken = default)
        => inner.GenerateEmbeddingsAsync(WithHint(configure, PineconeConstants.InputTypes.Passage), values, cancellationToken);

    // A reusable generator can be used for either, so it is left alone.
    public Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(
        Action<AIEmbeddingBuilder> configure,
        CancellationToken cancellationToken = default)
        => inner.CreateEmbeddingGeneratorAsync(configure, cancellationToken);

#pragma warning disable CS0618 // Obsolete members - passing the deprecated interface methods through

    public Task<Embedding<float>> GenerateEmbeddingAsync(
        string value,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
        => inner.GenerateEmbeddingAsync(value, WithHint(options, PineconeConstants.InputTypes.Query), cancellationToken);

    public Task<Embedding<float>> GenerateEmbeddingAsync(
        Guid profileId,
        string value,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
        => inner.GenerateEmbeddingAsync(profileId, value, WithHint(options, PineconeConstants.InputTypes.Query), cancellationToken);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateEmbeddingsAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
        => inner.GenerateEmbeddingsAsync(values, WithHint(options, PineconeConstants.InputTypes.Passage), cancellationToken);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateEmbeddingsAsync(
        Guid profileId,
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
        => inner.GenerateEmbeddingsAsync(profileId, values, WithHint(options, PineconeConstants.InputTypes.Passage), cancellationToken);

    public Task<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingGeneratorAsync(
        Guid? profileId = null,
        CancellationToken cancellationToken = default)
        => inner.GetEmbeddingGeneratorAsync(profileId, cancellationToken);

#pragma warning restore CS0618

    private static Action<AIEmbeddingBuilder> WithHint(Action<AIEmbeddingBuilder> configure, string inputType)
    {
        ArgumentNullException.ThrowIfNull(configure);

        return builder =>
        {
            builder.WithEmbeddingOptions(WithHint(options: null, inputType));
            configure(builder);
        };
    }

    private static EmbeddingGenerationOptions WithHint(EmbeddingGenerationOptions? options, string inputType)
    {
        // Copy so the caller's options are never mutated, and keep anything they already set.
        EmbeddingGenerationOptions hinted = options?.Clone() ?? new EmbeddingGenerationOptions();
        hinted.AdditionalProperties = hinted.AdditionalProperties?.Clone() ?? [];

        if (!hinted.AdditionalProperties.ContainsKey(PineconeConstants.InputTypeOptionKey))
        {
            hinted.AdditionalProperties[PineconeConstants.InputTypeOptionKey] = inputType;
        }

        return hinted;
    }
}
