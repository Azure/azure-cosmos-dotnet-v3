//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;

    internal class CosmosDiagnosticTimer : IDisposable
    {
        private Stopwatch timer;

        internal CosmosDiagnosticTimer()
        {
            this.timer = new Stopwatch();
            this.timer.Start();
        }

        internal TimeSpan GetElapsedTime()
        {
            return this.timer.Elapsed;
        }

        public void Dispose()
        {
            this.timer.Stop();
        }
    }
}
