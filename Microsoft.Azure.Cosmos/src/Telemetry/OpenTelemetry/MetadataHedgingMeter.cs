//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Meter wrapper for PPAF metadata-hedging telemetry. See design
    /// <c>docs/PPAF_Metadata_Hedging_ColdStart_Design.md</c> §9.1.1.
    /// </summary>
    internal static class MetadataHedgingMeter
    {
        private static readonly Meter Meter = new Meter(
            CosmosDbClientMetrics.MetadataHedgingMetrics.MeterName,
            CosmosDbClientMetrics.MetadataHedgingMetrics.Version);

        private static readonly Counter<long> FiresCounter = Meter.CreateCounter<long>(
            name: CosmosDbClientMetrics.MetadataHedgingMetrics.Name.Fires,
            unit: CosmosDbClientMetrics.MetadataHedgingMetrics.Unit.Request,
            description: CosmosDbClientMetrics.MetadataHedgingMetrics.Description.Fires);

        private static readonly Counter<long> HedgeWinsCounter = Meter.CreateCounter<long>(
            name: CosmosDbClientMetrics.MetadataHedgingMetrics.Name.HedgeWins,
            unit: CosmosDbClientMetrics.MetadataHedgingMetrics.Unit.Request,
            description: CosmosDbClientMetrics.MetadataHedgingMetrics.Description.HedgeWins);

        private static readonly Counter<long> BudgetExhaustedCounter = Meter.CreateCounter<long>(
            name: CosmosDbClientMetrics.MetadataHedgingMetrics.Name.BudgetExhausted,
            unit: CosmosDbClientMetrics.MetadataHedgingMetrics.Unit.Request,
            description: CosmosDbClientMetrics.MetadataHedgingMetrics.Description.BudgetExhausted);

        private static readonly Counter<long> LateLoserCounter = Meter.CreateCounter<long>(
            name: CosmosDbClientMetrics.MetadataHedgingMetrics.Name.LateLoser,
            unit: CosmosDbClientMetrics.MetadataHedgingMetrics.Unit.Request,
            description: CosmosDbClientMetrics.MetadataHedgingMetrics.Description.LateLoser);

        private static readonly Counter<long> HedgeAuthRejectCounter = Meter.CreateCounter<long>(
            name: CosmosDbClientMetrics.MetadataHedgingMetrics.Name.HedgeAuthReject,
            unit: CosmosDbClientMetrics.MetadataHedgingMetrics.Unit.Request,
            description: CosmosDbClientMetrics.MetadataHedgingMetrics.Description.HedgeAuthReject);

        private static readonly Histogram<double> HedgeFiredElapsedHistogram = Meter.CreateHistogram<double>(
            name: CosmosDbClientMetrics.MetadataHedgingMetrics.Name.HedgeFiredElapsed,
            unit: CosmosDbClientMetrics.MetadataHedgingMetrics.Unit.Sec,
            description: CosmosDbClientMetrics.MetadataHedgingMetrics.Description.HedgeFiredElapsed);

        internal static void RecordFire(string primaryRegion, string hedgeRegion, double elapsedMs)
        {
            FiresCounter.Add(1,
                new KeyValuePair<string, object>("primary_region", primaryRegion ?? "unknown"),
                new KeyValuePair<string, object>("hedge_region", hedgeRegion ?? "unknown"));

            HedgeFiredElapsedHistogram.Record(elapsedMs / 1000.0,
                new KeyValuePair<string, object>("primary_region", primaryRegion ?? "unknown"),
                new KeyValuePair<string, object>("hedge_region", hedgeRegion ?? "unknown"));
        }

        internal static void RecordHedgeWin(string hedgeRegion)
        {
            HedgeWinsCounter.Add(1,
                new KeyValuePair<string, object>("hedge_region", hedgeRegion ?? "unknown"));
        }

        internal static void RecordBudgetExhausted(string resourceType)
        {
            BudgetExhaustedCounter.Add(1,
                new KeyValuePair<string, object>("resource_type", resourceType ?? "unknown"));
        }

        internal static void RecordLateLoser(string loserRegion, string loserOutcome)
        {
            LateLoserCounter.Add(1,
                new KeyValuePair<string, object>("loser_region", loserRegion ?? "unknown"),
                new KeyValuePair<string, object>("loser_outcome", loserOutcome ?? "unknown"));
        }

        internal static void RecordHedgeAuthReject(string hedgeRegion, int statusCode)
        {
            HedgeAuthRejectCounter.Add(1,
                new KeyValuePair<string, object>("hedge_region", hedgeRegion ?? "unknown"),
                new KeyValuePair<string, object>("status_code", statusCode));
        }
    }
}
