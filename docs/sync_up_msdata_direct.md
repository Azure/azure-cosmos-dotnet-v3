# Update msdata/direct Repo with Cosmos v3 and Direct Codebase

## Table of Contents

* [Backgraound.](#backgraound)
* [Prerequisites.](#prerequisites)
* [Steps Required to Update msdata direct Repo.](#steps-required-to-update-msdata-direct-repo)
* [Validating the sync-up.](#validating-the-sync-up)
* [Submit Pull Request to msdata direct.](#submit-pull-request-to-msdata-direct)
* [Sample Pull Requests to Sync-up msdata direct.](#sample-pull-requests-to-sync-up-msdata-direct)

## Backgraound

As a developer on the Cosmos SDK team, we often engage in a task, that requires code changes in both cosmos dotnet sdk v3 repository, as well as in the `msdata` cosmosdb direct codebase (aka `Microsoft.Azure.Cosmos.Direct` namespace). Therefore, sometimes it's utter challanging to visualize the code changes as a whole, and analyze the impacts. To overcome this, we have created a branch called `msdata/direct` within our cosmos dotnet sdk v3 codebase, that basically mimics the code present in `msdata` repository mentioned above. This simplifies the code changes required to be done in both the repo and also provides much better understanding on the overall impacts of the code changes.

## Prerequisites

Before covering the sync-up process in detail, please follow the below steps to make sure all the required pre-requisites are met.

### Clone the Azure Cosmos DB .NET SDK Version 3 Repo

- Clone the azure `cosmos-db dotnet sdk` repo in the local environment, using the below git command:
    -       git clone https://github.com/Azure/azure-cosmos-dotnet-v3.git

- Navigate to the directory `azure-cosmos-dotnet-v3` and check out the following branch, `msdata/direct` using the below git commands:
    -       git pull && git checkout msdata/direct

### Clone the `CosmosDB` Repo hosted in msdata

- Clone the CosmosDB in the local environment, using the below git command:
    -       git clone https://msdata.visualstudio.com/DefaultCollection/CosmosDB/_git/CosmosDB

- Navigate to the directory `CosmosDB` and check out the following branch, `master` using the below git commands:
    -       git pull && git checkout master

## Steps Required to Update msdata direct Repo

### Create a Feature Branch for Local Changes.

The first step to sync up the `msdata/direct` repo is to create a feature branch out of it, where all the required changes could be made. Later on, we will use the feature branch to submit pull request to `msdata/direct`. Please use the following git command to create the feature branch:

- Stay on the `msdata/direct` branch and run `git checkout -b users/<user_name>/update_msdata_direct_<mm_dd_yyyy>` to create the feature branch.

### Merging the cosmos db v3 Code into Feature Branch.

The next step is to port the latest `master` branch code into the newly created feature branch. Please see the below git commands to perform this action:

- Stay on the newly created feature branch `users/<user_name>/update_msdata_direct_<mm_dd_yyyy>` and run `git merge master`. Make sure the `master` branch is up-to-date.
- There are likely to be conflicts during the merge. If that happens, we will need to resolve the conflicts gracfully.

### Pick the Required Microsoft Azure Cosmos.Direct files into `msdata/direct` repo.

This is the last part for the sync-up process. Please follow the below steps to copy the required `Microsoft.Cosmos.Direct` files from msdata CosmosDB repo.

- Open command prompt/windows terminal and navigate to the following directory where the cosmos v3 direct code is located, for example: `C:\stash\azure-cosmos-dotnet-v3\Microsoft.Azure.Cosmos\src\direct`.
- Locate and edit the following line in the `msdata_sync.ps1` script with the respective location of the msdata repo: `$baseDir    = "C:\stash\CosmosDB"` 
- Run the powershell script using: `.\msdata_sync.ps1`. You will notice the script started copying the required files from the msdata repo, and generating the console logs, like the below:

    ```
    Copying Files: rntbd2
    Copying Files: AccessCondition.cs
    Copying Files: AccessConditionType.cs
    Copying Files: Address.cs
    Copying Files: AddressCacheToken.cs
    Copying Files: AddressEnumerator.cs
    Copying Files: AddressInformation.cs
    Copying Files: AddressSelector.cs
    Copying Files: ApiType.cs
    Copying Files: Attachment.cs
    Copying Files: AuthorizationTokenType.cs
    Copying Files: BackoffRetryUtility.cs
    Copying Files: BadRequestException.cs
    Copying Files: BarrierRequestHelper.cs
    ```

- Note: There may be instances where some of the files could be missing in the v3 `msdata/direct` repo and the copy may fail with the following error: `Write-Error: SystemSynchronizationScope.cs False`. If that happens, please copy the file manually from the `msdata/CosmosDB` repo and continue running the script all over again. 

## Validating the sync-up

One of the most important part in the whole `msdata/direct` sync up process is to validate whether the code merges, conflict resolutions and file updates went successfully. To comply with this, please make sure to follow the below steps:

- Open command prompt/ windows terminal and navigate to the directory where the cosmos v3 code is located, for instance `C:\stash\azure-cosmos-dotnet-v3`.
- Make sure to stay on the newly created feature branch.
- Stay on the same directory mentioned above, and run the following command for a clean build: `dotnet build`. Make sure, the build passes successfully.

## Submit Pull Request to msdata direct

Once the feature branch builds successfully, it's time to submit the PR to `msdata/direct` to complete the sync-up process. To do this, please follow `git add`, `git commit` and `git push` commands to push the newly created branch upstream. Once the branch is pushed, please submit the pull request to the `msdata/direct` branch and seek for approvals.

## Sample Pull Requests to Sync-up msdata direct

- [[Internal] Msdata/Direct: Refactors msdata branch with latest v3 and direct release](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3726)
- [[Internal] Msdata/Direct: Refactors msdata/direct branch with latest v3 master and Cosmos.Direct v3.30.4](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3776)