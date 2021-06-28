//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Response of Bulk Request
    /// </summary>
    public class BulkRequestOptions : RequestOptions
    {
        internal BulkRequestOptions()
        {
        }

        internal int MaxConcurrencyPerPartition { get; set; }
        internal int MaxPipelinedOperations { get; set; }
        internal int MaxMicroBatchSize { get; set; }
        internal TimeSpan FlushBatchTimeout { get; set; }

    }
}
