//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;

    /// <summary>
    /// This represents a single scope in the diagnostics.
    /// A scope is a section of code that is important to track.
    /// For example there is a scope for serialization, retry handlers, etc..
    /// </summary>
    internal class CosmosDiagnosticScope : CosmosDiagnosticWriter, IDisposable
    {
        private readonly string Id;

        private readonly Stopwatch ElapsedTimeStopWatch;

        private readonly Action<TimeSpan> ElapsedTimeCallback;

        internal TimeSpan? ElapsedTime { get; private set; }

        private bool isDisposed = false;
        
        internal CosmosDiagnosticScope(
            string name,
            Action<TimeSpan> elapsedTimeCallback = null)
        {
            this.Id = name;
            this.ElapsedTimeStopWatch = Stopwatch.StartNew();
            this.ElapsedTime = null;
            this.ElapsedTimeCallback = elapsedTimeCallback;
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.ElapsedTimeStopWatch.Stop();
            this.ElapsedTime = this.ElapsedTimeStopWatch.Elapsed;
            this.ElapsedTimeCallback?.Invoke(this.ElapsedTime.Value);
            this.isDisposed = true;
        }

        internal override void WriteJsonObject(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"Id\":\"");
            stringBuilder.Append(this.Id);
            stringBuilder.Append("\",\"ElapsedTime\":\"");
            if (this.ElapsedTime.HasValue)
            {
                stringBuilder.Append(this.ElapsedTime.Value);
            }
            else
            {
                stringBuilder.Append("NoElapsedTime");
            }

            stringBuilder.Append("\"}");
        }
    }
}
