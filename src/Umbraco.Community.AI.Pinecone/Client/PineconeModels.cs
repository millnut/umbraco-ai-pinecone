namespace Umbraco.Community.AI.Pinecone.Client;

// Wire models for the parts of Pinecone's inference API the provider uses. The API is snake_case, which the
// client's naming policy handles, so they need no attributes. Parameter keys such as input_type are dictionary
// keys and are sent as written.

internal sealed record EmbedRequest(string Model, IDictionary<string, object?> Parameters, IReadOnlyList<EmbedInput> Inputs);

internal sealed record EmbedInput(string Text);

internal sealed record EmbedResponse(string? Model, string? VectorType, List<EmbeddingData>? Data, EmbedUsage? Usage);

/// <summary>
/// One embedding. Sparse models send <c>sparse_values</c> instead, which leaves <see cref="Values"/> null.
/// </summary>
internal sealed record EmbeddingData(string? VectorType, float[]? Values);

internal sealed record EmbedUsage(int? TotalTokens);

internal sealed record ModelListResponse(List<ModelInfo>? Models);

internal sealed record ModelInfo(string Model, string? Type, string? VectorType);
