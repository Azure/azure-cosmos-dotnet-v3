## Changes in [3.1.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.0.0) : ##

* Fixes [#544](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/544) Adding continuation token support for LINQ


## Changes in [3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/3.0.0) : ##

* General availability of [Version 3.0.0](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/) of the .NET SDK
* Targets .NET Standard 2.0, which supports .NET framework 4.6.1+ and .NET Core 2.0+
* New object model, with top-level CosmosClient and methods split across relevant Database and Container classes
* New highly performant stream APIs
* Built-in support for Change Feed processor APIs
* Fluent builder APIs for CosmosClient, Container, and Change Feed processor
* Idiomatic throughput management APIs
* Granular RequestOptions and ResponseTypes for database, container, item, query and throughput requests
* Ability to scale non-partitioned containers 
* Extensible and customizable serializer
* Extensible request pipeline with support for custom handlers
