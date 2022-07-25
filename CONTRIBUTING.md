# Contributing

## Prerequisites

- Install **.NET 6.0 SDK** for your specific platform. (or a higher version within the 6.0.*** band)  (https://dotnet.microsoft.com/download/dotnet-core/6.0)
- Install the latest version of git (https://git-scm.com/downloads)
- Install the [Azure Cosmos DB Emulator](https://docs.microsoft.com/azure/cosmos-db/local-emulator#download-the-emulator)

You can choose to use any IDE compatible with .NET development, such as:

- [Visual Studio](https://visualstudio.microsoft.com/downloads/) - any of the versions, including the free Community version.
- [Visual Studio Code](https://code.visualstudio.com/download).

## General guidance

Azure Cosmos DB SDKs are thick constructs that contain several layers:

- Public APIs
- Retry policies
- Processing pipeline
- Transport (HTTP or Direct). More information see the [connectivity modes documentation](https://docs.microsoft.com/azure/cosmos-db/sql/sql-sdk-connection-modes#direct-mode)

Make sure you are familiar with:

- [Azure SDK Guidelines](https://azure.github.io/azure-sdk/dotnet_introduction.html)
- [Best practices for Azure Cosmos DB .NET SDK](https://docs.microsoft.com/azure/cosmos-db/sql/best-practice-dotnet)
- [Designing resilient applications with Azure Cosmos DB SDKs](https://docs.microsoft.com/azure/cosmos-db/sql/conceptual-resilient-sdk-applications)

The following image shows the hierarchy of different entities in an Azure Cosmos account:

![Azure Cosmos DB resource model](https://docs.microsoft.com/azure/cosmos-db/media/databases-containers-items/cosmos-entities.png)

### CosmosClient

`CosmosClient` is the client:

- Working with Azure Cosmos databases. They include creating and listing through the `Database` type.
- Obtaining the Azure Cosmos account information.

### Database

A database is the unit of management for a set of Azure Cosmos containers. It maps to the `Database` class and supports:

- Working with Azure Cosmos containers. They include creating, modifying, deleting, and listing through the `Container` type.
- Working with Azure Cosmos users. Users define access scope and permissions. They include creating, modifying, deleting, and listing through the `User` type.

### Containers

An Azure Cosmos container is the unit of scalability both for provisioned throughput and storage. A container is horizontally partitioned and then replicated across multiple regions. It maps to the `Container` class and supports:

- Working with items. Items are the conceptually the user's data. They include creating, modifying, deleting, and listing (including query) items.
- Working with scripts. Scripts are defined as Stored Procedures, User Defined Functions, and Triggers.

For more details visit [here][https://docs.microsoft.com/azure/cosmos-db/databases-containers-items].

## Dependencies

The SDK currently depends on:

- `Microsoft.Azure.Cosmos.Direct` - This package contains the Direct transport protocol and ServiceInterop DLLs.
- `Microsoft.HybridRow` - This package contains the HybridRow protocol used to transmit data for Batch requests.

## Folder structure

- `Microsoft.Azure.Cosmos` contains the SDK and its tests:
  - `Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests` contains the **Unit tests** project.
  - `Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests` contains the **Emulator/Integration tests** project.
  - `Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Performance.Tests` contains the **micro benchmark tests** project.
  - `Microsoft.Azure.Cosmos/src` contains the SDK source code and the `Microsoft.Azure.Cosmos.csproj` project.
- `Microsoft.Azure.Cosmos.Samples` contains samples and tools:
  - `Microsoft.Azure.Cosmos.Samples/Usage` contains sample applications for multiple scenarios. Here is where we keep our public samples for users to consume.
  - `Microsoft.Azure.Cosmos.Samples/Tools` contains tools such as the CTL runner and Benchmark runner.
- `Microsoft.Azure.Cosmos.Encryption` and `Microsoft.Azure.Cosmos.Encryption.Custom` are exclusively related to the Encryption libraries that leverage the SDK.

## Building the SDK

If you are working with Visual Studio, opening the [Microsoft.Azure.Cosmos.sln](Microsoft.Azure.Cosmos.sln) file is the quickest way to build and work with the SDK.

Alternatively, you can build from the command line using the .NET tooling with `dotnet build Microsoft.Azure.Cosmos.sln` on the root of this repository or access the `Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj` project on the [folder structure](#folder-structure) and build just the SDK source code like `dotnet build .\Microsoft.Azure.Cosmos\src\Microsoft.Azure.Cosmos.csproj`.

## Tests

There are two major test projects:

- `Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests` contains Unit tests. Any new feature or work should add unit tests covering unless explicitly allowed due to some exceptional circumstance. Unit tests should be isolated and do not depend on any endpoint or Emulator.
- `Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests` contains Emulator tests. This tests will automatically connect to a running Azure Cosmos DB Emulator (see [prerequisites](#prerequisites)). Any new feature or work should have Emulator tests if the feature is interacting with the service.

All test projects can be interacted with through an IDE (some IDEs like Visual Studio have a Test Explorer to easily navigate through tests) but it can also be executed through the [dotnet test](https://docs.microsoft.com/dotnet/core/tools/dotnet-test) command in any of the above folders.

When evaluating adding new tests, please search in the existing test files if there is already a test file for the scenario or feature you are working on.

## Troubleshooting

- [General .NET SDK Troubleshooting](https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-dot-net-sdk)
- [Timeout troubleshooting](https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-dot-net-sdk-request-timeout?tabs=cpu-new)
- [Service unavailable troubleshooting](https://docs.microsoft.com/azure/cosmos-db/sql/troubleshoot-service-unavailable)
