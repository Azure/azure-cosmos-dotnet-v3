//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Controls the level of detail in <see cref="CosmosDiagnostics"/> serialized output.
    /// </summary>
    public enum DiagnosticsVerbosity
    {
        /// <summary>
        /// Full diagnostic output with all individual request traces.
        /// This is the default and preserves backward compatibility.
        /// </summary>
        Detailed = 0,

        /// <summary>
        /// Compacted diagnostic output optimized for log size constraints.
        /// Groups requests by region. Keeps first and last request in full detail.
        /// Deduplicates middle requests by (StatusCode, SubStatusCode) with
        /// aggregate statistics (count, total RU, min/max/P50 latency).
        /// </summary>
        Summary = 1,
    }
}
