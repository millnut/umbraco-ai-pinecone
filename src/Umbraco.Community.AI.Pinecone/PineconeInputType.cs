using Microsoft.Extensions.AI;

namespace Umbraco.Community.AI.Pinecone;

/// <summary>
/// Works out the <c>input_type</c> for an embed request.
/// </summary>
/// <remarks>
/// Pinecone's models embed queries and passages differently, and they require the caller to say which.
/// Umbraco AI's embedding interface carries no such flag, so it is read from the request options:
/// <list type="number">
///   <item>An explicit <c>input_type</c>, which Umbraco AI's own Cohere provider also reads. Cohere's
///   <c>search_query</c>/<c>search_document</c> and a neutral <c>document</c> are accepted too.</item>
///   <item>The value this package infers (<see cref="PineconeConstants.InputTypeOptionKey"/>).</item>
///   <item>Otherwise <c>passage</c>, as most embedding calls are for indexing.</item>
/// </list>
/// Values that mean nothing to Pinecone are skipped rather than sent, so the request still succeeds.
/// </remarks>
internal static class PineconeInputType
{
    internal const string InputTypeOption = "input_type";

    internal static string Resolve(EmbeddingGenerationOptions? options)
    {
        if (options?.AdditionalProperties is { } properties)
        {
            if (Normalize(properties.TryGetValue(InputTypeOption, out var explicitType) ? explicitType : null) is { } fromCaller)
            {
                return fromCaller;
            }

            if (Normalize(properties.TryGetValue(PineconeConstants.InputTypeOptionKey, out var inferredType) ? inferredType : null) is { } inferred)
            {
                return inferred;
            }
        }

        return PineconeConstants.InputTypes.Passage;
    }

    private static string? Normalize(object? value)
        => (value as string)?.Trim().ToLowerInvariant() switch
        {
            "query" or "search_query" => PineconeConstants.InputTypes.Query,
            "passage" or "search_document" or "document" => PineconeConstants.InputTypes.Passage,
            _ => null,
        };
}
