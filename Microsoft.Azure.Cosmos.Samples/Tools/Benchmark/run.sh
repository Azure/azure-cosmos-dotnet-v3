#!/bin/bash

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

if [ -z "$PL" ]
then
    echo "Missing PL"
    exit -1
fi

for BENCHMARK_NAME in InsertV3 ReadStreamExistsV3 #ReadFeedStreamV3 ReadNotExistsV3 ReadTExistsV3
do
    dotnet run -c Release  -- -e $ACCOUNT_ENDPOINT -k $ACCOUNT_KEY --DisableCoreSdkLogging --publishresults --resultspartitionkeyvalue $RESULTS_PK --commitid $(git log -1 | head -n 1 | cut -d ' ' -f 2) --commitdate $(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 1 -d ' ') --committime $(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 2 -d ' ') --branchname $(git rev-parse --abbrev-ref HEAD)  --database testdb --container testcol --partitionkeypath /pk -n 2000000 -w $BENCHMARK_NAME --pl $PL 
done

