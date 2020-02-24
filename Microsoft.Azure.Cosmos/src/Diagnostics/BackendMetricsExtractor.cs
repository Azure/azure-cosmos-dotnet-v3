// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using static Microsoft.Azure.Cosmos.Diagnostics.BackendMetricsExtractor;

    /// <summary>
    /// Extracts the aggregated <see cref="BackendMetrics"/> from a <see cref="CosmosDiagnostics"/>.
    /// </summary>
    internal sealed class BackendMetricsExtractor : CosmosDiagnosticsInternalVisitor<(ParseFailureReason, BackendMetrics)>
    {
        public static readonly BackendMetricsExtractor Singleton = new BackendMetricsExtractor();

        private BackendMetricsExtractor()
        {
            // Private default constructor.
        }

        public override (ParseFailureReason, BackendMetrics) Visit(PointOperationStatistics pointOperationStatistics)
        {
            return (ParseFailureReason.MetricsNotFound, default);
        }

        public override (ParseFailureReason, BackendMetrics) Visit(CosmosDiagnosticsContext cosmosDiagnosticsContext)
        {
            BackendMetrics.Accumulator accumulator = default;
            bool metricsFound = false;
            foreach (CosmosDiagnosticsInternal cosmosDiagnostics in cosmosDiagnosticsContext)
            {
                (ParseFailureReason parseFailureReason, BackendMetrics backendMetrics) = cosmosDiagnostics.Accept(this);
                switch (parseFailureReason)
                {
                    case ParseFailureReason.None:
                        metricsFound = true;
                        accumulator = accumulator.Accumulate(backendMetrics);
                        break;

                    case ParseFailureReason.MalformedString:
                        return (parseFailureReason, default);

                    default:
                        break;
                }
            }

            (ParseFailureReason parseFailureReason, BackendMetrics backendMetrics) failureReasonAndMetrics;
            if (metricsFound)
            {
                failureReasonAndMetrics = (ParseFailureReason.None, BackendMetrics.Accumulator.ToBackendMetrics(accumulator));
            }
            else
            {
                failureReasonAndMetrics = (ParseFailureReason.MetricsNotFound, default);
            }

            return failureReasonAndMetrics;
        }

        public override (ParseFailureReason, BackendMetrics) Visit(CosmosDiagnosticScope cosmosDiagnosticScope)
        {
            return (ParseFailureReason.MetricsNotFound, default);
        }

        public override (ParseFailureReason, BackendMetrics) Visit(QueryPageDiagnostics queryPageDiagnostics)
        {
            if (!BackendMetrics.TryParseFromDelimitedString(queryPageDiagnostics.QueryMetricText, out BackendMetrics backendMetrics))
            {
                return (ParseFailureReason.MalformedString, default);
            }

            return (ParseFailureReason.None, backendMetrics);
        }

        public override (ParseFailureReason, BackendMetrics) Visit(QueryPipelineDiagnostics queryPipelineDiagnostics)
        {
            return (ParseFailureReason.MetricsNotFound, default);
        }

        public enum ParseFailureReason
        {
            None,
            MetricsNotFound,
            MalformedString
        }
    }
}
