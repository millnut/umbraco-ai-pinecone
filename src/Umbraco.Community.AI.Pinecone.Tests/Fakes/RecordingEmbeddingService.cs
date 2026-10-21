using System.Reflection;
using Microsoft.Extensions.AI;
using Umbraco.AI.Core.Embeddings;

namespace Umbraco.Community.AI.Pinecone.Tests.Fakes;

/// <summary>
/// An <see cref="IAIEmbeddingService"/> that records what reached it instead of embedding anything.
/// </summary>
/// <remarks>
/// Builder calls are run against a real <see cref="AIEmbeddingBuilder"/>, and the options it ends up with
/// are read from its internal <c>EmbeddingOptions</c>. If Umbraco AI renames that, these tests fail, which
/// is the point: the decorator depends on how the builder keeps options.
/// </remarks>
internal sealed class RecordingEmbeddingService : IAIEmbeddingService
{
    private static readonly PropertyInfo EmbeddingOptionsProperty = typeof(AIEmbeddingBuilder)
        .GetProperty("EmbeddingOptions", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("AIEmbeddingBuilder no longer has an internal EmbeddingOptions property.");

    public string? LastCall { get; private set; }

    public EmbeddingGenerationOptions? LastOptions { get; private set; }

    public Action<AIEmbeddingBuilder>? LastConfigure { get; private set; }

    public Task<Embedding<float>> GenerateEmbeddingAsync(Action<AIEmbeddingBuilder> configure, string value, CancellationToken cancellationToken = default)
    {
        Record(nameof(GenerateEmbeddingAsync), configure);
        return Task.FromResult(new Embedding<float>(new float[] { 1 }));
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateEmbeddingsAsync(Action<AIEmbeddingBuilder> configure, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        Record(nameof(GenerateEmbeddingsAsync), configure);
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>());
    }

    public Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(Action<AIEmbeddingBuilder> configure, CancellationToken cancellationToken = default)
    {
        LastCall = nameof(CreateEmbeddingGeneratorAsync);
        LastConfigure = configure;
        return Task.FromException<IEmbeddingGenerator<string, Embedding<float>>>(new NotSupportedException());
    }

    public Task<Embedding<float>> GenerateEmbeddingAsync(string value, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        Record("Legacy" + nameof(GenerateEmbeddingAsync), options);
        return Task.FromResult(new Embedding<float>(new float[] { 1 }));
    }

    public Task<Embedding<float>> GenerateEmbeddingAsync(Guid profileId, string value, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        Record("LegacyProfile" + nameof(GenerateEmbeddingAsync), options);
        return Task.FromResult(new Embedding<float>(new float[] { 1 }));
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateEmbeddingsAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        Record("Legacy" + nameof(GenerateEmbeddingsAsync), options);
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>());
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateEmbeddingsAsync(Guid profileId, IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        Record("LegacyProfile" + nameof(GenerateEmbeddingsAsync), options);
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>());
    }

    public Task<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingGeneratorAsync(Guid? profileId = null, CancellationToken cancellationToken = default)
    {
        LastCall = nameof(GetEmbeddingGeneratorAsync);
        return Task.FromException<IEmbeddingGenerator<string, Embedding<float>>>(new NotSupportedException());
    }

    private void Record(string call, Action<AIEmbeddingBuilder> configure)
    {
        var builder = new AIEmbeddingBuilder();
        configure(builder);
        Record(call, (EmbeddingGenerationOptions?)EmbeddingOptionsProperty.GetValue(builder));
        LastConfigure = configure;
    }

    private void Record(string call, EmbeddingGenerationOptions? options)
    {
        LastCall = call;
        LastOptions = options;
    }
}
