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
        private readonly TextWriter textWriter;

        public CosmosDiagnosticsSerializerVisitor(TextWriter textWriter)
        {
            this.textWriter = textWriter ?? throw new ArgumentNullException(nameof(textWriter));
        }

        public override void Visit(CosmosDiagnosticsAggregate cosmosDiagnosticsAggregate)
        {
            foreach (CosmosDiagnosticsInternal diagnostics in cosmosDiagnosticsAggregate)
            {
                diagnostics.Accept(this);
            }
        }

        public override void Visit(PointOperationStatistics pointOperationStatistics)
        {
            using (JsonWriter writer = new JsonTextWriter(this.textWriter))
            {
                writer.WriteStartObject();

                writer.WritePropertyName("ActivityId");
                writer.WriteValue(pointOperationStatistics.ActivityId);

                writer.WritePropertyName("StatusCode");
                writer.WriteValue(pointOperationStatistics.StatusCode);

                writer.WritePropertyName("SubStatusCode");
                writer.WriteValue(pointOperationStatistics.SubStatusCode);

                writer.WritePropertyName("RequestCharge");
                writer.WriteValue(pointOperationStatistics.RequestCharge);

                writer.WritePropertyName("ErrorMessage");
                writer.WriteValue(pointOperationStatistics.ErrorMessage ?? string.Empty);

                writer.WritePropertyName("Method");
                if (pointOperationStatistics.Method != null)
                {
                    writer.WriteValue(pointOperationStatistics.Method.ToString());
                }
                else
                {
                    writer.WriteNull();
                }

                writer.WritePropertyName("RequestUri");
                if (pointOperationStatistics.RequestUri != null)
                {
                    writer.WriteValue(pointOperationStatistics.RequestUri.ToString());
                }
                else
                {
                    writer.WriteNull();
                }

                writer.WritePropertyName("RequestSessionToken");
                writer.WriteValue(pointOperationStatistics.RequestSessionToken);

                writer.WritePropertyName("ResponseSessionToken");
                writer.WriteValue(pointOperationStatistics.ResponseSessionToken);

                if (pointOperationStatistics.ClientSideRequestStatistics != null)
                {
                    writer.WritePropertyName("ClientSideRequestStatistics");
                    pointOperationStatistics.ClientSideRequestStatistics.SerializeToJson(writer);
                }

                writer.WriteEndObject();
            }
        }

        public override void Visit(QueryAggregateDiagnostics queryAggregateDiagnostics)
        {
            // JSON array start
            using (JsonWriter writer = new JsonTextWriter(this.textWriter))
            {
                writer.WriteStartArray();

                foreach (QueryPageDiagnostics queryPage in queryAggregateDiagnostics.Pages)
                {
                    queryPage.AppendToBuilder(writer);
                }

                writer.WriteEndArray();
            }
        }
    }
}
