//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Sink for connection-manager telemetry. Implementations decide where
    /// the rows go — production uses <see cref="DefaultTraceConnectionManagerTelemetry"/>
    /// which writes to the existing <see cref="DefaultTrace"/> ETW provider,
    /// tests provide an in-memory implementation that captures rows for
    /// assertions.
    /// </summary>
    /// <remarks>
    /// All methods MUST be cheap and non-throwing — the manager fires
    /// telemetry from hot paths and must never let an instrumentation bug
    /// fail an open or an acquire.
    /// </remarks>
    internal interface IRntbdConnectionManagerTelemetry
    {
        /// <summary>
        /// Per-request acquire row.
        /// </summary>
        void LogAcquisition(RntbdConnectionManagerConnectionAcquisitionEvent acquisition);

        /// <summary>
        /// Per-manager lifecycle / decision event.
        /// </summary>
        void LogEvent(RntbdConnectionManagerEvent managerEvent);
    }

    /// <summary>
    /// Default telemetry implementation that writes to <see cref="DefaultTrace"/>.
    /// Both event types serialize to a single grep-friendly line so downstream
    /// Kusto ingestion can parse them with a single regex.
    /// </summary>
    /// <remarks>
    /// Implementations are intentionally tiny — sampling and rate limiting
    /// happen at the call sites (see <c>rntbd-connection-manager-design.md</c>)
    /// so this sink stays a no-op-fast-path emitter.
    /// </remarks>
    internal sealed class DefaultTraceConnectionManagerTelemetry : IRntbdConnectionManagerTelemetry
    {
        public static readonly DefaultTraceConnectionManagerTelemetry Instance =
            new DefaultTraceConnectionManagerTelemetry();

        private DefaultTraceConnectionManagerTelemetry()
        {
        }

        public void LogAcquisition(RntbdConnectionManagerConnectionAcquisitionEvent acquisition)
        {
            if (acquisition == null)
            {
                return;
            }

            try
            {
                DefaultTrace.TraceInformation(acquisition.ToTelemetryLine());
            }
            catch (Exception e)
            {
                // Telemetry must never throw out of the manager. Surface the
                // failure (most likely a ToTelemetryLine() formatting bug) via
                // the message only — never re-render the line — and swallow any
                // secondary trace-sink failure.
                try
                {
                    DefaultTrace.TraceError(
                        "RntbdConnectionManager telemetry (LogAcquisition) failed: {0}", e.Message);
                }
                catch
                {
                }
            }
        }

        public void LogEvent(RntbdConnectionManagerEvent managerEvent)
        {
            if (managerEvent == null)
            {
                return;
            }

            try
            {
                DefaultTrace.TraceInformation(managerEvent.ToTelemetryLine());
            }
            catch (Exception e)
            {
                // Telemetry must never throw out of the manager. Surface the
                // failure (most likely a ToTelemetryLine() formatting bug) via
                // the message only — never re-render the line — and swallow any
                // secondary trace-sink failure.
                try
                {
                    DefaultTrace.TraceError(
                        "RntbdConnectionManager telemetry (LogEvent) failed: {0}", e.Message);
                }
                catch
                {
                }
            }
        }
    }
}
