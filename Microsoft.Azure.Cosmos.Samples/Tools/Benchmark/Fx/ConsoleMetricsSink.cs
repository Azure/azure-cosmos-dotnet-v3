//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// Writes each per-window <see cref="PerfResultsRecord"/> to the console as a single-line JSON
    /// document. Useful for local validation and as a zero-dependency fallback sink.
    /// </summary>
    internal sealed class ConsoleMetricsSink : IMetricsSink
    {
        public Task EmitAsync(IReadOnlyList<PerfResultsRecord> records)
        {
            if (records == null)
            {
                return Task.CompletedTask;
            }

            foreach (PerfResultsRecord record in records)
            {
                Utility.TeeTraceInformation("PerfResults: " + JsonConvert.SerializeObject(record));
            }

            return Task.CompletedTask;
        }

        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }
    }
}
