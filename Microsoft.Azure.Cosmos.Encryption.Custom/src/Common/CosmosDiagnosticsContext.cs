//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Lightweight diagnostics context for Custom Encryption extension.
    /// Uses <see cref="ActivitySource"/> so downstream telemetry (OpenTelemetry) can optionally subscribe.
    /// </summary>
    internal class CosmosDiagnosticsContext
    {
        private static readonly ActivitySource ActivitySource = new ("Microsoft.Azure.Cosmos.Encryption.Custom");

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

        /// <summary>
        /// Factory. A new instance is created per high-level operation.
        /// </summary>
        public static CosmosDiagnosticsContext Create(RequestOptions options)
        {
            _ = options; // Reserved for future correlation if RequestOptions ever carries a diagnostics handle.
            return new CosmosDiagnosticsContext();
        }

        /// <summary>
        /// Creates a new diagnostic scope for an operation.
        /// </summary>
        /// <param name="scope">
        /// The name of the scope. Must not be null or empty.
        /// </param>
        /// <returns>
        /// A <see cref="Scope"/> that creates an Activity when disposed.
        /// Use with a <c>using</c> statement to ensure proper disposal.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="scope"/> is empty.</exception>
        public Scope CreateScope(string scope)
        {
            ArgumentValidation.ThrowIfNullOrEmpty(scope, nameof(scope));

            // Only create Activity if there are listeners to avoid unnecessary allocations.
            Activity activity = ActivitySource.HasListeners() ? ActivitySource.StartActivity(scope, ActivityKind.Internal) : null;
            return new Scope(activity);
        }

        /// <summary>
        /// Represents a diagnostic scope for Activity tracking.
        /// IMPORTANT: This struct should ONLY be used with the 'using' pattern to ensure
        /// single disposal. Do not manually copy this struct as it contains a reference
        /// to an Activity object that will be disposed when this scope is disposed.
        /// </summary>
        /// <remarks>
        /// While Activity.Dispose() is idempotent (safe to call multiple times), the intended
        /// usage pattern is single disposal via 'using' statement.
        /// </remarks>
        public readonly struct Scope : IDisposable
        {
            private readonly Activity activity;

            internal Scope(Activity activity)
            {
                this.activity = activity;
            }

            public void Dispose()
            {
                this.activity?.Dispose();
            }
        }
    }
}
