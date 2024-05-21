//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Test.Analytics
{
    using System;

    [Serializable]
    public sealed class PerformanceCounter
    {
        public string CounterName { get; set; }

        public double Average { get; set; }

        public double Percentile10 { get; set; }

        public double Percentile90 { get; set; }

        public double Percentile99 { get; set; }
    }
}
