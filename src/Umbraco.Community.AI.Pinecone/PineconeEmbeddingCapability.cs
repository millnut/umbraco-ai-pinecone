using Microsoft.Extensions.AI;
using Umbraco.AI.Core.Models;
using Umbraco.AI.Core.Providers;

namespace Umbraco.Community.AI.Pinecone;

/// <summary>
/// Embedding capability for the Pinecone provider.
/// </summary>
public class PineconeEmbeddingCapability(PineconeProvider provider)
    : AIEmbeddingCapabilityBase<PineconeProviderSettings>(provider)
{
    /// <summary>
    /// Models known to accept a <c>dimension</c> parameter. An allow-list, so a model released after this
    /// package hides the Dimensions setting rather than offering one it may reject.
    /// </summary>
    private static readonly HashSet<string> ConfigurableDimensionModels =
        new(StringComparer.OrdinalIgnoreCase) { "llama-text-embed-v2" };

    private new PineconeProvider Provider => (PineconeProvider)base.Provider;

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<AIModelDescriptor>> GetModelsAsync(
        PineconeProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> modelIds = await Provider.GetEmbeddingModelIdsAsync(settings, cancellationToken);

        return modelIds
            .Select(id => new AIModelDescriptor(new AIModelRef(Provider.Id, id), id))
            .ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Declaring Dimensions unsupported hides it in the profile editor, and Umbraco AI also strips it from
    /// requests, so a fixed-size model such as <c>multilingual-e5-large</c> never receives one.
    /// </remarks>
    public override AIModelSettingsSupport GetSettingsSupport(string modelId)
        => SupportsDimensions(modelId)
            ? AIModelSettingsSupport.Default
            : new AIModelSettingsSupport
            {
                UnsupportedProfileSettings = [AIProfileSettingKeys.Dimensions],
            };

    /// <inheritdoc />
    protected override IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(
        PineconeProviderSettings settings,
        string? modelId)
        => new PineconeEmbeddingGenerator(
            Provider.CreateClient(settings),
            modelId ?? PineconeConstants.DefaultEmbeddingModel,
            settings.Truncate,
            Logger);

    internal static bool SupportsDimensions(string modelId) => ConfigurableDimensionModels.Contains(modelId);
}
