//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry;
    using OpenTelemetry.Trace;

    internal class CustomTraceExporter : BaseExporter<Activity>
    {
        private readonly string _name;

        public static List<Activity> CollectedActivities;
        
        public CustomTraceExporter()
        {
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            // NoOp

            return ExportResult.Success;
        }
    }

    internal static class OTelExtensions
    {
        public static TracerProviderBuilder AddCustomOtelExporter(this TracerProviderBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddProcessor(new SimpleActivityExportProcessor(new CustomTraceExporter()));
        }
    }
}
