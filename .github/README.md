# Pinecone Provider for Umbraco AI

[![Downloads](https://img.shields.io/nuget/dt/Umbraco.Community.AI.Pinecone?color=cc9900)](https://www.nuget.org/packages/Umbraco.Community.AI.Pinecone/)
[![NuGet](https://img.shields.io/nuget/vpre/Umbraco.Community.AI.Pinecone?color=0273B3)](https://www.nuget.org/packages/Umbraco.Community.AI.Pinecone)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](../LICENSE)

An [Umbraco AI](https://github.com/umbraco/Umbraco.AI) provider that generates embeddings with [Pinecone's hosted models](https://docs.pinecone.io/guides/index-data/index-configuration#embedding-models), for [Umbraco AI Search](https://www.nuget.org/packages/Umbraco.AI.Search) or anything else in Umbraco AI that needs embeddings.

This is a community package. It is not made or supported by Umbraco HQ or Pinecone.

Requires Umbraco 18 (.NET 10) and Umbraco AI 18.6 or later.

## Install

```bash
dotnet add package Umbraco.Community.AI.Pinecone
```

## Set up

1. Put your Pinecone API key in configuration: an appsettings file you don't commit to source control, or the environment variable `Umbraco__AI__Secrets__PineconeApiKey`:

   ```json
   {
     "Umbraco": {
       "AI": {
         "Secrets": {
           "PineconeApiKey": "your-pinecone-api-key"
         }
       }
     }
   }
   ```

2. In the backoffice, add an AI connection with the **Pinecone** provider. For the API key, enter `$Umbraco:AI:Secrets:PineconeApiKey` so the key stays out of the database. Use **Test** to check it.
3. Add an embedding profile on that connection and make it the default embedding profile. Umbraco AI Search always uses the default.

The default model is `llama-text-embed-v2`, with 1024 dimensions; you can set 384, 512, 768 or 2048 instead. `multilingual-e5-large` is also available, at 1024. After changing model or dimensions, rebuild your search index.

## Configuration

| Setting | Where | Meaning |
|---|---|---|
| API key | Connection | Required. The key, or a `$Umbraco:AI:Secrets:...` reference to it. |
| Truncate | Connection | What happens to text that's too long: `END` (default) cuts the end, `START` cuts the start (llama only), `NONE` rejects it. |
| `Umbraco:AI:Pinecone:InferInputType` | appsettings | `true` (default) tags requests as query or passage. See below. |

## Queries and passages

Pinecone embeds search queries and indexed content differently, and needs to be told which each request is. Umbraco AI doesn't say, so this package infers it: one value is a query and several values are passages. That matches how Umbraco AI Search works.

If your own code embeds content one item at a time, set it yourself with `EmbeddingGenerationOptions.AdditionalProperties["input_type"] = "passage"` (or `"query"`). An explicit value always wins.

## Data processing

Whatever Umbraco AI asks this provider to embed is sent to Pinecone's API. With Umbraco AI Search, that's the text of published content and media (including protected pages) and every search query. If any of it can contain personal data, Pinecone is a data processor for your site, so cover it in your data processing agreement and privacy notice. The package stores nothing itself and never logs the text it sends.

## Known limitations

- Umbraco AI Search chunks content by word count (512 words by default). That's fine for `llama-text-embed-v2`, but over `multilingual-e5-large`'s 507-token limit, so with e5 set `Umbraco:AI:Search:ChunkSize` to about 350.
- Only dense embedding models are supported.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
