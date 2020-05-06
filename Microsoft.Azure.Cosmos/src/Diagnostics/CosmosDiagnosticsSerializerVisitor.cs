//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal sealed class CosmosDiagnosticsSerializerVisitor : CosmosDiagnosticsInternalVisitor
    {
        private const string DiagnosticsVersion = "2";
        private readonly JsonWriter jsonWriter;

        public CosmosDiagnosticsSerializerVisitor(TextWriter textWriter)
        {
            this.jsonWriter = new JsonTextWriter(textWriter ?? throw new ArgumentNullException(nameof(textWriter)));
        }

        public override void Visit(PointOperationStatistics pointOperationStatistics)
        {
            this.jsonWriter.WriteStartObject();
            this.jsonWriter.WritePropertyName("Id");
            this.jsonWriter.WriteValue("PointOperationStatistics");

            this.jsonWriter.WritePropertyName("ActivityId");
            this.jsonWriter.WriteValue(pointOperationStatistics.ActivityId);

            this.jsonWriter.WritePropertyName("ResponseTimeUtc");
            this.jsonWriter.WriteValue(pointOperationStatistics.ResponseTimeUtc.ToString("o", CultureInfo.InvariantCulture));

            this.jsonWriter.WritePropertyName("StatusCode");
            this.jsonWriter.WriteValue((int)pointOperationStatistics.StatusCode);

            this.jsonWriter.WritePropertyName("SubStatusCode");
            this.jsonWriter.WriteValue((int)pointOperationStatistics.SubStatusCode);

            this.jsonWriter.WritePropertyName("RequestCharge");
            this.jsonWriter.WriteValue(pointOperationStatistics.RequestCharge);

            this.jsonWriter.WritePropertyName("RequestUri");
            this.jsonWriter.WriteValue(pointOperationStatistics.RequestUri);

            if (!string.IsNullOrEmpty(pointOperationStatistics.ErrorMessage))
            {
                this.jsonWriter.WritePropertyName("ErrorMessage");
                this.jsonWriter.WriteValue(pointOperationStatistics.ErrorMessage);
            }

            this.jsonWriter.WritePropertyName("RequestSessionToken");
            this.jsonWriter.WriteValue(pointOperationStatistics.RequestSessionToken);

            this.jsonWriter.WritePropertyName("ResponseSessionToken");
            this.jsonWriter.WriteValue(pointOperationStatistics.ResponseSessionToken);

            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext)
        {
            this.jsonWriter.WriteStartObject();

            this.jsonWriter.WritePropertyName("Version");
            this.jsonWriter.WriteValue(DiagnosticsVersion);

            this.jsonWriter.WritePropertyName("Summary");
            this.jsonWriter.WriteStartObject();
            this.jsonWriter.WritePropertyName("StartUtc");
            this.jsonWriter.WriteValue(cosmosDiagnosticsContext.StartUtc.ToString("o", CultureInfo.InvariantCulture));

            if (cosmosDiagnosticsContext.IsComplete())
            {
                this.jsonWriter.WritePropertyName("TotalElapsedTimeInMs");
            }
            else
            {
                this.jsonWriter.WritePropertyName("RunningElapsedTimeInMs");
            }
            
            this.jsonWriter.WriteValue(cosmosDiagnosticsContext.GetClientElapsedTime().TotalMilliseconds);

            this.jsonWriter.WritePropertyName("UserAgent");
            this.jsonWriter.WriteValue(cosmosDiagnosticsContext.UserAgent);

            this.jsonWriter.WritePropertyName("TotalRequestCount");
            this.jsonWriter.WriteValue(cosmosDiagnosticsContext.TotalRequestCount);

            this.jsonWriter.WritePropertyName("FailedRequestCount");
            this.jsonWriter.WriteValue(cosmosDiagnosticsContext.FailedRequestCount);

            this.jsonWriter.WriteEndObject();

            this.jsonWriter.WritePropertyName("Context");
            this.jsonWriter.WriteStartArray();

            foreach (CosmosDiagnosticsInternal cosmosDiagnosticsInternal in cosmosDiagnosticsContext)
            {
                cosmosDiagnosticsInternal.Accept(this);
            }

            this.jsonWriter.WriteEndArray();

            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(CosmosDiagnosticScope cosmosDiagnosticScope)
        {
            this.jsonWriter.WriteStartObject();

            this.jsonWriter.WritePropertyName("Id");
            this.jsonWriter.WriteValue(cosmosDiagnosticScope.Id);

            if (cosmosDiagnosticScope.IsComplete())
            {
                this.jsonWriter.WritePropertyName("ElapsedTimeInMs");
            }
            else
            {
                this.jsonWriter.WritePropertyName("RunningElapsedTimeInMs");
            }

            this.jsonWriter.WriteValue(cosmosDiagnosticScope.GetElapsedTime().TotalMilliseconds);

            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(QueryPageDiagnostics queryPageDiagnostics)
        {
            this.jsonWriter.WriteStartObject();

            this.jsonWriter.WritePropertyName("PKRangeId");
            this.jsonWriter.WriteValue(queryPageDiagnostics.PartitionKeyRangeId);

            this.jsonWriter.WritePropertyName("StartUtc");
            this.jsonWriter.WriteValue(queryPageDiagnostics.DiagnosticsContext.StartUtc.ToString("o", CultureInfo.InvariantCulture));

            this.jsonWriter.WritePropertyName("QueryMetric");
            this.jsonWriter.WriteValue(queryPageDiagnostics.QueryMetricText);

            this.jsonWriter.WritePropertyName("IndexUtilization");
            this.jsonWriter.WriteValue(queryPageDiagnostics.IndexUtilizationText);

            this.jsonWriter.WritePropertyName("ClientCorrelationId");
            this.jsonWriter.WriteValue(queryPageDiagnostics.ClientCorrelationId);

            this.jsonWriter.WritePropertyName("Context");
            this.jsonWriter.WriteStartArray();

            foreach (CosmosDiagnosticsInternal cosmosDiagnosticsInternal in queryPageDiagnostics.DiagnosticsContext)
            {
                cosmosDiagnosticsInternal.Accept(this);
            }

            this.jsonWriter.WriteEndArray();

            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(AddressResolutionStatistics addressResolutionStatistics)
        {
            this.jsonWriter.WriteStartObject();

            this.jsonWriter.WritePropertyName("Id");
            this.jsonWriter.WriteValue("AddressResolutionStatistics");

            this.jsonWriter.WritePropertyName("StartTimeUtc");
            this.jsonWriter.WriteValue(addressResolutionStatistics.StartTime.ToString("o", CultureInfo.InvariantCulture));

            this.jsonWriter.WritePropertyName("EndTimeUtc");
            if (addressResolutionStatistics.EndTime.HasValue)
            {
                this.jsonWriter.WriteValue(addressResolutionStatistics.EndTime.Value.ToString("o", CultureInfo.InvariantCulture));

                this.jsonWriter.WritePropertyName("ElapsedTimeInMs");
                TimeSpan totaltime = addressResolutionStatistics.EndTime.Value - addressResolutionStatistics.StartTime;
                this.jsonWriter.WriteValue(totaltime.TotalMilliseconds);
            }
            else
            {
                this.jsonWriter.WriteValue("EndTime Never Set.");
            }

            this.jsonWriter.WritePropertyName("TargetEndpoint");
            this.jsonWriter.WriteValue(addressResolutionStatistics.TargetEndpoint);

            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(StoreResponseStatistics storeResponseStatistics)
        {
            this.jsonWriter.WriteStartObject();

            this.jsonWriter.WritePropertyName("Id");
            this.jsonWriter.WriteValue("StoreResponseStatistics");

            this.jsonWriter.WritePropertyName("StartTimeUtc");
            if (storeResponseStatistics.RequestStartTime.HasValue)
            {
                this.jsonWriter.WriteValue(storeResponseStatistics.RequestStartTime.Value.ToString("o", CultureInfo.InvariantCulture));
            }
            else
            {
                this.jsonWriter.WriteValue("Start time never set");
            }

            this.jsonWriter.WritePropertyName("ResponseTimeUtc");
            this.jsonWriter.WriteValue(storeResponseStatistics.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture));

            if (storeResponseStatistics.RequestStartTime.HasValue)
            {
                this.jsonWriter.WritePropertyName("ElapsedTimeInMs");
                TimeSpan totaltime = storeResponseStatistics.RequestResponseTime - storeResponseStatistics.RequestStartTime.Value;
                this.jsonWriter.WriteValue(totaltime.TotalMilliseconds);
            }

            this.jsonWriter.WritePropertyName("ResourceType");
            this.jsonWriter.WriteValue(storeResponseStatistics.RequestResourceType.ToString());

            this.jsonWriter.WritePropertyName("OperationType");
            this.jsonWriter.WriteValue(storeResponseStatistics.RequestOperationType.ToString());

            this.jsonWriter.WritePropertyName("LocationEndpoint");
            this.jsonWriter.WriteValue(storeResponseStatistics.LocationEndpoint);

            if (storeResponseStatistics.StoreResult != null)
            {
                this.jsonWriter.WritePropertyName("StoreResult");
                this.jsonWriter.WriteValue(storeResponseStatistics.StoreResult.ToString());
            }

            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(CosmosClientSideRequestStatistics clientSideRequestStatistics)
        {
            this.jsonWriter.WriteStartObject();
            this.jsonWriter.WritePropertyName("Id");
            this.jsonWriter.WriteValue("AggregatedClientSideRequestStatistics");

            this.WriteJsonUriArrayWithDuplicatesCounted("ContactedReplicas", clientSideRequestStatistics.ContactedReplicas);

            this.WriteJsonUriArray("RegionsContacted", clientSideRequestStatistics.RegionsContacted);
            this.WriteJsonUriArray("FailedReplicas", clientSideRequestStatistics.FailedReplicas);

            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(FeedRangeStatistics feedRangeStatistics)
        {
            this.jsonWriter.WriteStartObject();
            this.jsonWriter.WritePropertyName("FeedRange");
            this.jsonWriter.WriteValue(feedRangeStatistics.FeedRange.ToString());
            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(RequestHandlerScope requestHandlerScope)
        {
            this.jsonWriter.WriteStartObject();

            this.jsonWriter.WritePropertyName("Id");
            this.jsonWriter.WriteValue(requestHandlerScope.Id);

            if (requestHandlerScope.TryGetTotalElapsedTime(out TimeSpan handlerOnlyElapsedTime))
            {
                this.jsonWriter.WritePropertyName("HandlerElapsedTimeInMs");
                this.jsonWriter.WriteValue(handlerOnlyElapsedTime.TotalMilliseconds);
            }
            else
            {
                this.jsonWriter.WritePropertyName("HandlerRunningElapsedTimeInMs");
                this.jsonWriter.WriteValue(requestHandlerScope.GetCurrentElapsedTime());
            }

            this.jsonWriter.WriteEndObject();
        }

        private void WriteJsonUriArray(string propertyName, IEnumerable<Uri> uris)
        {
            this.jsonWriter.WritePropertyName(propertyName);
            this.jsonWriter.WriteStartArray();

            if (uris != null)
            {
                foreach (Uri contactedReplica in uris)
                {
                    this.jsonWriter.WriteValue(contactedReplica);
                }
            }

            this.jsonWriter.WriteEndArray();
        }

        /// <summary>
        /// Writes the list of URIs to JSON.
        /// Sequential duplicates are counted and written as a single object to prevent
        /// writing the same URI multiple times.
        /// </summary>
        private void WriteJsonUriArrayWithDuplicatesCounted(string propertyName, List<Uri> uris)
        {
            this.jsonWriter.WritePropertyName(propertyName);
            this.jsonWriter.WriteStartArray();

            if (uris != null)
            {
                Uri previous = null;
                int duplicateCount = 1;
                int totalCount = uris.Count;
                for (int i = 0; i < totalCount; i++)
                {
                    Uri contactedReplica = uris[i];
                    if (contactedReplica.Equals(previous))
                    {
                        duplicateCount++;
                        // Don't continue for last link so it get's printed
                        if (i < totalCount - 1)
                        {
                            continue;
                        }
                    }

                    // The URI is not a duplicate.
                    // Write previous URI and count.
                    // Then update them to the new URI and count
                    if (previous != null)
                    {
                        this.jsonWriter.WriteStartObject();
                        this.jsonWriter.WritePropertyName("Count");
                        this.jsonWriter.WriteValue(duplicateCount);
                        this.jsonWriter.WritePropertyName("Uri");
                        this.jsonWriter.WriteValue(contactedReplica);
                        this.jsonWriter.WriteEndObject();
                    }

                    previous = contactedReplica;
                    duplicateCount = 1;
                }
            }

            this.jsonWriter.WriteEndArray();
        }
    }
}
