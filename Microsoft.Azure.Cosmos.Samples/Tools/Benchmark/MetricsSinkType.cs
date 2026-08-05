//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    /// <summary>
    /// Supported per-window metrics sinks selected via <c>--metrics-sink</c>.
    /// </summary>
    public enum MetricsSinkType
    {
        /// <summary>No per-window record sink (default). Existing OTel/App Insights export is unaffected.</summary>
        None,

        /// <summary>Write per-window result rows to the console as JSON.</summary>
        Console,

        /// <summary>Ingest per-window result rows into Azure Data Explorer (Kusto).</summary>
        Adx,
    }
}
