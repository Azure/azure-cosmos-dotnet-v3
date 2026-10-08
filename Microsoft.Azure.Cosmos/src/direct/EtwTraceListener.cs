// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Trace
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Text;

    /// <summary>
    /// Listens to trace as emitted by TraceSource and sends them to the ETW subsystem using EventWriteString native call.
    /// Only supported operation is TraceEvent.
    /// </summary>
    /// <inheritdoc />
    internal sealed class EtwTraceListener : TraceListener
    {
        /// <summary>
        /// Max ETW event length for EventWriteString.
        /// Formula = 64KB / sizeof(char)(2) - sizeof(ETW header)(72) - unknown factors ~= 32500 char.
        /// The ETW subsystem won't log anything longer than 64KB total.
        /// Assuming that logging 32500 chars is preferable to 0, any message exceeding that is truncated.
        /// </summary>
        public const int MaxEtwEventLength = 32500;

        // ETW keyword bits — mirror Microsoft.Azure.Cosmos.Core.Trace.TraceConstants
        // and the native TraceConstants::EventKeyword enum
        // (Product/Backend/native/common/Trace/TraceConstants.h). These are defined
        // locally (rather than referencing TraceConstants) because this file is also
        // cherry-picked as source into the SDK projects
        // (Microsoft.Azure.Cosmos.Direct / Microsoft.Azure.Documents.Client), which
        // do NOT link TraceConstants.cs. A non-zero base keyword (NonMetric) is
        // required so the ETL session's matchAllKeywords=0x8 filter can exclude
        // non-Verbose events — ETW passes any event whose keyword is 0 through
        // keyword filters unconditionally. Verbose(+) events additionally carry
        // EtlEmit so they are written to the .etl file, while non-Verbose events
        // (levels 1-4) omit EtlEmit yet keep a non-zero keyword so they still reach
        // Kusto via the warm path.
        private const long NonMetricKeyword = 0x1;   // EventKeyword_NonMetric (1 << 0)
        private const long EtlEmitKeyword = 0x8;      // EventKeyword_EtlEmit   (1 << 3)

        /// <summary>
        /// Federation-level feature flag mirrored from the server's
        /// <c>emitNonVerboseEventsToEtl</c> configuration. When true, non-Verbose
        /// events (levels 1-4) are additionally stamped with the EtlEmit keyword
        /// so they are also written to ETL files (they still reach Kusto via the
        /// warm path). Verbose(+) events always go to ETL regardless of this flag.
        /// <para>
        /// Defaults to <c>false</c> so cherry-picked SDK builds
        /// (Microsoft.Azure.Cosmos.Direct / Microsoft.Azure.Documents.Client)
        /// preserve the existing behavior. The server host sets this once at
        /// startup from the federation configuration; it is process-wide because
        /// the setting is federation/tenant-scoped, not per-request.
        /// </para>
        /// </summary>
        internal static bool EmitNonVerboseEventsToEtl { get; set; }

        /// <summary>
        /// Writes an event to the ETW subsystem. The parameters mirror the
        /// relevant arguments of <see cref="EtwNativeInterop.EventWriteString"/>
        /// (the provider handle is captured per instance). This is a seam so unit
        /// tests can capture the (level, keyword, message) each event is emitted
        /// with — without a real <c>TraceEventSession</c>, which requires
        /// administrator privileges unavailable in gated pipelines.
        /// </summary>
        /// <param name="level">ETW level, encoded as <c>(byte)TraceEventType</c>.</param>
        /// <param name="keyword">ETW keyword bitmask.</param>
        /// <param name="message">Event message.</param>
        /// <returns>ETW return code (0 on success).</returns>
        internal delegate uint EtwWriteString(byte level, long keyword, string message);

        /// <summary>
        /// Registration handle as provided by ETW RegisterEvent call.
        /// </summary>
        private readonly EtwNativeInterop.ProviderHandle providerHandle = new EtwNativeInterop.ProviderHandle();

        /// <summary>
        /// The sink used to emit events to ETW. In production this forwards to
        /// <see cref="EtwNativeInterop.EventWriteString"/>; unit tests inject a
        /// capturing delegate via the test constructor.
        /// </summary>
        private readonly EtwWriteString etwWrite;

        /// <summary>
        /// Initializes a new instance of the <see cref="EtwTraceListener"/> class.
        /// </summary>
        /// <param name="providerGuid">ETW provider guid.</param>
        /// <param name="name">Trace listener name (unrelated to ETW).</param>
        public EtwTraceListener(Guid providerGuid, string name)
            : base(name)
        {
            this.ProviderGuid = providerGuid;

            uint retVal = EtwNativeInterop.EventRegister(providerGuid, IntPtr.Zero, IntPtr.Zero, ref this.providerHandle);

            if (retVal != 0)
            {
                throw new Win32Exception((int)retVal);
            }

            this.etwWrite = (level, keyword, message) =>
                EtwNativeInterop.EventWriteString(this.providerHandle, level, keyword, message);
        }

        /// <summary>
        /// Test-only constructor. Injects a custom ETW writer and does NOT register
        /// a real ETW provider, so unit tests can capture emitted
        /// (level, keyword, message) tuples without a <c>TraceEventSession</c>
        /// (which requires administrator privileges). Do not use in production.
        /// </summary>
        /// <param name="name">Trace listener name (unrelated to ETW).</param>
        /// <param name="etwWrite">Sink invoked in place of the native ETW write.</param>
        internal EtwTraceListener(string name, EtwWriteString etwWrite)
            : base(name)
        {
            this.etwWrite = etwWrite ?? throw new ArgumentNullException(nameof(etwWrite));
        }

        /// <summary>
        /// ETW provider guid.
        /// </summary>
        public Guid ProviderGuid { get; }

        /// <inheritdoc />
        public override bool IsThreadSafe { get; } = true;

        /// <inheritdoc />
        public override void Close()
        {
            this.Dispose();
            base.Close();
        }

        /// <summary>
        /// Unregister ETW handle.
        /// </summary>
        /// <param name="disposing">Unused.</param>
        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (this.providerHandle != null && !this.providerHandle.IsInvalid)
            {
                this.providerHandle.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override void TraceEvent(
            TraceEventCache eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string format,
            params object[] args)
        {
            if (this.IsFiltered(eventCache, source, eventType, id))
            {
                return;
            }

            string finalMessage = format;

            if (args != null && args.Length > 0)
            {
                StringBuilder sb = StringBuilderCache.Instance;
                sb.AppendFormat(format, args);

                // ETW subsystem won't log anything that exceeds buffer size. Ensure data fits.
                if (sb.Length > EtwTraceListener.MaxEtwEventLength)
                {
                    sb.Remove(EtwTraceListener.MaxEtwEventLength, sb.Length - EtwTraceListener.MaxEtwEventLength);
                }

                finalMessage = sb.ToString();
            }

            this.TraceInternal(eventType, finalMessage);
        }

        /// <inheritdoc />
        public override void TraceEvent(
            TraceEventCache eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string message)
        {
            if (this.IsFiltered(eventCache, source, eventType, id))
            {
                return;
            }

            // ETW subsystem won't log anything that exceeds buffer size. Ensure data fits.
            if (message.Length > EtwTraceListener.MaxEtwEventLength)
            {
                message = message.Remove(EtwTraceListener.MaxEtwEventLength, message.Length - EtwTraceListener.MaxEtwEventLength);
            }

            this.TraceInternal(eventType, message);
        }

        private void TraceInternal(TraceEventType eventType, string message)
        {
            // All trace levels are emitted to ETW. The event carries a keyword
            // that determines whether it is written to the .etl file: Verbose(+)
            // events get the EtlEmit bit and are captured by the ETL session
            // (matchAllKeywords=0x8); non-Verbose events (levels 1-4) omit
            // EtlEmit and are filtered out of ETL, but still carry a non-zero
            // keyword so they reach Kusto via the warm path. Filtering must NOT
            // happen here at the producer — dropping non-Verbose events would
            // also starve the warm-path consumer. When the federation-level
            // feature flag EmitNonVerboseEventsToEtl is enabled, non-Verbose
            // events are ALSO stamped with EtlEmit so they are additionally
            // written to ETL files (they still reach Kusto).
            long keyword = (eventType == TraceEventType.Verbose || EmitNonVerboseEventsToEtl)
                ? (NonMetricKeyword | EtlEmitKeyword)
                : NonMetricKeyword;

            // Discard return value in release mode - errors are not handled.
#if DEBUG
            this.LastReturnCode = this.etwWrite((byte)eventType, keyword, message);
#else
            _ = this.etwWrite((byte)eventType, keyword, message);
#endif
        }

        /// <summary>
        /// Error code returned by latest ETW call.
        /// </summary>
        internal uint LastReturnCode { get; private set; }

        /// <summary>
        /// Should trace be filtered out.
        /// </summary>
        /// <param name="eventCache">Event cache.</param>
        /// <param name="source">Source.</param>
        /// <param name="eventType">Event type.</param>
        /// <param name="id">Id.</param>
        /// <returns>True if filtered out, false if trace should go through.</returns>
        private bool IsFiltered(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            return this.Filter != null &&
                !this.Filter.ShouldTrace(
                    cache: eventCache,
                    source: source,
                    eventType: eventType,
                    id: id,
                    formatOrMessage: null,
                    args: null,
                    data1: null,
                    data: null);
        }

        /// <summary>
        /// Traces the message with information level trace.
        /// </summary>
        public override void Write(string message)
        {
            // ETW subsystem won't log anything that exceeds buffer size. Ensure data fits.
            if (message.Length > EtwTraceListener.MaxEtwEventLength)
            {
                message = message.Remove(EtwTraceListener.MaxEtwEventLength, message.Length - EtwTraceListener.MaxEtwEventLength);
            }

            this.TraceInternal(TraceEventType.Information, message);
        }

        /// <summary>
        /// Traces the message with information level trace.
        /// </summary>
        public override void WriteLine(string message) => this.Write(message);

        /// <summary>
        /// Thread static cache for pre-allocated string builder.
        /// </summary>
        private static class StringBuilderCache
        {
            /// <summary>
            /// This is the same value as StringBuilder.MaxChunkSize.
            /// Avoid needless buffer fragmentation and memory usage remains reasonable.
            /// </summary>
            private const int MaxBuilderSize = 8000;

            [ThreadStatic]
            private static StringBuilder cachedInstance;

            /// <summary>
            /// Get cached string builder or allocate if none.
            /// </summary>
            /// <returns>String builder.</returns>
            public static StringBuilder Instance
            {
                get
                {
                    if (StringBuilderCache.cachedInstance == null)
                    {
                        StringBuilderCache.cachedInstance = new StringBuilder(StringBuilderCache.MaxBuilderSize);
                    }

                    StringBuilderCache.cachedInstance.Clear();
                    return StringBuilderCache.cachedInstance;
                }
            }
        }
    }
}