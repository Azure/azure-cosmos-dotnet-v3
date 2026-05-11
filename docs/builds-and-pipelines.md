# Build pipelines for the Azure Cosmos DB .NET SDK

This repository contains 7 pipelines that are used on different scenarios.

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

This pipeline does **not** run live-account tests (`MultiRegion`, `MultiMaster`) because those require secret variables that Azure DevOps refuses to mount on builds triggered by pull requests from forks. The live-account jobs were intentionally extracted from [`templates/build-test.yml`](../templates/build-test.yml) into [`templates/build-test-live.yml`](../templates/build-test-live.yml) and are surfaced through the [Live account validation](#live-account-validation) pipeline described below.

This pipeline executes on Azure Pipelines as [dotnet-v3-ci](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=63).

## Live account validation

[azure-pipelines-live-account.yml](../azure-pipelines-live-account.yml) runs the live-account EmulatorTests categories &mdash; `MultiRegion` and `MultiMaster` &mdash; via [`templates/build-test-live.yml`](../templates/build-test-live.yml). These jobs depend on the `$(COSMOSDB_MULTI_REGION)` and `$(COSMOSDB_MULTIMASTER)` secret variables.

This pipeline is configured (in the Azure DevOps UI) so that:

* It auto-runs on merges into `main` and `releases/*`.
* It does **not** auto-run on pull requests from forks. A team member must comment `/azp run dotnet-v3-live-account` on the PR to dispatch a run against the PR's commit. This is the same "team-member approval" pattern that `azure-sdk-for-rust`, `azure-sdk-for-java`, and `azure-sdk-for-python` use for live-test stages.
* It is listed in GitHub branch protection on `main` as a required status check, so no PR can merge without it having completed successfully on the head commit.

This pipeline is expected to execute on Azure Pipelines as `dotnet-v3-live-account`. See [docs/builds-and-pipelines.md#manual-ado-setup](#manual-ado-setup) for the one-time ADO configuration required.

## SDK release

[azure-pipelines-official.yml](../azure-pipelines-official.yml) is used during the release process of a new version:

* [Static analysis](../templates/static-tools.yml)
* [Run the Unit and Emulator tests](../templates/build-test.yml) -> For more information about tests, see the [CONTRIBUTING guide](../CONTRIBUTING.md#tests).
* [Generate a Nuget package, and a Symbols package, and publish it on the `cosmosdb/csharp/<version>` storage container](../templates/nuget-pack.yml) the Nuget version will be what is defined on [Directory.Build.Props](../Directory.Build.props). Template parameters: ReleasePackage = true, CleanupFolder = false, BlobVersion = `<version>`

This pipeline executes on Azure Pipelines as [dotnet-v3-release](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=65).

## Nightly release

[azure-pipelines-nightly.yml](../azure-pipelines-nightly.yml) is a scheduled run that executes every day at 0:00 UTC and produces two Nuget packages with the content on the `main` branch:

* A non-preview package with versioning `Microsoft.Azure.Cosmos.X.Y.Z-nightly-DATE` where `X.Y.Z` is the current version from [Directory.Build.Props](../Directory.Build.props) and `DATE` is the current date in `MMDDYYYY` format.
* A preview package with versioning `Microsoft.Azure.Cosmos.X.Y.Z-preview-nightly-DATE` where `X.Y.Z` is the current version from [Directory.Build.Props](../Directory.Build.props) and `DATE` is the current date in `MMDDYYYY` format.

The pipeline will:

* [Generate a nightly Nuget package and publish it on the `cosmosdb/csharp/nightly` storage container and delete previous contents](../templates/nuget-pack.yml). Template parameters: ReleasePackage = true, CleanupFolder = true, BlobVersion = nightly.
* [Generate a preview nightly Nuget package and publish it on the `cosmosdb/csharp/nightly-preview` storage container and delete previous contents](../templates/nuget-pack.yml). Template parameters: ReleasePackage = true, CleanupFolder = true, BlobVersion = nightly-preview.

This pipeline executes on Azure Pipelines as [dotnet-v3-nightly](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build?definitionId=75).

## Docker image for CTL workloads

[azure-pipelines-ctl-publishing.yml](../azure-pipelines-ctl-publishing.yml) executes every time a change is merged into `main` and it will:

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

## Manual ADO setup

The [Live account validation](#live-account-validation) pipeline requires one-time configuration in Azure DevOps and GitHub that is not expressible in YAML. These steps are performed by a repository administrator the first time `azure-pipelines-live-account.yml` lands on `main`:

1. **Create the pipeline definition in Azure DevOps.**
   * In the `cosmos-db-sdk-public` ADO project, create a new pipeline named `dotnet-v3-live-account` that points at `azure-pipelines-live-account.yml` in this repository.
2. **Define the secret variables on that pipeline definition.**
   * `COSMOSDB_MULTI_REGION` &mdash; connection string for the shared multi-region test Cosmos DB account, marked as secret.
   * `COSMOSDB_MULTIMASTER` &mdash; connection string for the shared multi-master test Cosmos DB account, marked as secret.
3. **Configure the pipeline's "Pull request validation" settings (Triggers tab):**
   * `Build pull requests from forks of this repository`: **ON**
   * `Make secrets available to builds of forks`: **ON**
   * `Require a team member's comment before building a pull request`: **ON** (this is what makes `/azp run` the required gate for fork PRs).
4. **Add the new pipeline as a required check in GitHub branch protection.**
   * Go to *Settings -> Branches -> Branch protection rule for `main`* in GitHub.
   * Add `dotnet-v3-live-account` to the list of required status checks.
5. **(Recommended) Verify the symmetric configuration on `dotnet-v3-ci`** (the public PR pipeline) just so the contract is explicit:
   * `Build pull requests from forks of this repository`: **ON**
   * `Make secrets available to builds of forks`: **OFF**
   * `Require a team member's comment before building a pull request`: **OFF**
6. **Smoke test.** Open a throwaway fork PR and verify that:
   * `dotnet-v3-ci` runs automatically and passes.
   * `dotnet-v3-live-account` does **not** run automatically and shows as pending/required.
   * Commenting `/azp run dotnet-v3-live-account` triggers the live pipeline and it succeeds.

These steps are configuration of the Azure DevOps and GitHub services themselves and cannot be committed to the repository. Review them when this PR is merged.

