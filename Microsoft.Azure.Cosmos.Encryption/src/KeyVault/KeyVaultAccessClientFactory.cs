//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;

    internal sealed class KeyVaultAccessClientFactory : IKeyVaultAccessClientFactory
    {
        private HttpClient httpClient;
        public KeyVaultAccessClientFactory()
        {
            this.httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(KeyVaultConstants.DefaultHttpClientTimeoutInSeconds)
            };
        }
        public override IKeyVaultAccessClient CreateKeyVaultAccessClient(
            string clientId,
            X509Certificate2 certificate,
            int aadRetryIntervalInSeconds = KeyVaultConstants.DefaultAadRetryIntervalInSeconds, 
            int aadRetryCount = KeyVaultConstants.DefaultAadRetryCount)
        {
            return new KeyVaultAccessClient(
                clientId: clientId,
                certificate: certificate,
                httpClient: this.httpClient,
                aadRetryInterval: TimeSpan.FromSeconds(aadRetryIntervalInSeconds),
                aadRetryCount: aadRetryCount);
        }

        public override void Dispose()
        {
            this.httpClient?.Dispose();
        }
    }
}
