# Changelog

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

## [3.1.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.1.0) - 2019-07-26

### Added

- [#541](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/541) Added consistency level to client and query options
- [#544](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/544) Added continuation token support for LINQ
- [#557](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/557) Added trigger options to item request options
- [#571](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/571) Added a default JSON.net serializer with optional settings
- [#572](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/572) Added partition key validation on CreateContainerIfNotExistsAsync
- [#581](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/581) Adding LINQ to QueryDefinition API
- [#592](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/592) Added CreateIfNotExistsAsync to container builder
- [#597](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/597) Added continuation token property to ResponseMessage

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
