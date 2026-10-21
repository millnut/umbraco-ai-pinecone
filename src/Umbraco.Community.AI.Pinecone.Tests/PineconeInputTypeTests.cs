using Microsoft.Extensions.AI;

namespace Umbraco.Community.AI.Pinecone.Tests;

public class PineconeInputTypeTests
{
    [Fact]
    public void Passage_when_nothing_says_otherwise()
    {
        Assert.Equal("passage", PineconeInputType.Resolve(null));
        Assert.Equal("passage", PineconeInputType.Resolve(new EmbeddingGenerationOptions()));
    }

    [Theory]
    [InlineData("query", "query")]
    [InlineData("passage", "passage")]
    [InlineData("QUERY", "query")]
    [InlineData("search_query", "query")]
    [InlineData("search_document", "passage")]
    [InlineData("document", "passage")]
    public void Explicit_input_type_is_used(string value, string expected)
    {
        Assert.Equal(expected, PineconeInputType.Resolve(With(("input_type", value))));
    }

    [Fact]
    public void Inferred_input_type_is_used_when_there_is_no_explicit_one()
    {
        Assert.Equal("query", PineconeInputType.Resolve(With((PineconeConstants.InputTypeOptionKey, "query"))));
    }

    [Fact]
    public void Explicit_input_type_beats_the_inferred_one()
    {
        var options = With(("input_type", "passage"), (PineconeConstants.InputTypeOptionKey, "query"));

        Assert.Equal("passage", PineconeInputType.Resolve(options));
    }

    [Fact]
    public void Values_Pinecone_does_not_know_are_skipped()
    {
        var options = With(("input_type", "classification"), (PineconeConstants.InputTypeOptionKey, "query"));

        Assert.Equal("query", PineconeInputType.Resolve(options));
        Assert.Equal("passage", PineconeInputType.Resolve(With(("input_type", 42))));
    }

    private static EmbeddingGenerationOptions With(params (string Key, object Value)[] properties)
    {
        var additional = new AdditionalPropertiesDictionary();
        foreach (var (key, value) in properties)
        {
            additional[key] = value;
        }

        return new EmbeddingGenerationOptions { AdditionalProperties = additional };
    }
}
