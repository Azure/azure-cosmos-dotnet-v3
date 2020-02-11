//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.KeyVault
{
    using System;
    using System.Security.Cryptography.X509Certificates;

    internal interface IKeyVaultAccessClientFactory : IDisposable
    {
        IKeyVaultAccessClient CreateKeyVaultAccessClient(
            string clientId,
            X509Certificate2 certificate,
            int aadRetryIntervalInSeconds = KeyVaultConstants.DefaultAadRetryIntervalInSeconds,
            int aadRetryCount = KeyVaultConstants.DefaultAadRetryCount);
    }
}
