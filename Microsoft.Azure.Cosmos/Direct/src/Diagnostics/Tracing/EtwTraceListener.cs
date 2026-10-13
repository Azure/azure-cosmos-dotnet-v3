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

        /// <summary>
        /// Registration handle as provided by ETW RegisterEvent call.
        /// </summary>
        private readonly EtwNativeInterop.ProviderHandle providerHandle = new EtwNativeInterop.ProviderHandle();

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
            // Discard return value in release mode - errors are not handled.
#if DEBUG
            this.LastReturnCode = EtwNativeInterop.EventWriteString(this.providerHandle, (byte)eventType, 0, message);
#else
            _ = EtwNativeInterop.EventWriteString(this.providerHandle, (byte)eventType, 0, message);
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