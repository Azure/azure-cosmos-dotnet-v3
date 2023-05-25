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
dotnet build --configuration Release -p:"OSSProjectRef=true;ShouldUnsetParentConfigurationAndPlatform=false"