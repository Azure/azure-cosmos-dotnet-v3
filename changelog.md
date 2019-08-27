# Changelog

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

- [#100](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/100) Configurable Tcp settings to CosmosClientOptions
- [#615](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/615) Adding basic request diagnostics for V3
- [#729](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/729) Adding aggregate(CountAsync/SumAsync etc.) extensions for LINQ query
- [#622](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/622) Added CRUD and query operations for Users and Permissions which enables [ResourceToken](https://docs.microsoft.com/en-us/azure/cosmos-db/secure-access-to-data#resource-tokens) support
- [#716](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/716) Adding camel case serialization on LINQ query generation

### Fixed

- [#726](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/726) Query iterator HasMoreResults now returns false if an exception is hit
- [#705](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/705) User agent suffix gets truncated

## [3.1.1](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.1.1) - 2019-08-12

### Added

- [#650](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/650) CosmosSerializerOptions to customize serialization

### Fixed

- [#612](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/612) Bug fix for ReadFeed with partition-key
- [#614](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/614) Fixed SpatialPath serialization and compatibility with older index versions
- [#619](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/619) Fixed PInvokeStackImbalance exception for .NET Framework 
- [#626](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/626) FeedResponse<T> status code now return OK for success instead of the invalid status code 0 or Accepted 
- [#629](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/629) Fixed CreateContainerIfNotExistsAsync validation to limited to partitionKeyPath only
- [#630](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/630) Fixed User Agent to contain environment and package information

## 3.1.0 - 2019-07-29 - Unlisted

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

## [3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.0.0) - 2019-07-15

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
