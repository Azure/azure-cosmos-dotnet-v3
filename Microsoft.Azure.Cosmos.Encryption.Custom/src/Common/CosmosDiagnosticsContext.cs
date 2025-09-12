//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// Lightweight diagnostics context for Custom Encryption extension.
    /// Records scope names, start/stop timestamps and exposes them for tests or future wiring into SDK diagnostics.
    /// Uses <see cref="ActivitySource"/> so downstream telemetry (OpenTelemetry) can optionally subscribe.
    /// </summary>
    internal class CosmosDiagnosticsContext
    {
        private static readonly ActivitySource ActivitySource = new ("Microsoft.Azure.Cosmos.Encryption.Custom");

        private readonly List<ScopeRecord> records = new (4);

        internal CosmosDiagnosticsContext()
        {
        }

        /// <summary>
        /// Factory. A new instance is created per high-level operation to avoid cross-talk.
        /// </summary>
        public static CosmosDiagnosticsContext Create(RequestOptions options)
        {
            _ = options; // Reserved for future correlation if RequestOptions ever carries a diagnostics handle.
            return new CosmosDiagnosticsContext();
        }

        /// <summary>
        /// Recorded scope metadata (immutable snapshot on scope dispose).
        /// </summary>
        internal readonly struct ScopeRecord
        {
            public ScopeRecord(string name, long startTimestamp, long elapsedTicks)
            {
                this.Name = name;
                this.StartTimestamp = startTimestamp;
                this.ElapsedTicks = elapsedTicks;
            }

            public string Name { get; }

            public long StartTimestamp { get; }

            public long ElapsedTicks { get; } // Stopwatch ticks

            public TimeSpan Elapsed => TimeSpan.FromTicks(this.ElapsedTicks);
        }

        /// <summary>
        /// Gets recorded scope names primarily for unit tests (copy snapshot each access).
        /// </summary>
        internal IReadOnlyList<string> Scopes
        {
            get
            {
                if (this.records.Count == 0)
                {
                    return Array.Empty<string>();
                }

                string[] names = new string[this.records.Count];
                for (int i = 0; i < this.records.Count; i++)
                {
                    names[i] = this.records[i].Name;
                }

                return names;
            }
        }

        public Scope CreateScope(string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return Scope.Noop; // returns default(struct) => no-op
            }

            // Only create Activity if there are listeners to avoid unnecessary allocations.
            Activity activity = ActivitySource.HasListeners() ? ActivitySource.StartActivity(scope, ActivityKind.Internal) : null;
            long startTicks = Stopwatch.GetTimestamp();
            return new Scope(this, scope, startTicks, activity);
        }

        private void Record(string name, long startTicks, long elapsedTicks)
        {
            lock (this.records)
            {
                this.records.Add(new ScopeRecord(name, startTicks, elapsedTicks));
            }
        }

        public readonly struct Scope : IDisposable
        {
            private readonly CosmosDiagnosticsContext owner;
            private readonly string name;
            private readonly long startTicks;
            private readonly Activity activity;
            private readonly bool enabled;

            internal Scope(CosmosDiagnosticsContext owner, string name, long startTicks, Activity activity)
            {
                this.owner = owner;
                this.name = name;
                this.startTicks = startTicks;
                this.activity = activity;
                this.enabled = owner != null; // default struct (Noop) => owner null
            }

            internal static Scope Noop => default;

            public void Dispose()
            {
                if (!this.enabled)
                {
                    return;
                }

                long elapsedTicks = Stopwatch.GetTimestamp() - this.startTicks;
                this.owner.Record(this.name, this.startTicks, elapsedTicks);
                this.activity?.Dispose();
            }
        }
    }
}
