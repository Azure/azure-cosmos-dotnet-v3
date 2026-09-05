#!/bin/bash

export OSSProjectRef=True
export RESULTS_PK=test_runs
# PL is set per-mode inside the loop below:
#   Direct           -> 18  (baseline; higher values regress Direct P99)
#   ThinClient/Gateway -> 75 (needed to saturate HTTP transport)

#These must be configured
export ACCOUNT_ENDPOINT=
# Leave ACCOUNT_KEY empty to authenticate with AAD (Microsoft Entra ID) using the
# VM's managed identity. When empty, the database and container must already exist.
# Optionally set ACCOUNT_MI_CLIENT_ID to select a specific user-assigned identity.
export ACCOUNT_KEY=
#export ACCOUNT_MI_CLIENT_ID=

# Loop forever
i=0
while :
do
    #Kill any running processes
    pkill -f run.sh
    git pull origin main

    # Distribute workload between modes
    mode=$((i % 3))
    if [ $mode -eq 0 ]; then
        echo "Running in THINCLIENT mode"
        export THINCLIENT_ENABLED=true
        export GATEWAYMODE_ENABLED=false
        export DIRECTMODE_ENABLED=false
        export PL=75
    elif [ $mode -eq 1 ]; then
        echo "Running in GATEWAY mode"
        export THINCLIENT_ENABLED=false
        export GATEWAYMODE_ENABLED=true
        export DIRECTMODE_ENABLED=false
        export PL=75
    else
        echo "Running in DIRECT mode"
        export THINCLIENT_ENABLED=false
        export GATEWAYMODE_ENABLED=false
        export DIRECTMODE_ENABLED=true
        export PL=18
    fi

    # Query operations take a long time
    # Only run them once every 10 runs
    if [ $(($i % 10)) -eq 0 ]; then
        echo Query run is enabled
        export INCLUDE_QUERY=true
    else
        export INCLUDE_QUERY=false
    fi
    ((i++))

    ./run.sh

    echo "====== Waiting for 10Sec ================="
    sleep 10 #Wait for 10sec

done