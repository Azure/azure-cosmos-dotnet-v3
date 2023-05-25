#!/bin/bash

# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

echo "##########VM NAME###########: $DB_BINDING_NAME"
echo "##########VM NAME###########: $VM_NAME"
echo "##########MACHINE_INDEX###########: $MACHINE_INDEX"
echo "##########VM_COUNT###########: $VM_COUNT"

echo "##########BENCHMARKING_TOOLS_BRANCH_NAME###########: $BENCHMARKING_TOOLS_BRANCH_NAME"
echo "##########BENCHMARKING_TOOLS_URL###########: $BENCHMARKING_TOOLS_URL"

#Cloning Test Bench Repo
echo "########## Cloning Test Bench repository ##########"
git clone https://github.com/Azure/azure-cosmos-dotnet-v3.git

# Build Benchmark Project
cd 'azure-cosmos-dotnet-v3/Microsoft.Azure.Cosmos.Samples/Tools/Benchmark'

echo "########## Build benckmark tool ##########"
dotnet build --configuration Release -p:"OSSProjectRef=true;ShouldUnsetParentConfigurationAndPlatform=false"

echo "########## Run benchmark ##########"
dotnet run -c Release --no-build -- -e $COSMOS_URI -k $COSMOS_KEY --tcp 10 --enablelatencypercentiles --disablecoresdklogging --publishresults --resultspartitionkeyvalue "runs-summary" --commitid $(git log -1 | head -n 1 | cut -d ' ' -f 2) --commitdate $(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 1 -d ' ') --committime $(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 2 -d ' ') --branchname $(git rev-parse --abbrev-ref HEAD)  --database testdb --container testcol --partitionkeypath /pk -n 2000 -w ReadStreamExistsV3 --pl 18 
