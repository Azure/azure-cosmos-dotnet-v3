#!/bin/bash

# Looped benchmrak run
# $1: BenchmarkName
# $2: IterationCount
loopedBenchmarkRun() {
    echo
    echo ========$1==========
    echo

    for ((i=0; i < $2; i++))
    do
        echo ========ITER: $i ==========
        dotnet run -c Release  -- -e $ACCOUNT_ENDPOINT -k $ACCOUNT_KEY --publishresults --resultspartitionkeyvalue $RESULTS_PK -commitid $(git log -1 | head -n 1 | cut -d ' ' -f 2) --commitdate $(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 1 -d ' ') --committime $(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 2 -d ' ') --branchname $(git rev-parse --abbrev-ref HEAD)  --database testdb --container testcol --partitionkeypath /pk -n 500000 -w ReadStreamExistsV3 --pl $PL 
    done
}

if [ -z "$ACCOUNT_ENDPOINT" ]
then
    echo "Missing ACCOUNT_ENDPOINT"
    exit -1
fi

if [ -z "$ACCOUNT_KEY" ]
then
    echo "Missing ACCOUNT_KEY"
    exit -1
fi

if [ -z "$RESULTS_PK" ]
then
    echo "Missing RESULTS_PK"
    exit -1
fi

if [ -Z "$PL" ]
then
    echo "Missing PL"
    exit -1
fi

for BENCHMARK_NAME in InsertV3 ReadFeedStreamV3 ReadNotExistsV3 ReadStreamExistsV3 ReadTExistsV3
do
    loopedBenchmarkRun $BENCHMARK_NAME 5
done

