# Azure Cosmos DB .NET SDK AI Extension Library

`Microsoft.Azure.Cosmos.AI` is an optional, provider-style extension package for the [Azure Cosmos DB .NET SDK](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) that adds AI-powered capabilities to applications built on Azure Cosmos DB for NoSQL.

> **Status:** This package is in early development. It currently ships as a scaffold only — no public APIs are exposed yet. The first capability (an `AzureOpenAIEmbeddingGenerator` provider for automatic vector embedding generation in hybrid and vector search queries) is tracked in [#5842](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5842). Breaking changes may occur prior to GA.

## Why a Separate Package

- **Zero cost for non-AI workloads.** Applications that don't use AI features pull in no AI dependencies. `Azure.AI.OpenAI` and related dependencies are only present for customers who explicitly opt in to this package.
- **Independent versioning.** This package ships on its own cadence, allowing rapid iteration on AI capabilities without coupling to the core SDK release cycle.
- **Established extensibility model.** Follows the same pattern proven by `Microsoft.Azure.Cosmos.Encryption` — a separate, optional, provider-style package that extends the core SDK without polluting it.
- **Clear separation of concerns.** The core SDK owns the data plane. This package owns AI service integration.

## Planned Capabilities

- **Automatic vector embedding generation** — provide raw text to vector / hybrid search queries; the SDK calls a configured embedding service (e.g., Azure OpenAI) on your behalf.
- **Semantic re-ranking** — re-rank query results using AI services.
- **AI-assisted query construction** — generate Cosmos DB queries from natural language.
- Additional AI-powered features as the landscape evolves.

## Getting Started

Once the first capability lands in a published preview, install from NuGet:

```bash
dotnet add package Microsoft.Azure.Cosmos.AI --prerelease
```

Usage examples will be added here as APIs ship.

## Related Links

- [Azure Cosmos DB documentation](https://learn.microsoft.com/azure/cosmos-db/)
- [Azure Cosmos DB .NET SDK on NuGet](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/)
- [Changelog](./changelog.md)
- [Repository](https://github.com/Azure/azure-cosmos-dotnet-v3)
