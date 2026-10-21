# Contributing Guidelines

Contributions to this package are most welcome!

There is no test site in the solution. To try the package in a real site, pack it and install it there (see below).

## Building

```bash
# From the repository root
dotnet build src/Umbraco.Community.AI.Pinecone.slnx
dotnet test --project src/Umbraco.Community.AI.Pinecone.Tests
```

The unit tests fake the Pinecone API. `PineconeIntegrationTests` call the real one and are skipped unless `PINECONE_API_KEY` is set:

```bash
PINECONE_API_KEY=your-key dotnet test --project src/Umbraco.Community.AI.Pinecone.Tests
```

They use a few tokens of the free inference allowance per run.

## Testing in a site

Pack with a version that has never been used before, because NuGet's global cache keeps serving the first build of any version:

```bash
VERSION="0.1.0-local.$(date +%Y%m%d%H%M)"
dotnet pack src/Umbraco.Community.AI.Pinecone/Umbraco.Community.AI.Pinecone.csproj \
  -c Release /p:Version=$VERSION -o /path/to/a/local/nuget/source
```

In the site, add that folder as a NuGet source, reference `Umbraco.Community.AI.Pinecone` at `$VERSION`, and restore. The site also needs `Umbraco.AI` and, to test search, `Umbraco.AI.Search`. Then follow the set-up steps in the README.
