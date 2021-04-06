#!/bin/bash

dotnetparameters="$dotnetparameters --ctl_endpoint $ctl_endpoint  --ctl_key $ctl_key"

if [ -z "$ctl_operation" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_operation $ctl_operation"
fi

if [ -z "$ctl_concurrency" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_concurrency $ctl_concurrency"
fi

if [ -z "$ctl_consistency_level" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_consistency_level $ctl_consistency_level"
fi

if [ -z "$ctl_throughput" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_throughput $ctl_throughput"
fi

if [ -z "$ctl_read_write_query_pct" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_read_write_query_pct $ctl_read_write_query_pct"
fi

if [ -z "$ctl_number_of_operations" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_number_of_operations $ctl_number_of_operations"
fi

if [ -z "$ctl_max_running_time_duration" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_max_running_time_duration $ctl_max_running_time_duration"
fi

if [ -z "$ctl_number_Of_collection" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_number_Of_collection $ctl_number_Of_collection"
fi

if [ -z "$ctl_diagnostics_threshold_duration" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_diagnostics_threshold_duration $ctl_diagnostics_threshold_duration"
fi

if [ -z "$ctl_database" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_database $ctl_database"
fi

if [ -z "$ctl_collection" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_collection $ctl_collection"
fi

if [ -z "$ctl_graphite_endpoint" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_graphite_endpoint $ctl_graphite_endpoint"
fi

if [ -z "$ctl_graphite_port" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_graphite_port $ctl_graphite_port"
fi

if [ -z "$ctl_reporting_interval" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_reporting_interval $ctl_reporting_interval"
fi

if [ -z "$ctl_content_response_on_write" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_content_response_on_write $ctl_content_response_on_write"
fi

if [ -z "$ctl_output_event_traces" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_output_event_traces $ctl_output_event_traces"
fi

if [ -z "$ctl_gateway_mode" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_gateway_mode $ctl_gateway_mode"
fi

if [ -z "$ctl_logging_context" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_logging_context $ctl_logging_context"
fi

if [ -z "$ctl_precreated_documents" ]
then
dotnetparameters="$dotnetparameters"
else
dotnetparameters="$dotnetparameters --ctl_precreated_documents $ctl_precreated_documents"
fi

log_filename="/tmp/dotnetctl.log"

echo "Log file name is $log_filename"

echo "$dotnetparameters" > $log_filename

./CosmosCTL $dotnetparameters 2>&1 | tee -a "$log_filename"