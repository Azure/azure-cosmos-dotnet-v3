// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal static partial class TraceWriter
    {
        private static class TraceJsonWriter
        {
            public static void WriteTrace(
                IJsonWriter writer,
                ITrace trace)
            {
                if (writer == null)
                {
                    throw new ArgumentNullException(nameof(writer));
                }

                if (trace == null)
                {
                    throw new ArgumentNullException(nameof(trace));
                }

                writer.WriteObjectStart();

                writer.WriteFieldName("name");
                writer.WriteStringValue(trace.Name);

                writer.WriteFieldName("id");
                writer.WriteStringValue(trace.Id.ToString());

                writer.WriteFieldName("component");
                writer.WriteStringValue(trace.Component.ToString());

                writer.WriteFieldName("caller information");
                writer.WriteObjectStart();

                writer.WriteFieldName("member name");
                writer.WriteStringValue(trace.CallerInfo.MemberName);

                writer.WriteFieldName("file path");
                writer.WriteStringValue(trace.CallerInfo.FilePath);

                writer.WriteFieldName("line number");
                writer.WriteNumber64Value(trace.CallerInfo.LineNumber);

                writer.WriteObjectEnd();

                writer.WriteFieldName("start time");
                writer.WriteStringValue(trace.StartTime.ToString("hh:mm:ss:fff"));

                writer.WriteFieldName("duration in milliseconds");
                writer.WriteNumber64Value(trace.Duration.TotalMilliseconds);

                writer.WriteFieldName("data");
                writer.WriteObjectStart();

                foreach (KeyValuePair<string, object> kvp in trace.Data)
                {
                    string key = kvp.Key;
                    object value = kvp.Value;

                    writer.WriteFieldName(key);
                    WriteTraceDatum(writer, value);
                }

                writer.WriteObjectEnd();

                writer.WriteFieldName("children");
                writer.WriteArrayStart();

                foreach (ITrace child in trace.Children)
                {
                    WriteTrace(writer, child);
                }

                writer.WriteArrayEnd();

                writer.WriteObjectEnd();
            }
        }

        private static void WriteTraceDatum(IJsonWriter writer, object value)
        {
            if (value is TraceDatum traceDatum)
            {
                TraceDatumJsonWriter traceJsonWriter = new TraceDatumJsonWriter(writer);
                traceDatum.Accept(traceJsonWriter);
            }
            else if (value is double doubleValue)
            {
                writer.WriteNumber64Value(doubleValue);
            }
            else if (value is long longValue)
            {
                writer.WriteNumber64Value(longValue);
            }
            else if (value is IEnumerable<object> enumerable)
            {
                writer.WriteArrayStart();

                foreach (object item in enumerable)
                {
                    WriteTraceDatum(writer, item);
                }

                writer.WriteArrayEnd();
            }
            else if (value is IDictionary<string, object> dictionary)
            {
                writer.WriteObjectStart();

                foreach (KeyValuePair<string, object> kvp in dictionary)
                {
                    writer.WriteFieldName(kvp.Key);
                    WriteTraceDatum(writer, kvp.Value);
                }

                writer.WriteObjectEnd();
            }
            else if (value is string stringValue)
            {
                writer.WriteStringValue(stringValue);
            }
            else
            {
                writer.WriteStringValue(value.ToString());
            }
        }

        private sealed class TraceDatumJsonWriter : ITraceDatumVisitor
        {
            private readonly IJsonWriter jsonWriter;

            public TraceDatumJsonWriter(IJsonWriter jsonWriter)
            {
                this.jsonWriter = jsonWriter ?? throw new ArgumentNullException(nameof(jsonWriter));
            }

            public void Visit(QueryMetricsTraceDatum queryMetricsTraceDatum)
            {
                this.jsonWriter.WriteStringValue(queryMetricsTraceDatum.QueryMetrics.ToString());
            }

            public void Visit(CosmosDiagnosticsTraceDatum cosmosDiagnosticsTraceDatum)
            {
                StringWriter writer = new StringWriter();
                CosmosDiagnosticsSerializerVisitor serializer = new CosmosDiagnosticsSerializerVisitor(writer);
                cosmosDiagnosticsTraceDatum.CosmosDiagnostics.Accept(serializer);
                this.jsonWriter.WriteStringValue(writer.ToString());
            }
        }
    }
}
