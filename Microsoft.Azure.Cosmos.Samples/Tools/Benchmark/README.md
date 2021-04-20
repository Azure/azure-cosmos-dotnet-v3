# Microsoft Azure Cosmos DB .NET Benchmark tool

### Setup
1. Create 1 Cosmos database named 'testdb' without shared throughput. Make sure the account is not geo-replicated as this will impact the performance. 
2. Create a container named 'testcol' 100,000 RUs
3. Create a container named 'runsummary' 5,000 RUs. This is where the results will be sent.
4. Allocate a VM in the same region as the Cosmos account. Recommend Standard F4s_v2 (4 vcpus, 8 GiB memory). Anything larger might result in request being throttled.
5. Clone the github repo to the VM. 
6. Follow the [Running on linux](linux) or [Running on Windows](windows) sections below.
7. The concurrency changed to ensure the request P99 latency is completing is under 10ms to match SLA requirements.
8. After the run the results will be published to the runsummary container. 
9. It's recommended to run the project at least 3 times per a commit to average the numbers to get a solid baseline to compare commits.

## Sample tool usage
```
dotnet run CosmosBenchmark.csproj -e {ACCOUNT_ENDPOINT} -k {ACCOUNT_KEY}
```

Dry tun targeting emulator will look like below
```
dotnet run CosmosBenchmark.csproj -e "https://localhost:8081" -k "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
```

To target Microsoft.Azure.Cosmos\src\Microsoft.Azure.Cosmos.csproj
```
export OSSProjectRef=True
dotnet run CosmosBenchmark.csproj -e {ACCOUNT_ENDPOINT} -k {ACCOUNT_KEY}
```

## Running on Linux <a name="linux"></a>
For PerfRuns with reports (INTERNAL)
```
export OSSProjectRef=True
export ACCOUNT_ENDPOINT=<ENDPOINT
export ACCOUNT_KEY=<KEY>
export RESULTS_PK="runs-summary" #For test runs use different one
export PL=18

dotnet run -c Release  -- -e $ACCOUNT_ENDPOINT -k $ACCOUNT_KEY --tcp 10 --enablelatencypercentiles --disablecoresdklogging --publishresults --resultspartitionkeyvalue $RESULTS_PK --commitid $(git log -1 | head -n 1 | cut -d ' ' -f 2) --commitdate $(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 1 -d ' ') --committime $(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 2 -d ' ') --branchname $(git rev-parse --abbrev-ref HEAD)  --database testdb --container testcol --partitionkeypath /pk -n 2000000 -w ReadStreamExistsV3 --pl $PL 
```

![image](https://user-images.githubusercontent.com/6880899/61565403-8e41bd00-aa96-11e9-9996-b7fc77c3aed3.png)

## Running on Windows <a name="windows"></a>

Powershell version to build using project reference
```
$log = git log -1
$commit = $log.Split([Environment]::NewLine)[0].Substring(7)

$branch = git branch --show-current

 dotnet build --configuration Release -p:"OSSProjectRef=true;ShouldUnsetParentConfigurationAndPlatform=false"
 
$commit
$branch

while($true){
  dotnet run  -c Release --no-build  -- -e {addAccountEndpoint} -k {addAccountKey} --publishresults --disablecoresdklogging --resultspartitionkeyvalue headerTest --branchname $branch  --database testdb --container testcol --partitionkeypath /pk -n 2000000 -w ReadStreamExistsV3 --pl 18 --commitid $commit --commitdate '{commit date like: Wed Aug 12 2020}' --committime  '{commit time like: 05:49:38}' --enablelatencypercentiles
  $branch
}
```

## Usage
```
>dotnet run CosmosBenchmark.csproj
CosmosBenchmark 1.0.0
Copyright (C) 2019 CosmosBenchmark

  -e                     Required. Cosmos account end point

  -k                     Required. Cosmos account master key

  --database             Database to use

  --container            Collection to use

  -t                     Collection throughput use

  -n                     Number of documents to insert

  --cleanuponstart       Start with new collection

  --cleanuponfinish      Clean-up after run

  --partitionkeypath     Container partition key path

  --pl                   Degree of parallism

  --itemtemplatefile     Item template

  --minthreadpoolsize    Min thread pool size

  --help                 Display this help screen.

  --version              Display version information.
```

## Running on Azure

If you quickly get results, you can use our [guide to leverage Azure Container Instances](./AzureContainerInstances/README.md) to execute the benchmarks in any number of Azure regions with very little setup required.