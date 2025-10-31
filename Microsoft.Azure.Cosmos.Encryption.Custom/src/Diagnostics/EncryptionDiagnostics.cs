//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    /// <summary>
    /// Defines standard diagnostic scope names used throughout the encryption pipeline.
    /// These scope names are used with <see cref="CosmosDiagnosticsContext"/> to track
    /// encryption/decryption operations and can be observed via ActivitySource listeners
    /// (e.g., OpenTelemetry).
    /// </summary>
    /// <remarks>
    /// This class is kept as a separate type (rather than inlined into <see cref="CosmosDiagnosticsContext"/>)
    /// to maintain separation of concerns:
    /// <list type="bullet">
    /// <item><description><see cref="CosmosDiagnosticsContext"/> provides the diagnostic infrastructure (scope creation, recording, Activity management)</description></item>
    /// <item><description><see cref="EncryptionDiagnostics"/> defines the domain-specific scope names used in encryption operations</description></item>
    /// </list>
    /// This separation improves discoverability and keeps domain constants separate from infrastructure code.
    /// </remarks>
    internal static class EncryptionDiagnostics
    {
        /// <summary>
        /// Scope name prefix for MDE (Microsoft.Data.Encryption) encrypt operations.
        /// Concatenate with the JSON processor name to create the full scope name.
        /// </summary>
        /// <example>
        /// Usage: <c>EncryptionDiagnostics.ScopeEncryptModeSelectionPrefix + "Stream"</c>
        /// produces: <c>"EncryptionProcessor.Encrypt.Mde.Stream"</c>
        /// </example>
        internal const string ScopeEncryptModeSelectionPrefix = "EncryptionProcessor.Encrypt.Mde.";

        /// <summary>
        /// Scope name prefix for MDE (Microsoft.Data.Encryption) decrypt operations.
        /// Concatenate with the JSON processor name to create the full scope name.
        /// </summary>
        /// <example>
        /// Usage: <c>EncryptionDiagnostics.ScopeDecryptModeSelectionPrefix + "Newtonsoft"</c>
        /// produces: <c>"EncryptionProcessor.Decrypt.Mde.Newtonsoft"</c>
        /// </example>
        internal const string ScopeDecryptModeSelectionPrefix = "EncryptionProcessor.Decrypt.Mde.";
    }
}
