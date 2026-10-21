namespace Umbraco.Community.AI.Pinecone.Options;

/// <summary>
/// Settings bound from the <c>Umbraco:AI:Pinecone</c> section of configuration.
/// </summary>
/// <remarks>
/// The API key and model are not here: they belong to the Umbraco AI connection and profile.
/// </remarks>
public sealed class PineconeOptions
{
    public const string SectionName = "Umbraco:AI:Pinecone";

    /// <summary>
    /// When true, embedding calls through Umbraco AI's embedding service are tagged as a query (one value)
    /// or a passage (several values), so Umbraco AI Search gets the right Pinecone <c>input_type</c>.
    /// Turn it off if your own code embeds single passages and you set <c>input_type</c> yourself.
    /// </summary>
    public bool InferInputType { get; set; } = true;
}
