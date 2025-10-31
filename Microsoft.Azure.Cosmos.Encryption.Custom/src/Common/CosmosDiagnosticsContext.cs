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
    /// Records scope names, start/stop timestamps, and parent-child relationships for nested scopes.
    /// Exposes scope records for tests or future wiring into SDK diagnostics.
    /// Uses <see cref="ActivitySource"/> so downstream telemetry (OpenTelemetry) can optionally subscribe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Nested Scope Support:</strong> This implementation fully supports hierarchical scope tracking.
    /// When a scope is created while another scope is active, the parent-child relationship is captured
    /// in <see cref="ScopeRecord.ParentName"/>. This enables analysis of timing attribution and call hierarchies.
    /// </para>
    /// <para>
    /// For example, nested scopes like <c>Outer { Inner1 { } Inner2 { } }</c> will be recorded with
    /// Inner1.ParentName = "Outer" and Inner2.ParentName = "Outer", allowing reconstruction of the call tree.
    /// </para>
    /// <para>
    /// Parent-child relationships are captured at scope creation time using a stack to track the currently active scope.
    /// Each <see cref="CosmosDiagnosticsContext"/> instance is intended for use within a single operation flow and is
    /// not designed for concurrent access across parallel tasks.
    /// </para>
    /// </remarks>
    internal class CosmosDiagnosticsContext
    {
        /// <summary>
        /// Scope name prefix for MDE (Microsoft.Data.Encryption) encrypt operations.
        /// Concatenate with the JSON processor name to create the full scope name.
        /// </summary>
        /// <example>
        /// Usage: <c>CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + "Stream"</c>
        /// produces: <c>"EncryptionProcessor.Encrypt.Mde.Stream"</c>
        /// </example>
        internal const string ScopeEncryptModeSelectionPrefix = "EncryptionProcessor.Encrypt.Mde.";

        /// <summary>
        /// Scope name prefix for MDE (Microsoft.Data.Encryption) decrypt operations.
        /// Concatenate with the JSON processor name to create the full scope name.
        /// </summary>
        /// <example>
        /// Usage: <c>CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + "Newtonsoft"</c>
        /// produces: <c>"EncryptionProcessor.Decrypt.Mde.Newtonsoft"</c>
        /// </example>
        internal const string ScopeDecryptModeSelectionPrefix = "EncryptionProcessor.Decrypt.Mde.";

        private static readonly ActivitySource ActivitySource = new ("Microsoft.Azure.Cosmos.Encryption.Custom");

        private readonly List<ScopeRecord> records = new (4);
        private readonly Stack<string> scopeStack = new ();

        internal CosmosDiagnosticsContext()
        {
        }

        /// <summary>
        /// Factory. A new instance is created per high-level operation to avoid cross-talk.
        /// </summary>
        public static CosmosDiagnosticsContext Create(RequestOptions options)
        {
            _ = options;
            return new CosmosDiagnosticsContext();
        }

        /// <summary>
        /// Recorded scope metadata (immutable snapshot on scope dispose).
        /// </summary>
        internal readonly struct ScopeRecord
        {
            public ScopeRecord(string name, long startTimestamp, long elapsedTicks, string parentName)
            {
                this.Name = name;
                this.StartTimestamp = startTimestamp;
                this.ElapsedTicks = elapsedTicks;
                this.ParentName = parentName;
            }

            public string Name { get; }

            public long StartTimestamp { get; }

            public long ElapsedTicks { get; } // Stopwatch ticks

            public string ParentName { get; }

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
        /// Gets full scope records including parent relationships and timing data.
        /// Primarily for unit tests to verify nested scope hierarchies (copy snapshot each access).
        /// </summary>
        internal IReadOnlyList<ScopeRecord> ScopeRecords
        {
            get
            {
                lock (this.records)
                {
                    return this.records.ToArray();
                }
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
        /// Nested scopes are fully supported. When a scope is created while another scope is active,
        /// the parent-child relationship is captured in the <see cref="ScopeRecord.ParentName"/> property.
        /// Each scope is recorded when it is disposed, in LIFO order for nested scopes.
        /// </remarks>
        public Scope CreateScope(string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return Scope.Noop;
            }

            string parentName;
            lock (this.scopeStack)
            {
                parentName = this.scopeStack.Count > 0 ? this.scopeStack.Peek() : null;
                this.scopeStack.Push(scope);
            }

            Activity activity = ActivitySource.HasListeners() ? ActivitySource.StartActivity(scope, ActivityKind.Internal) : null;
            Scope newScope = new Scope(this, scope, activity, parentName);

            return newScope;
        }

        private void Record(string name, long startTicks, long elapsedTicks, string parentName)
        {
            lock (this.records)
            {
                this.records.Add(new ScopeRecord(name, startTicks, elapsedTicks, parentName));
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
            private readonly string scopeName;
            private readonly long startTicks;
            private readonly Activity activity;
            private readonly string parentName;

            internal string Name => this.scopeName;

            internal Scope(CosmosDiagnosticsContext owner, string name, Activity activity, string parentName)
            {
                ArgumentValidation.ThrowIfNull(owner, nameof(owner));
                ArgumentValidation.ThrowIfNullOrEmpty(name, nameof(name));

                this.owner = owner;
                this.scopeName = name;
                this.startTicks = Stopwatch.GetTimestamp();
                this.activity = activity;
                this.parentName = parentName;
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
                long elapsedTicks = Stopwatch.GetTimestamp() - this.startTicks;

                this.owner.Record(this.scopeName, this.startTicks, elapsedTicks, this.parentName);

                this.activity?.Dispose();

                lock (this.owner.scopeStack)
                {
                    if (this.owner.scopeStack.Count > 0 && this.owner.scopeStack.Peek() == this.scopeName)
                    {
                        this.owner.scopeStack.Pop();
                    }
                }
            }
        }
    }
}
