using Umbraco.AI.Core.EditableModels;
using Umbraco.AI.Core.Providers;

namespace Umbraco.Community.AI.Pinecone.Tests.Fakes;

/// <summary>
/// Enough provider infrastructure to construct the real provider: capabilities are created by activation,
/// as Umbraco AI's own provider tests do.
/// </summary>
internal sealed class FakeProviderInfrastructure : IAIProviderInfrastructure
{
    public IAICapabilityFactory CapabilityFactory { get; } = new ActivatorCapabilityFactory();

    public IAIEditableModelSchemaBuilder SchemaBuilder
        => throw new NotSupportedException("Schema building is not exercised by these tests.");

    private sealed class ActivatorCapabilityFactory : IAICapabilityFactory
    {
        public TCapability Create<TCapability>(IAIProvider provider)
            where TCapability : class, IAICapability
            => (TCapability)Activator.CreateInstance(typeof(TCapability), provider)!;
    }
}
