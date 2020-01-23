// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;

    /// <summary>
    /// Extracts the aggregated <see cref="BackendMetrics"/> from a <see cref="CosmosDiagnostics"/>.
    /// </summary>
    internal sealed class BackendMetricsExtractor : CosmosDiagnosticsInternalVisitor<(bool, BackendMetrics)>
    {
        public static readonly BackendMetricsExtractor Singleton = new BackendMetricsExtractor();

        private BackendMetricsExtractor()
        {
            // Private default constructor.
        }

        public override (bool, BackendMetrics) Visit(PointOperationStatistics pointOperationStatistics)
        {
            return (false, default);
        }

        public override (bool, BackendMetrics) Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext)
        {
            return cosmosDiagnosticsContext.ContextList.Accept(this);
        }

        public override (bool, BackendMetrics) Visit(CosmosDiagnosticScope cosmosDiagnosticScope)
        {
            return (false, default);
        }

        public override (bool, BackendMetrics) Visit(CosmosDiagnosticsContextList cosmosDiagnosticsContextList)
        {
            BackendMetrics.Accumulator accumulator = default;
            foreach (CosmosDiagnosticsInternal cosmosDiagnostics in cosmosDiagnosticsContextList)
            {
                (bool gotBackendMetric, BackendMetrics backendMetrics) = cosmosDiagnostics.Accept(this);
                if (gotBackendMetric)
                {
                    accumulator = accumulator.Accumulate(backendMetrics);
                }
            }

            return (true, BackendMetrics.Accumulator.ToBackendMetrics(accumulator));
        }

        public override (bool, BackendMetrics) Visit(QueryPageDiagnostics queryPageDiagnostics)
        {
            if (!BackendMetrics.TryParseFromDelimitedString(queryPageDiagnostics.QueryMetricText, out BackendMetrics backendMetrics))
            {
                return (false, default);
            }

            return (true, backendMetrics);
        }
    }
}
