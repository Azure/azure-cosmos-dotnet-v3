﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    internal sealed class Metrics
    {
        private ValueStopwatch stopwatch;

        public Metrics()
        {
            this.stopwatch = new ValueStopwatch();
        }

        public int Count
        {
            get;
            private set;
        }

        public long ElapsedMilliseconds
        {
            get
            {
                return this.stopwatch.ElapsedMilliseconds;
            }
        }

        public double AverageElapsedMilliseconds
        {
            get
            {
                return (double)this.ElapsedMilliseconds / this.Count;
            }
        }

        public void Start()
        {
            this.stopwatch.Start();
        }

        public void Stop()
        {
            this.stopwatch.Stop();
            ++this.Count;
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Total time (ms): {0}, Count: {1}, Average Time (ms): {2}",
                this.ElapsedMilliseconds,
                this.Count,
                this.AverageElapsedMilliseconds);
        }
    }
}
