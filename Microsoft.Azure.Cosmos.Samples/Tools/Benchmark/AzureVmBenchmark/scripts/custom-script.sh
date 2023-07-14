#!/bin/bash

# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

#Cloning Test Bench Repo
echo "########## Cloning Test Bench repository ##########"
git clone https://github.com/Azure/azure-cosmos-dotnet-v3.git

# Build Benchmark Project
cd 'azure-cosmos-dotnet-v3/'
git checkout ${BENCHMARKING_TOOLS_BRANCH_NAME}

cd 'Microsoft.Azure.Cosmos.Samples/Tools/Benchmark'

echo "########## Build benckmark tool ##########"
dotnet build --configuration Release -p:"OSSProjectRef=true;ShouldUnsetParentConfigurationAndPlatform=false"

echo "########## Run benchmark ##########"
nohup dotnet run -c Release -e ${COSMOS_URI} -k ${COSMOS_KEY} -t ${THROUGHPUT} -n ${DOCUMENTS} --pl ${PARALLELISM} \
--enablelatencypercentiles true --resultscontainer ${RESULTS_CONTAINER} --resultspartitionkeyvalue "pk" \
--DiagnosticsStorageConnectionString ${DIAGNOSTICS_STORAGE_CONNECTION_STRING} \
--DiagnosticLatencyThresholdInMs ${DIAGNOSTICS_LATENCY_THRESHOLD_IN_MS} \
--DiagnosticsStorageContainerPrefix ${DIAGNOSTICS_STORAGE_CONTAINER_PREFIX} \
-w ${WORKLOAD_TYPE} \
> "/home/${ADMIN_USER_NAME}/agent.out" 2> "/home/${ADMIN_USER_NAME}/agent.err" &
