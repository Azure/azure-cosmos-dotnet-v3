//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Test.Analytics
{
    using System;
    
    /// <summary>
    /// Represents a performance counter with statistical metrics.
    /// </summary>
    [Serializable]
    public sealed class PerformanceCounter
    {
        /// <summary>
        /// Gets or sets the name of the performance counter.
        /// </summary>
        public string CounterName { get; set; }

        /// <summary>
        /// Gets or sets the average value of the performance counter.
        /// </summary>
        public double Average { get; set; }

        /// <summary>
        /// Gets or sets the 10th percentile value of the performance counter.
        /// </summary>
        public double Percentile10 { get; set; }

        /// <summary>
        /// Gets or sets the 90th percentile value of the performance counter.
        /// </summary>
        public double Percentile90 { get; set; }

        /// <summary>
        /// Gets or sets the 99th percentile value of the performance counter.
        /// </summary>
        public double Percentile99 { get; set; }
    }
}
