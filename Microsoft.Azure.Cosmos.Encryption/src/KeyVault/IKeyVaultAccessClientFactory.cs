//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Security.Cryptography.X509Certificates;

    internal abstract class IKeyVaultAccessClientFactory : IDisposable
    {
        public abstract IKeyVaultAccessClient CreateKeyVaultAccessClient(
            string clientId,
            X509Certificate2 certificate,
            int aadRetryIntervalInSeconds = KeyVaultConstants.DefaultAadRetryIntervalInSeconds,
            int aadRetryCount = KeyVaultConstants.DefaultAadRetryCount);

        public abstract void Dispose();
    }
}
