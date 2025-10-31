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
    /// <remarks>
    /// <para>
    /// <strong>Limitation:</strong> This implementation does not capture hierarchical relationships between nested scopes.
    /// All scopes are recorded in a flat list in the order they are disposed (LIFO order for nested scopes).
    /// </para>
    /// <para>
    /// For example, nested scopes like <c>Outer { Inner1 { } Inner2 { } }</c> will be recorded as
    /// <c>[Inner1, Inner2, Outer]</c> without parent-child relationships.
    /// </para>
    /// <para>
    /// For hierarchical span tracking, consider using <see cref="ActivitySource"/>/<see cref="Activity"/> directly
    /// or a structured logging framework that supports scope nesting.
    /// </para>
    /// </remarks>
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

        /// <summary>
        /// Creates a new diagnostic scope for timing an operation.
        /// </summary>
        /// <param name="scope">
        /// The name of the scope. If null or empty, returns a no-op scope that performs no recording.
        /// </param>
        /// <returns>
        /// A <see cref="Scope"/> that records timing information when disposed.
        /// Use with a <c>using</c> statement to ensure proper disposal.
        /// If <paramref name="scope"/> is null or empty, returns <see cref="Scope.Noop"/> which has a null owner
        /// and performs no recording (zero-allocation no-op pattern).
        /// </returns>
        /// <remarks>
        /// Note: Nested scopes are recorded independently in a flat list without capturing parent-child relationships.
        /// Each scope is recorded when it is disposed, resulting in LIFO order for nested scopes.
        /// </remarks>
        public Scope CreateScope(string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return Scope.Noop; // Returns default(Scope) with null owner - this is intentional for no-op pattern
            }

            // Only create Activity if there are listeners to avoid unnecessary allocations.
            Activity activity = ActivitySource.HasListeners() ? ActivitySource.StartActivity(scope, ActivityKind.Internal) : null;
            return new Scope(this, scope, activity);
        }

        private void Record(string name, long startTicks, long elapsedTicks)
        {
            lock (this.records)
            {
                this.records.Add(new ScopeRecord(name, startTicks, elapsedTicks));
            }
        }

        /// <summary>
        /// Represents a diagnostic scope for timing operations and Activity tracking.
        /// IMPORTANT: This struct should ONLY be used with the 'using' pattern to ensure
        /// single disposal. Do not manually copy this struct as it contains a reference
        /// to an Activity object that will be disposed when this scope is disposed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// While Activity.Dispose() is idempotent (safe to call multiple times), the intended
        /// usage pattern is single disposal via 'using' statement. Copying the struct and
        /// disposing multiple copies is not recommended, even though it is technically safe.
        /// </para>
        /// <para>
        /// <strong>Null Owner Design:</strong> A default-initialized Scope (via <see cref="Noop"/>) has a null owner
        /// and acts as a no-op. This is an intentional design to avoid allocations when diagnostics are disabled
        /// or when the scope name is null/empty. The <c>enabled</c> flag is set to <c>false</c> when owner is null,
        /// causing <see cref="Dispose"/> to return early without any operations.
        /// </para>
        /// </remarks>
        public readonly struct Scope : IDisposable
        {
            private readonly CosmosDiagnosticsContext owner;
            private readonly string name;
            private readonly long startTicks;
            private readonly Activity activity;
            private readonly bool enabled;

            internal Scope(CosmosDiagnosticsContext owner, string name, Activity activity)
            {
                this.owner = owner;
                this.name = name;
                this.startTicks = Stopwatch.GetTimestamp(); // Capture start time in constructor for better accuracy
                this.activity = activity;
                this.enabled = owner != null; // default struct (Noop) => owner null
            }

            /// <summary>
            /// Gets a no-op scope that performs no recording when disposed.
            /// </summary>
            /// <remarks>
            /// This returns <c>default(Scope)</c> which has a null owner. The <see cref="Dispose"/> method
            /// safely handles this case by checking the <c>enabled</c> flag and returning early.
            /// This pattern avoids allocations for disabled scopes or empty scope names.
            /// </remarks>
            internal static Scope Noop => default;

            public void Dispose()
            {
                if (!this.enabled)
                {
                    return;
                }

                long elapsedTicks = Stopwatch.GetTimestamp() - this.startTicks;

                // Defensive null check - should never be null if enabled=true,
                // but guards against struct manipulation bugs
                this.owner?.Record(this.name, this.startTicks, elapsedTicks);

                // Activity.Dispose() is idempotent per .NET framework documentation,
                // so multiple calls are safe (though not the intended usage pattern).
                // Null-conditional ensures we skip disposal if activity was never created.
                this.activity?.Dispose();
            }
        }
    }
}
