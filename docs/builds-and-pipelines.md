# Build pipelines for the Azure Cosmos DB .NET SDK

This repository contains 3 pipelines that are used on different scenarios.

## PR Validation

[azure-pipelines.yml](../azure-pipelines.yml) defines the checks that are performed during a PR validation, it covers:

* [Static analysis](../templates/static-tools.yml)
* [Verifying if the state of the SDK package is valid / can we generate a Nuget package](../templates/nuget-pack.yml)
* [Verify if the CTL runner builds](../templates/build-ctl.yml) -> [CTL Runner source](../Microsoft.Azure.Cosmos.Samples/Tools/CTL).
* [Verify if the Samples build](../templates/build-samples.yml) -> [Samples folder source](../Microsoft.Azure.Cosmos.Samples/Usage).
* [Run the Unit and Emulator tests](../templates/build-test.yml) -> For more information about tests, see the [CONTRIBUTING guide](../CONTRIBUTING.md#tests).
* [Verify the project builds with INTERNAL flag](../templates/build-internal.yml) -> INTERNAL is used for service dogfooding and for friends assembly access.
* [Verify the project builds with the PREVIEW flag, Unit tests for PREVIEW pass, Encryption and Benchmark projects for PREVIEW build](../templates/build-preview.yml) -> PREVIEW is used to ship the `-preview` SDK package.
* [Verify the Benchmark project builds, including PREVIEW flag build](../templates/build-benchmark.yml) -> [Benchmark project](../Microsoft.Azure.Cosmos.Samples/Tools/Benchmark/README.md) enables users to execute benchmark runs on their accounts.

This pipeline executes on Azure Pipelines as [dotnet-v3-ci](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=63).

## SDK release

[azure-pipelines-official.yml](../azure-pipelines-official.yml) is used during the release process of a new version:

* [Static analysis](../templates/static-tools.yml)
* [Run the Unit and Emulator tests](../templates/build-test.yml) -> For more information about tests, see the [CONTRIBUTING guide](../CONTRIBUTING.md#tests).
* [Generate a Nuget package, and a Symbols package, and publish it on the `cosmosdb/csharp/<version>/` storage container](../templates/nuget-pack.yml)

This pipeline executes on Azure Pipelines as [dotnet-v3-release](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=65).

## Nightly release

[azure-pipelines-nightly.yml](../azure-pipelines-nightly.yml) is a scheduled run that executes every day at 0:00 UTC and produces two Nuget packages with the content on the `master` branch:

* A non-preview package with versioning `Microsoft.Azure.Cosmos.X.Y.Z-nightly-DATE` where `X.Y.Z` is the current version from [Directory.Build.Props](../Directory.Build.props) and `DATE` is the current date in `MMDDYYYY` format.
* A preview package with versioning `Microsoft.Azure.Cosmos.X.Y.Z-preview-nightly-DATE` where `X.Y.Z` is the current version from [Directory.Build.Props](../Directory.Build.props) and `DATE` is the current date in `MMDDYYYY` format.

The pipeline will:

* [Generate a nightly Nuget package and publish it on the `cosmosdb/csharp/nightly` storage container and delete previous contents](../templates/nuget-pack.yml)
* [Generate a preview nightly Nuget package and publish it on the `cosmosdb/csharp/nightly-preview` storage container and delete previous contents](../templates/nuget-pack.yml)

This pipeline executes on Azure Pipelines as [dotnet-v3-nightly](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=75).

## Docker image for CTL workloads

[azure-pipelines-ctl-publishing.yml](../azure-pipelines-ctl-publishing.yml) executes every time a change is merged into `master` and it will:

* Generate [docker config files](../Microsoft.Azure.Cosmos.Samples/Tools/CTL/Dockerfile).
* Copy the [executable shell file](../Microsoft.Azure.Cosmos.Samples/Tools/CTL/run_ctl.sh).
* Build and publish as a binary [the CTL project](../Microsoft.Azure.Cosmos.Samples/Tools/CTL/CosmosCTL.csproj).
* Execute docker build and publish the image to the team's Azure Container Instances container.

This pipeline executes on Azure Pipelines as [dotnet-v3-ctl-image-publish](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=64).

## Encryption packages release

This repository also includes the [Microsoft.Azure.Cosmos.Encryption](../Microsoft.Azure.Cosmos.Encryption/) and [Microsoft.Azure.Cosmos.Encryption.Custom](../Microsoft.Azure.Cosmos.Encryption.Custom/) projects as satellite packages for client side encryption.

[azure-pipelines-encryption](../azure-pipelines-encryption.yml) is used during the release process for a new `Microsoft.Azure.Cosmos.Encryption` release:

* Builds the [Microsoft.Azure.Cosmos.Encryption](../Microsoft.Azure.Cosmos.Encryption/src/Microsoft.Azure.Cosmos.Encryption.csproj)
* Generate a Nuget package and a Symbols package
* Publish the package on the `cosmosdb/csharp/encryption/<version>` storage container.

This pipeline executes on Azure Pipelines as [dotnet-v3-encryption-release](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=66).

[azure-pipelines-encryption-custom](../azure-pipelines-encryption-custom.yml) is used during the release process for a new `Microsoft.Azure.Cosmos.Encryption.Custom` release:

* Builds the [Microsoft.Azure.Cosmos.Encryption.Custom](../Microsoft.Azure.Cosmos.Encryption.Custom/src/Microsoft.Azure.Cosmos.Encryption.Custom.csproj)
* Generate a Nuget package and a Symbols package
* Publish the package on the `cosmosdb/csharp/encryption.custom/<version>` storage container.

This pipeline executes on Azure Pipelines as [dotnet-v3-encryption-custom-release](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=67).
