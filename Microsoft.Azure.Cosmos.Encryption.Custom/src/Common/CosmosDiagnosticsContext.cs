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
    /// Manages Activity creation for OpenTelemetry integration.
    /// </summary>
    internal class CosmosDiagnosticsContext
    {
        private static readonly ActivitySource ActivitySource = new ("Microsoft.Azure.Cosmos.Encryption.Custom");

        private readonly HashSet<int> disposedScopeIds = new HashSet<int>();
        private int nextScopeId = 0;

        /// <summary>
        /// Scope name prefix for MDE (Microsoft.Data.Encryption) encrypt operations.
        /// </summary>
        internal const string ScopeEncryptModeSelectionPrefix = "EncryptionProcessor.Encrypt.Mde.";

        /// <summary>
        /// Scope name prefix for MDE (Microsoft.Data.Encryption) decrypt operations.
        /// </summary>
        internal const string ScopeDecryptModeSelectionPrefix = "EncryptionProcessor.Decrypt.Mde.";

        internal CosmosDiagnosticsContext()
        {
        }

        /// <summary>
        /// Creates a new diagnostics context instance.
        /// </summary>
        public static CosmosDiagnosticsContext Create(RequestOptions options)
        {
            _ = options;
            return new CosmosDiagnosticsContext();
        }

        /// <summary>
        /// Creates a new diagnostic scope for Activity tracking.
        /// </summary>
        /// <param name="scope">The name of the scope.</param>
        /// <returns>A <see cref="Scope"/> that manages an Activity lifecycle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="scope"/> is empty or whitespace.</exception>
        /// <remarks>
        /// Use with a <c>using</c> statement to ensure proper disposal.
        /// </remarks>
        public Scope CreateScope(string scope)
        {
            ArgumentValidation.ThrowIfNullOrWhiteSpace(scope, nameof(scope));

            Activity activity = ActivitySource.HasListeners() ? ActivitySource.StartActivity(scope, ActivityKind.Internal) : null;

            int scopeId = Interlocked.Increment(ref this.nextScopeId);
            return new Scope(this, activity, scopeId);
        }

        private bool TryMarkDisposed(int scopeId)
        {
            lock (this.disposedScopeIds)
            {
                return this.disposedScopeIds.Add(scopeId);
            }
        }

        /// <summary>
        /// Represents a diagnostic scope for Activity tracking.
        /// Must be used with the 'using' pattern to ensure proper disposal.
        /// </summary>
        /// <remarks>
        /// Dispose() is idempotent - calling it multiple times will only dispose the Activity once.
        /// </remarks>
        public readonly struct Scope : IDisposable
        {
            private readonly CosmosDiagnosticsContext owner;
            private readonly Activity activity;
            private readonly int scopeId;

            internal Scope(CosmosDiagnosticsContext owner, Activity activity, int scopeId)
            {
                ArgumentValidation.ThrowIfNull(owner, nameof(owner));

                this.owner = owner;
                this.activity = activity;
                this.scopeId = scopeId;
            }

            public void Dispose()
            {
                if (!this.owner.TryMarkDisposed(this.scopeId))
                {
                    this.activity?.Dispose();
                    return;
                }

                this.activity?.Dispose();
            }
        }
    }
}
