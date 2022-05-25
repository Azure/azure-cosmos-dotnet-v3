// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Trace
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;

    internal static class DefaultTrace
    {
        public static readonly Guid ProviderId = new Guid("{B30ABF1C-6A50-4F2B-85C4-61823ED6CF24}");

        private static readonly TraceSource TraceSourceInternal;

        private static bool IsListenerAdded;

        static DefaultTrace()
        {
            // From MSDN: http://msdn.microsoft.com/en-us/library/system.diagnostics.trace.usegloballock%28v=vs.110%29.aspx
            // The global lock is always used if the trace listener is not thread safe,
            // regardless of the value of UseGlobalLock. The IsThreadSafe property is used to determine
            // if the listener is thread safe. The global lock is not used only if the value of
            // UseGlobalLock is false and the value of IsThreadSafe is true.
            // The default behavior is to use the global lock.
            System.Diagnostics.Trace.UseGlobalLock = false;

            DefaultTrace.TraceSourceInternal = new TraceSource("DocDBTrace");

            // The DefaultTraceListener can cause lock contention which leads to request failures.
            // Remove the DefaultTraceListener unless a debugger is attached.
            DefaultTrace.RemoveDefaultTraceListener();
        }

        public static TraceSource TraceSource
        {
            get { return DefaultTrace.TraceSourceInternal; }
        }

        /// <summary>
        /// Only client need to init this listener.
        /// </summary>
        public static void InitEventListener()
        {
            if (DefaultTrace.IsListenerAdded)
            {
                return;
            }

            DefaultTrace.IsListenerAdded = true;

#if !NETSTANDARD16
            SourceSwitch sourceSwitch = new SourceSwitch("ClientSwitch", "Information");
            DefaultTrace.TraceSourceInternal.Switch = sourceSwitch;

#if !COSMOSCLIENT
#if NETSTANDARD2_0
            // ETW is a Windows-only feature.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            TraceListener listener = new EtwTraceListener(DefaultTrace.ProviderId, "DocDBClientListener");
#else
            TraceListener listener = new System.Diagnostics.Eventing.EventProviderTraceListener(DefaultTrace.ProviderId.ToString(), "DocDBClientListener", "::");

            DefaultTrace.TraceSourceInternal.Listeners.Add(listener);
#endif // NETSTANDARD2_0
#endif // !COSMOSCLIENT
#endif // NETSTANDARD16
        }

        public static void Flush()
        {
            DefaultTrace.TraceSource.Flush();
        }

        public static void TraceVerbose(string message)
        {
            DefaultTrace.TraceSource.TraceEvent(TraceEventType.Verbose, 0, message);
        }

        public static void TraceVerbose(string format, params object[] args)
        {
            DefaultTrace.TraceSource.TraceEvent(TraceEventType.Verbose, 0, format, args);
        }

        public static void TraceInformation(string message)
        {
            DefaultTrace.TraceSource.TraceInformation(message);
        }

        public static void TraceInformation(string format, params object[] args)
        {
            DefaultTrace.TraceSource.TraceInformation(format, args);
        }

        public static void TraceWarning(string message)
        {
            DefaultTrace.TraceSource.TraceEvent(TraceEventType.Warning, 0, message);
        }

        public static void TraceWarning(string format, params object[] args)
        {
            DefaultTrace.TraceSource.TraceEvent(TraceEventType.Warning, 0, format, args);
        }

        public static void TraceError(string message)
        {
            DefaultTrace.TraceSource.TraceEvent(TraceEventType.Error, 0, message);
        }

        public static void TraceError(string format, params object[] args)
        {
            DefaultTrace.TraceSource.TraceEvent(TraceEventType.Error, 0, format, args);
        }

        public static void TraceCritical(string message)
        {
            DefaultTrace.TraceSource.TraceEvent(TraceEventType.Critical, 0, message);
        }

        public static void TraceCritical(string format, params object[] args)
        {
            DefaultTrace.TraceSource.TraceEvent(TraceEventType.Critical, 0, format, args);
        }

        /// <summary>
        /// Removes the DefaultTraceListener which causes locking issues 
        /// </summary>
        public static void RemoveDefaultTraceListener()
        {
            if (Debugger.IsAttached)
            {
                return;
            }

            if (DefaultTrace.TraceSource.Listeners.Count > 0)
            {
                List<DefaultTraceListener> removeDefaultTraceListeners = new List<DefaultTraceListener>();
                foreach (object traceListenerObject in DefaultTrace.TraceSource.Listeners)
                {
                    if (traceListenerObject is DefaultTraceListener defaultTraceListener)
                    {
                        removeDefaultTraceListeners.Add(defaultTraceListener);
                    }
                }

                // Remove all the default trace listeners
                foreach (DefaultTraceListener defaultTraceListener in removeDefaultTraceListeners)
                {
                    DefaultTrace.TraceSource.Listeners.Remove(defaultTraceListener);
                }
            }
        }

        /// <summary>
        /// Emit a trace for a set of metric values.
        /// This is intended to be used next to MDM metrics
        /// Details:
        /// Produce a semi-typed trace format as a pipe delimited list of metrics values.
        /// 'TraceMetrics' prefix provides a search term for indexing.
        /// 'name' is an identifier to correlate to call site
        /// Example: TraceMetric|LogServicePoolInfo|0|123|1.
        /// </summary>
        /// <param name="name">metric name.</param>
        /// <param name="values">sequence of values to be emitted in the trace.</param>
        internal static void TraceMetrics(string name, params object[] values)
        {
            DefaultTrace.TraceInformation(string.Join("|", new object[] { "TraceMetrics", name }.Concat(values)));
        }
    }
}
