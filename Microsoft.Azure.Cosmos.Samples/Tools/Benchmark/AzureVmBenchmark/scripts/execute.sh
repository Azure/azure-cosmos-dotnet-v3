#!/bin/sh

# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
export DOTNET_CLI_HOME=/temp

cloud-init status --wait
curl -o custom-script.sh $CUSTOM_SCRIPT_URL
bash -x custom-script.sh