#!/bin/sh

# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

cloud-init status --wait
echo "##########CUSTOM_SCRIPT_URL###########: $CUSTOM_SCRIPT_URL"
# Running custom-script in background, arm template completion wont wait on this
# stdout and stderr will be logged in <$HOME>/custom-script.out and <$HOME>/custom-script.err
curl -o custom-script.sh $CUSTOM_SCRIPT_URL
bash custom-script.sh
