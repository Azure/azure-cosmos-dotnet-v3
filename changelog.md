# Changelog

Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

- [#814](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/814) Ability to limit to configured endpoint only
- [#822](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/822) GROUP BY query support.

### Fixed

- [#835](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/835) Fixed a bug that caused sortedRanges exceptions

## <a name="3.2.0"/> [3.2.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.2.0) - 2019-09-17

### Added

- [#100](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/100) Configurable Tcp settings to CosmosClientOptions
- [#615](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/615), [#775](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/775)  Added request diagnostics to Response's
- [#622](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/622) Added CRUD and query operations for Users and Permissions which enables [ResourceToken](https://docs.microsoft.com/en-us/azure/cosmos-db/secure-access-to-data#resource-tokens) support
- [#716](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/716) Added camel case serialization on LINQ query generation
- [#729](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/729), [#776](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/776) Added aggregate(CountAsync/SumAsync etc.) extensions for LINQ query
- [#743](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/743) Added WebProxy to CosmosClientOptions

### Fixed

- [#726](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/726) Query iterator HasMoreResults now returns false if an exception is hit
- [#705](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/705) User agent suffix gets truncated
- [#753](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/753) Reason was not being propagated for Conflict exceptions
- [#756](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/756) Change Feed Processor with WithStartTime would execute the delegate the first time with no items.
- [#761](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/761) CosmosClient deadlocks when using a custom Task Scheduler like Orleans (Thanks to jkonecki)
- [#769](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/769) Session Consistency + Gateway mode session-token bug fix: Under few rare non-success cases session token might be in-correct
- [#772](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/772) Fixed Throughput throwing when custom serializer used or offer doesn't exists
- [#785](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/785) Incorrect key to throw CosmosExceptions with HttpStatusCode.Unauthorized status code

## <a name="3.2.0-preview2"/> [3.2.0-preview2](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.2.0-preview2) - 2019-09-10

- [#585](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/585), [#741](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/741) Bulk execution support
- [#427](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/427) Transactional batch support (Item CRUD)

## <a name="3.2.0-preview"/> [3.2.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.2.0-preview) - 2019-08-09

- [#427](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/427) Transactional batch support (Item CRUD)

## <a name="3.1.1"/> [3.1.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.1.1) - 2019-08-12

### Added

- [#650](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/650) CosmosSerializerOptions to customize serialization

### Fixed

- [#612](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/612) Bug fix for ReadFeed with partition-key
- [#614](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/614) Fixed SpatialPath serialization and compatibility with older index versions
- [#619](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/619) Fixed PInvokeStackImbalance exception for .NET Framework 
- [#626](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/626) FeedResponse<T> status code now return OK for success instead of the invalid status code 0 or Accepted 
- [#629](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/629) Fixed CreateContainerIfNotExistsAsync validation to limited to partitionKeyPath only
- [#630](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/630) Fixed User Agent to contain environment and package information

## <a name="3.1.0"/> 3.1.0 - 2019-07-29 - Unlisted

### Added

- [#541](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/541) Added consistency level to client and query options
- [#544](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/544) Added continuation token support for LINQ
- [#557](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/557) Added trigger options to item request options
- [#572](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/572) Added partition key validation on CreateContainerIfNotExistsAsync
- [#581](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/581) Added LINQ to QueryDefinition API
- [#592](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/592) Added CreateIfNotExistsAsync to container builder
- [#597](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/597) Added continuation token property to ResponseMessage
- [#604](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/604) Added LINQ ToStreamIterator extension method

### Fixed

- [#548](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/548) Fixed mis-typed message in CosmosException.ToString();
- [#558](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/558) LocationCache ConcurrentDict lock contention fix
- [#561](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/561) GetItemLinqQueryable now works with null query
- [#567](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/567) Query correctly handles different language cultures
- [#574](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/574) Fixed empty error message if query parsing fails from unexpected exception
- [#576](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/576) Query correctly serializes the input into a stream

## <a name="3.0.0"/> [3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.0.0) - 2019-07-15

- General availability of [Version 3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) of the .NET SDK
- Targets .NET Standard 2.0, which supports .NET framework 4.6.1+ and .NET Core 2.0+
- New object model, with top-level CosmosClient and methods split across relevant Database and Container classes
- New highly performant stream APIs
- Built-in support for Change Feed processor APIs
- Fluent builder APIs for CosmosClient, Container, and Change Feed processor
- Idiomatic throughput management APIs
- Granular RequestOptions and ResponseTypes for database, container, item, query and throughput requests
- Ability to scale non-partitioned containers 
- Extensible and customizable serializer
- Extensible request pipeline with support for custom handlers

## Release & Retirement dates
Microsoft provides notification at least **12 months** in advance of retiring an SDK in order to smooth the transition to a newer/supported version.

New features and functionality and optimizations are only added to the current SDK, as such it is recommended that you always upgrade to the latest SDK version as early as possible. 

Any requests to Azure Cosmos DB using a retired SDK are rejected by the service.

<br/>

| Version | Release Date | Retirement Date |
| --- | --- | --- |
| [3.2.0](#3.2.0) |September 18, 2019 |--- |
| [3.1.1](#3.1.1) |August 12, 2019 |--- |
| [3.1.0](#3.1.0) |July 29, 2019 |--- |
| [3.0.0](#3.0.0) |July 15, 2019 |--- |
