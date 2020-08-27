//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif

    internal sealed class ChannelOpenTimeline
    {
        private readonly DateTimeOffset creationTime;
        private DateTimeOffset connectTime = DateTimeOffset.MinValue;
        private DateTimeOffset sslHandshakeTime = DateTimeOffset.MinValue;
        private DateTimeOffset rntbdHandshakeTime = DateTimeOffset.MinValue;

        public delegate void ConnectionTimerDelegate(
            Guid activityId,
            string connectionCreationTime,
            string tcpConnectCompleteTime,
            string sslHandshakeCompleteTime,
            string rntbdHandshakeCompleteTime,
            string openTaskCompletionTime);

        public ChannelOpenTimeline()
        {
            this.creationTime = DateTimeOffset.UtcNow;
        }

        public void RecordConnectFinishTime()
        {
            Debug.Assert(this.connectTime == DateTimeOffset.MinValue);
            this.connectTime = DateTimeOffset.UtcNow;
        }

        public void RecordSslHandshakeFinishTime()
        {
            Debug.Assert(this.connectTime != DateTimeOffset.MinValue,
                string.Format("Call {0} first", nameof(RecordConnectFinishTime)));
            Debug.Assert(this.sslHandshakeTime == DateTimeOffset.MinValue);
            this.sslHandshakeTime = DateTimeOffset.UtcNow;
        }

        public void RecordRntbdHandshakeFinishTime()
        {
            Debug.Assert(this.sslHandshakeTime != DateTimeOffset.MinValue,
                string.Format("Call {0} first", nameof(RecordSslHandshakeFinishTime)));
            Debug.Assert(this.rntbdHandshakeTime == DateTimeOffset.MinValue);
            this.rntbdHandshakeTime = DateTimeOffset.UtcNow;
        }

        public void WriteTrace()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ChannelOpenTimeline.TraceFunc?.Invoke(
                Trace.CorrelationManager.ActivityId,
                ChannelOpenTimeline.InvariantString(this.creationTime),
                ChannelOpenTimeline.InvariantString(this.connectTime),
                ChannelOpenTimeline.InvariantString(this.sslHandshakeTime),
                ChannelOpenTimeline.InvariantString(this.rntbdHandshakeTime),
                ChannelOpenTimeline.InvariantString(now));
        }

        // TODO(ovplaton): Delete this function when retiring the old RNTBD stack.
        public static void LegacyWriteTrace(RntbdConnectionOpenTimers timers)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ChannelOpenTimeline.TraceFunc?.Invoke(
                Trace.CorrelationManager.ActivityId,
                ChannelOpenTimeline.InvariantString(timers.CreationTimestamp),
                ChannelOpenTimeline.InvariantString(timers.TcpConnectCompleteTimestamp),
                ChannelOpenTimeline.InvariantString(timers.SslHandshakeCompleteTimestamp),
                ChannelOpenTimeline.InvariantString(timers.RntbdHandshakeCompleteTimestamp),
                ChannelOpenTimeline.InvariantString(now));
        }

        public static ConnectionTimerDelegate TraceFunc { get; set; }

        private static string InvariantString(DateTimeOffset t)
        {
            return t.ToString("o", CultureInfo.InvariantCulture);
        }
    }
}
