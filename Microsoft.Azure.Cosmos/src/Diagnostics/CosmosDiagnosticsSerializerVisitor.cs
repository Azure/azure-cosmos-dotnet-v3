//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using Newtonsoft.Json;

    internal sealed class CosmosDiagnosticsSerializerVisitor : CosmosDiagnosticsInternalVisitor
    {
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
            this.jsonWriter.WriteValue(pointOperationStatistics.ResponseTimeUtc);

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

            this.jsonWriter.WritePropertyName("Summary");
            this.jsonWriter.WriteStartObject();
            this.jsonWriter.WritePropertyName("StartUtc");
            this.jsonWriter.WriteValue(cosmosDiagnosticsContext.StartUtc.ToString("o", CultureInfo.InvariantCulture));

            if (cosmosDiagnosticsContext.OverallClientRequestTime.IsRunning)
            {
                this.jsonWriter.WritePropertyName("CurrentElapsedTime");
            }
            else
            {
                this.jsonWriter.WritePropertyName("TotalElapsedTime");
            }
            
            this.jsonWriter.WriteValue(cosmosDiagnosticsContext.OverallClientRequestTime.Elapsed);

            this.jsonWriter.WritePropertyName("UserAgent");
            this.jsonWriter.WriteValue(cosmosDiagnosticsContext.UserAgent);

            if (!string.IsNullOrEmpty(cosmosDiagnosticsContext.UserClientRequestId))
            {
                this.jsonWriter.WritePropertyName("UserClientRequestId");
                this.jsonWriter.WriteValue(cosmosDiagnosticsContext.UserClientRequestId);
            }

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

            this.jsonWriter.WritePropertyName("ElapsedTime");
            if (cosmosDiagnosticScope.TryGetElapsedTime(out TimeSpan elapsedTime))
            {
                this.jsonWriter.WriteValue(elapsedTime);
            }
            else
            {
                this.jsonWriter.WriteValue("Timer Never Stopped.");
            }

            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(QueryPageDiagnostics queryPageDiagnostics)
        {
            this.jsonWriter.WriteStartObject();

            this.jsonWriter.WritePropertyName("PKRangeId");
            this.jsonWriter.WriteValue(queryPageDiagnostics.PartitionKeyRangeId);

            this.jsonWriter.WritePropertyName("QueryMetric");
            this.jsonWriter.WriteValue(queryPageDiagnostics.QueryMetricText);

            this.jsonWriter.WritePropertyName("IndexUtilization");
            this.jsonWriter.WriteValue(queryPageDiagnostics.IndexUtilizationText);

            this.jsonWriter.WritePropertyName("SchedulingTimeSpan");
            queryPageDiagnostics.SchedulingTimeSpan.WriteJsonObject(this.jsonWriter);

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

            this.jsonWriter.WritePropertyName("ResponseTimeUtc");
            this.jsonWriter.WriteValue(storeResponseStatistics.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture));

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
