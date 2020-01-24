//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
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

            if (pointOperationStatistics.ClientSideRequestStatistics != null)
            {
                this.jsonWriter.WritePropertyName("ClientRequestStats");
                pointOperationStatistics.ClientSideRequestStatistics.WriteJsonObject(this.jsonWriter);
            }

            this.jsonWriter.WriteEndObject();
        }

        public override void Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext)
        {
            this.jsonWriter.WriteStartObject();

            this.jsonWriter.WritePropertyName("Summary");
            cosmosDiagnosticsContext.Summary.WriteJsonProperty(this.jsonWriter);

            this.jsonWriter.WritePropertyName("Context");
            cosmosDiagnosticsContext.ContextList.Accept(this);

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

        public override void Visit(CosmosDiagnosticsContextList cosmosDiagnosticsContextList)
        {
            this.jsonWriter.WriteStartArray();

            foreach (CosmosDiagnosticsInternal cosmosDiagnosticsInternal in cosmosDiagnosticsContextList)
            {
                cosmosDiagnosticsInternal.Accept(this);
            }

            this.jsonWriter.WriteEndArray();
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
            queryPageDiagnostics.DiagnosticsContext.ContextList.Accept(this);

            this.jsonWriter.WriteEndObject();
        }
    }
}
