// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Cosmos Instrumentation Interface
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif 
        abstract class IOpenTelemetryRecorder : IDisposable
    {
        /// <summary>
        /// Recording Attributes
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public abstract void Record(string key, string value);

        /// <summary>
        /// Recording attributes from response
        /// </summary>
        /// <param name="response">OpenTelemetryResponse</param>
        public abstract void Record(OpenTelemetryResponse response);

        /// <summary>
        /// Recording Request Diagnostics
        /// </summary>
        /// <param name="trace"></param>
        public abstract void Record(ITrace trace);

        /// <summary>
        /// Mark Scope as failed and add exceptions in attribute
        /// </summary>
        /// <param name="exception"></param>
        public abstract void MarkFailed(Exception exception);

        public abstract bool IsEnabled { get; }

        /// <summary>
        /// Dispose open telemetry recorder
        /// </summary>
        public abstract void Dispose();
    }
}
