#!/bin/bash

export OSSProjectRef=True
export RESULTS_PK=test_runs
export PL=18

#These must be configured
export ACCOUNT_ENDPOINT=
export ACCOUNT_KEY=

# Loop forever
i=0
while :
do
    #Kill any running processes
    pkill -f run.sh
    git pull origin master

    # Distribute workload between modes
    mode=$((i % 3))
    if [ $mode -eq 0 ]; then
        echo "Running in THINCLIENT mode"
        export THINCLIENT_ENABLED=true
        export GATEWAYMODE_ENABLED=false
        export DIRECTMODE_ENABLED=false
    elif [ $mode -eq 1 ]; then
        echo "Running in GATEWAY mode"
        export THINCLIENT_ENABLED=false
        export GATEWAYMODE_ENABLED=true
        export DIRECTMODE_ENABLED=false
    else
        echo "Running in DIRECT mode"
        export THINCLIENT_ENABLED=false
        export GATEWAYMODE_ENABLED=false
        export DIRECTMODE_ENABLED=true
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