//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Destination for per-window <see cref="PerfResultsRecord"/> rows. Implementations route the
    /// records to a Grafana-readable backend (e.g. Azure Data Explorer) or to the console.
    /// </summary>
    internal interface IMetricsSink
    {
        /// <summary>
        /// Emits the per-window result rows. Implementations must not throw; a sink failure must
        /// never interrupt the benchmark run.
        /// </summary>
        Task EmitAsync(IReadOnlyList<PerfResultsRecord> records);

        /// <summary>
        /// Flushes any buffered records. Called once at the end of the run.
        /// </summary>
        Task FlushAsync();
    }
}
