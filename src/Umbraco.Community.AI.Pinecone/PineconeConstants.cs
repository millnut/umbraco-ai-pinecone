namespace Umbraco.Community.AI.Pinecone;

/// <summary>
/// Identifiers and limits shared across the Pinecone provider.
/// </summary>
public static class PineconeConstants
{
    /// <summary>
    /// The Umbraco AI provider id, stored on connections and profiles. Changing it orphans existing ones.
    /// </summary>
    public const string ProviderId = "pinecone";

    public const string ProviderName = "Pinecone";

    /// <summary>
    /// Used when a profile has no model set. Its 2,048-token context suits Umbraco AI Search's word-based
    /// chunks, which can exceed the 507-token limit of <c>multilingual-e5-large</c>.
    /// </summary>
    public const string DefaultEmbeddingModel = "llama-text-embed-v2";

    /// <summary>
    /// The most inputs Pinecone accepts in one embed request for its hosted dense models.
    /// </summary>
    public const int MaxBatchSize = 96;

    /// <summary>
    /// Name of the <see cref="System.Net.Http.IHttpClientFactory"/> client used for Pinecone requests.
    /// </summary>
    public const string HttpClientName = "Umbraco.Community.AI.Pinecone";

    /// <summary>
    /// Key in <c>EmbeddingGenerationOptions.AdditionalProperties</c> for the input type this package infers.
    /// Namespaced so it never reaches another provider that reads a plain <c>input_type</c>.
    /// </summary>
    public const string InputTypeOptionKey = "Umbraco.Community.AI.Pinecone.InputType";

    /// <summary>
    /// Values of Pinecone's <c>input_type</c> parameter, which its hosted models require.
    /// </summary>
    public static class InputTypes
    {
        /// <summary>
        /// Content being embedded for storage in an index.
        /// </summary>
        public const string Passage = "passage";

        /// <summary>
        /// A search query being embedded to match against stored passages.
        /// </summary>
        public const string Query = "query";
    }
}
