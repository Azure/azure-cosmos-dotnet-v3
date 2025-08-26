//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// Lightweight diagnostics context to optionally collect metrics in custom encryption flows.
    /// If unused, callers may still pass a default instance; all operations are no-ops by design.
    /// </summary>
    internal class CosmosDiagnosticsContext
    {
        private static readonly CosmosDiagnosticsContext UnusedSingleton = new ();
        private static readonly IDisposable UnusedScopeSingleton = new Scope();

        // Simple metric bag for counters and durations (best-effort; thread-safe and optional)
        private readonly ConcurrentDictionary<string, long> metrics = new (StringComparer.Ordinal);

        public static CosmosDiagnosticsContext Create(RequestOptions options)
        {
            _ = options;
            return CosmosDiagnosticsContext.UnusedSingleton;
        }

        public IDisposable CreateScope(string scope)
        {
            // Scope is a no-op placeholder to preserve existing call patterns.
            _ = scope;
            return CosmosDiagnosticsContext.UnusedScopeSingleton;
        }

        // Record or update a metric with an absolute value.
        public void SetMetric(string name, long value)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            this.metrics[name] = value;
        }

        // Increment a metric by delta (can be negative).
        public void AddMetric(string name, long delta = 1)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            this.metrics.AddOrUpdate(name, delta, (_, current) => current + delta);
        }

        // Snapshot of current metrics (intended for diagnostics/testing only).
        public IReadOnlyDictionary<string, long> GetMetricsSnapshot()
        {
            return new Dictionary<string, long>(this.metrics);
        }

        private class Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
