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

if [ -z "$TELEMETRY_ENDPOINT" ]
then
    echo "Missing TELEMETRY_ENDPOINT"
    exit -1
fi

if [ -z "$INCLUDE_QUERY" ]
then
    echo "Missing INCLUDE_QUERY"
    exit -1
fi

COMMIT_ID=$(git log -1 | head -n 1 | cut -d ' ' -f 2)
COMMIT_DATE=$(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 1 -d ' ')
COMMIT_TIME=$(git log -1 --date=format:'%Y-%m-%d %H:%M:%S' | grep Date | cut -f 2- -d ':' | sed 's/^[ \t]*//;s/[ \t]*$//' | cut -f 2 -d ' ')
BRANCH_NAME=$(git rev-parse --abbrev-ref HEAD)

echo $COMMIT_ID
echo $COMMIT_DATE
echo $COMMIT_TIME
echo $BRANCH_NAME

# Client telemetry disabled ReadStreamExistsV3
dotnet run -c Release  -- -n 2000000 -w ReadStreamExistsV3 --tcp 10 --pl $PL -e $ACCOUNT_ENDPOINT -k $ACCOUNT_KEY  --enablelatencypercentiles --disablecoresdklogging --publishresults --resultspartitionkeyvalue $RESULTS_PK --commitid $COMMIT_ID --commitdate $COMMIT_DATE --committime $COMMIT_TIME  --branchname $BRANCH_NAME --database testdb --container testcol --partitionkeypath /pk 
sleep 10 #Wait

# Client telemetry enabled ReadStreamExistsV3. This is needed to see the impact of client telemetry. 
dotnet run -c Release  -- -n 2000000 -w ReadStreamExistsV3 --WorkloadName ReadStreamExistsV3WithTelemetry  --enableTelemetry --telemetryScheduleInSec 60 --telemetryEndpoint $TELEMETRY_ENDPOINT --tcp 10 --pl $PL -e $ACCOUNT_ENDPOINT -k $ACCOUNT_KEY  --enablelatencypercentiles --disablecoresdklogging --publishresults --resultspartitionkeyvalue $RESULTS_PK --commitid $COMMIT_ID --commitdate $COMMIT_DATE --committime $COMMIT_TIME  --branchname $BRANCH_NAME --database testdb --container testcol --partitionkeypath /pk 
sleep 10 #Wait

#Point read operations
for WORKLOAD_NAME in ReadNotExistsV3 ReadTExistsV3 ReadStreamExistsWithDiagnosticsV3
do
    dotnet run -c Release  -- -n 2000000 -w $WORKLOAD_NAME --pl $PL --enableTelemetry --telemetryScheduleInSec 60 --telemetryEndpoint $TELEMETRY_ENDPOINT --tcp 10 -e $ACCOUNT_ENDPOINT -k $ACCOUNT_KEY  --enablelatencypercentiles --disablecoresdklogging --publishresults --resultspartitionkeyvalue $RESULTS_PK --commitid $COMMIT_ID --commitdate $COMMIT_DATE --committime $COMMIT_TIME  --branchname $BRANCH_NAME --database testdb --container testcol --partitionkeypath /pk 
    sleep 10 #Wait
done

#Insert operation
dotnet run -c Release  -- -n 2000000 -w InsertV3 --pl 30 --enableTelemetry --telemetryScheduleInSec 60 --telemetryEndpoint $TELEMETRY_ENDPOINT --tcp 1 -e $ACCOUNT_ENDPOINT -k $ACCOUNT_KEY  --enablelatencypercentiles --disablecoresdklogging --publishresults --resultspartitionkeyvalue $RESULTS_PK --commitid $COMMIT_ID --commitdate $COMMIT_DATE --committime $COMMIT_TIME  --branchname $BRANCH_NAME --database testdb --container testcol --partitionkeypath /pk 
sleep 45 #Wait

if [ "$INCLUDE_QUERY" = true ]
then
    #Query operations
    # n value is lowered to 200000 because queries are significantly slower. This prevents the runs from taking to long.
    # pl is 16 because 18 was casuing a small amount of thorrtles.
    for WORKLOAD_NAME in ReadFeedStreamV3 QueryTSinglePkV3 QueryTSinglePkOrderByWithPaginationV3 QueryTSinglePkOrderByFullDrainV3 QueryTCrossPkV3 QueryTCrossPkOrderByWithPaginationV3 QueryTCrossPkOrderByFullDrainV3 QueryStreamSinglePkV3 QueryStreamSinglePkOrderByWithPaginationV3 QueryStreamSinglePkOrderByFullDrainV3 QueryStreamCrossPkV3 QueryStreamCrossPkOrderByWithPaginationV3 QueryStreamCrossPkOrderByFullDrainV3
    do
        dotnet run -c Release  -- -n 200000 -w $WORKLOAD_NAME --pl 16 --enableTelemetry --telemetryScheduleInSec 60 --telemetryEndpoint $TELEMETRY_ENDPOINT --tcp 10 -e $ACCOUNT_ENDPOINT -k $ACCOUNT_KEY --enablelatencypercentiles --disablecoresdklogging --publishresults --resultspartitionkeyvalue $RESULTS_PK --commitid $COMMIT_ID --commitdate $COMMIT_DATE --committime $COMMIT_TIME  --branchname $BRANCH_NAME --database testdb --container testcol --partitionkeypath /pk 
        sleep 10 #Wait
    done
fi