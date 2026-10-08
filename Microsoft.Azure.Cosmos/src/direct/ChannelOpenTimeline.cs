//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;
    using System.Globalization;

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

        public static ConnectionTimerDelegate TraceFunc { get; set; }

        // Per-stage accessors used by RntbdConnectionManager when emitting
        // OpenCompleted / OpenFailed events. These mirror the values
        // already captured during InitializeAsync — they are read-only and
        // safe to call after the open has completed (success or failure).
        public DateTimeOffset CreationTime => this.creationTime;

        public DateTimeOffset? ConnectTime =>
            this.connectTime == DateTimeOffset.MinValue ? null : this.connectTime;

        public DateTimeOffset? SslHandshakeTime =>
            this.sslHandshakeTime == DateTimeOffset.MinValue ? null : this.sslHandshakeTime;

        public DateTimeOffset? RntbdHandshakeTime =>
            this.rntbdHandshakeTime == DateTimeOffset.MinValue ? null : this.rntbdHandshakeTime;

        /// <summary>Duration of the TCP connect phase; null if it never completed.</summary>
        public TimeSpan? TcpConnectDuration =>
            this.ConnectTime is DateTimeOffset c ? c - this.creationTime : null;

        /// <summary>Duration of the SSL/TLS handshake phase; null if it never completed.</summary>
        public TimeSpan? SslHandshakeDuration =>
            this.SslHandshakeTime is DateTimeOffset s && this.ConnectTime is DateTimeOffset c2
                ? s - c2
                : null;

        /// <summary>Duration of the RNTBD context negotiation phase; null if it never completed.</summary>
        public TimeSpan? RntbdHandshakeDuration =>
            this.RntbdHandshakeTime is DateTimeOffset r && this.SslHandshakeTime is DateTimeOffset s2
                ? r - s2
                : null;

        private static string InvariantString(DateTimeOffset t)
        {
            return t.ToString("o", CultureInfo.InvariantCulture);
        }
    }
}
