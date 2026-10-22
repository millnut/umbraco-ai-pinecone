using System.Net;
using Umbraco.AI.Core.Providers.Errors;
using Umbraco.Community.AI.Pinecone.Client;
using Umbraco.Community.AI.Pinecone.Tests.Fakes;

namespace Umbraco.Community.AI.Pinecone.Tests;

public class PineconeProviderTests
{
    [Fact]
    public void Is_registered_under_the_pinecone_id()
    {
        var provider = TestProvider.Create(new FakePineconeHandler());

        Assert.Equal("pinecone", provider.Id);
        Assert.Equal("Pinecone", provider.Name);
    }

    [Theory]
    [InlineData(401, AIProviderErrorCategory.Authentication)]
    [InlineData(429, AIProviderErrorCategory.RateLimited)]
    [InlineData(400, AIProviderErrorCategory.InvalidRequest)]
    [InlineData(503, AIProviderErrorCategory.Transient)]
    public void Pinecone_errors_are_classified_by_status(int status, AIProviderErrorCategory expected)
    {
        var provider = TestProvider.Create(new FakePineconeHandler());

        var info = provider.ClassifyError(new PineconeException("failed", (HttpStatusCode)status));

        Assert.Equal(expected, info.Category);
    }

    [Fact]
    public void Other_errors_fall_back_to_the_default_classification()
    {
        var provider = TestProvider.Create(new FakePineconeHandler());

        var info = provider.ClassifyError(new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable));

        Assert.Equal(AIProviderErrorCategory.Transient, info.Category);
    }
}
