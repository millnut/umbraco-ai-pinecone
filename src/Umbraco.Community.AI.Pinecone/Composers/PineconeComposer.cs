using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.AI.Core.Embeddings;
using Umbraco.AI.Startup.Configuration;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.AI.Pinecone.Embeddings;
using Umbraco.Community.AI.Pinecone.Options;

namespace Umbraco.Community.AI.Pinecone.Composers;

/// <summary>
/// Registers the services the Pinecone provider needs. The provider itself is discovered by Umbraco AI.
/// </summary>
[ComposeAfter(typeof(UmbracoAIComposer))]
public sealed class PineconeComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        IConfigurationSection section = builder.Config.GetSection(PineconeOptions.SectionName);
        builder.Services.AddOptions<PineconeOptions>().Bind(section);

        builder.Services.AddHttpClient(PineconeConstants.HttpClientName);

        // Read once here rather than through IOptions because it decides what gets registered at all.
        if ((section.Get<PineconeOptions>() ?? new PineconeOptions()).InferInputType)
        {
            DecorateEmbeddingService(builder.Services);
        }
    }

    /// <summary>
    /// Wraps the registered <see cref="IAIEmbeddingService"/> in <see cref="InputTypeInferringEmbeddingService"/>,
    /// keeping its lifetime. Runs after Umbraco AI's composer, so its registration is already there.
    /// </summary>
    internal static void DecorateEmbeddingService(IServiceCollection services)
    {
        ServiceDescriptor? descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IAIEmbeddingService) && !d.IsKeyedService);
        if (descriptor is null)
        {
            return;
        }

        services.Remove(descriptor);
        services.Add(ServiceDescriptor.Describe(
            typeof(IAIEmbeddingService),
            sp => new InputTypeInferringEmbeddingService(CreateInner(sp, descriptor)),
            descriptor.Lifetime));
    }

    private static IAIEmbeddingService CreateInner(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
        => (IAIEmbeddingService)(descriptor.ImplementationInstance
            ?? descriptor.ImplementationFactory?.Invoke(serviceProvider)
            ?? ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!));
}
