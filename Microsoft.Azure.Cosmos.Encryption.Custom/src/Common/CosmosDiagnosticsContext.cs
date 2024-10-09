//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    /// <summary>
    /// This is an empty implementation of CosmosDiagnosticsContext which has been plumbed through the DataEncryptionKeyProvider and EncryptionContainer.
    /// This may help adding diagnostics more easily in future.
    /// </summary>
    internal class CosmosDiagnosticsContext
    {
        private static readonly CosmosDiagnosticsContext UnusedSingleton = new ();
        private static readonly IDisposable UnusedScopeSingleton = new Scope();

        public static CosmosDiagnosticsContext Create(RequestOptions options)
        {
            _ = options;
            return CosmosDiagnosticsContext.UnusedSingleton;
        }

        public IDisposable CreateScope(string scope)
        {
            _ = scope;
            return CosmosDiagnosticsContext.UnusedScopeSingleton;
        }

        private class Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
