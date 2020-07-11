//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    class RunSummary
    {
        public string Pk { get; set; } = "RunSummary";

        public string id { get; set; }
        public string Commit { get; set; }
        public string Remarks { get; set; }
        public double Top10PercentAverage { get; set; }
        public double Top20PercentAverage { get; set; }
        public double Top30PercentAverage { get; set; }
        public double Top40PercentAverage { get; set; }
        public double Top50PercentAverage { get; set; }
        public double Top60PercentAverage { get; set; }
        public double Top70PercentAverage { get; set; }
        public double Top80PercentAverage { get; set; }
        public double Top90PercentAverage { get; set; }
        public double Top95PercentAverage { get; set; }
        public double Average { get; set; }
    }
}
