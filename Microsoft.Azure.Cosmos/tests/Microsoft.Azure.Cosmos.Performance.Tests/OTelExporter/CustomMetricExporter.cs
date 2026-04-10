//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Metrics
{
    using OpenTelemetry.Metrics;
    using OpenTelemetry;

    public class CustomMetricExporter : BaseExporter<Metric>
    {
        public CustomMetricExporter()
        {
        }

        // This method will be called periodically by OpenTelemetry SDK
        public override ExportResult Export(in Batch<Metric> batch)
        {
            //NoOP
            return ExportResult.Success;
        }
    }
}
