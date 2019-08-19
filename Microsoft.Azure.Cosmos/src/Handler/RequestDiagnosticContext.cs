//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    internal class RequestDiagnosticContext
    {
        private object lockObject = new object();

        internal List<Tuple<string, TimeSpan>> customHandlerLatency { get; set; }

        internal DateTime? requestStartTime { get; set; }

        internal DateTime? requestEndTime { get; set; }

        internal DateTime? handlerRequestStartTime { get; set; }

        internal DateTime? handlerRequestEndTime { get; set; }

        internal string currentHandlerName { get; set; }

        internal TimeSpan costOfFetchingPK { get; set; }

        internal RequestDiagnosticContext()
        {
            customHandlerLatency = new List<Tuple<string, TimeSpan>>();
            this.requestStartTime = DateTime.UtcNow;
            this.requestEndTime = DateTime.UtcNow;
        }

        internal TimeSpan deserializationLatencyinMS { get; set; }

        internal TimeSpan serializationLatencyinMS { get; set; }

        internal bool isCustomSerialization { get; set; }

        internal void updateRequestEndTime()
        {
            DateTime responseTime = DateTime.UtcNow;
            lock (this.lockObject)
            {
                if (responseTime > this.requestEndTime)
                {
                    this.requestEndTime = responseTime;
                }
            }
        }
    }
}
