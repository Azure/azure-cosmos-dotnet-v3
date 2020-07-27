//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;
    using global::Azure.Security.KeyVault.Keys;
    using Microsoft.Azure.Cosmos.Common;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientCertificateCredentialFactory"/> class.
    /// Class implements Client Certificate Based TokenCredential.
    /// Retreives TenantId based on the keyVault-Key Uri that is passed.
    /// </summary>
    public class ClientCertificateCredentialFactory : KeyVaultTokenCredentialFactory
    {
        private readonly string clientId;
        private readonly X509Certificate2 certificate;
        private readonly AsyncCache<string, ClientCertificateCredential> clientCertificateCredentialByTenantId;
        private readonly ConcurrentDictionary<Uri, string> tenantIdByKeyVaultUri;

        /// <summary>
        /// Initializes all the required information to
        /// fetch the Tenant ID and TokenCredentials.
        /// </summary>
        /// <param name="clientId"> Registered Azure Application (client) ID </param>
        /// <param name="clientCertificate"> Client Certificate </param>
        public ClientCertificateCredentialFactory(string clientId, X509Certificate2 clientCertificate)
        {
            this.certificate = clientCertificate;
            this.clientId = clientId;
            this.clientCertificateCredentialByTenantId = new AsyncCache<string, ClientCertificateCredential>();
            this.tenantIdByKeyVaultUri = new ConcurrentDictionary<Uri, string>();
        }

        /// <summary>
        /// Get the TokenCredentials for the Given KeyVaultKey URI
        /// </summary>
        /// <param name="keyVaultKeyUri"> Key-Vault Key  Uri </param>
        /// <param name="cancellationToken"> Cancellation token </param>
        /// <returns> The TokenCredential </returns>
        public override async ValueTask<TokenCredential> GetTokenCredentialAsync(Uri keyVaultKeyUri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string tenantId;

            if (!KeyVaultUriProperties.TryParseUri(keyVaultKeyUri, out KeyVaultUriProperties keyVaultUriProperties))
            {
                throw new ArgumentException("KeyVault Key Uri {0} is invalid.", keyVaultKeyUri.ToString());
            }

            // build a keyvaultkey Uri and Tenant ID map
            if (!this.tenantIdByKeyVaultUri.TryGetValue(keyVaultUriProperties.KeyVaultUri, out tenantId))
            {
                tenantId = await this.GetTenantIdAsync(keyVaultUriProperties, cancellationToken);
                this.tenantIdByKeyVaultUri.TryAdd(keyVaultUriProperties.KeyVaultUri, tenantId);
            }

            // The idea is to get the Client Credentials,cache them per Tanant
            return await this.clientCertificateCredentialByTenantId.GetAsync(
                                               key: tenantId,
                                               obsoleteValue: null,
                                               singleValueInitFunc: () =>
                                               {
                                                   // Retrieve the Client Creds against the TenantID/ClientID for the saved certificate.
                                                   return Task.FromResult(new ClientCertificateCredential(tenantId, this.clientId, this.certificate));
                                               },
                                               cancellationToken: cancellationToken);
        }

        /// <summary>
        /// This is basically used to retrieve Tenant ID.KeyVault SDK has not exposed any API to retreive the same.
        /// This is required to get Azure AD token to access KeyVault Services in multi-tenant model.
        ///  We return an empty token to retreive the required information.
        /// FIXME : Move to SDK call once Azure SDK exposes the required API.Issue tracked <see href="https://github.com/Azure/azure-sdk-for-net/issues/13713"> here </see>
        /// </summary>
        /// <param name="keyVaultUriProperties"> KeyVault-Key URI for which we need the Authority</param>
        /// <param name="cancellationToken"> Cancellation Token </param>
        /// <returns> Tenant ID </returns>
        private async Task<string> GetTenantIdAsync(KeyVaultUriProperties keyVaultUriProperties, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string tenantId;

            // key client options and set the pipeline policy.
            KeyClientOptions keyClientOptions = new KeyClientOptions();
            HttpPipelinePosition httpPipelinePosition = HttpPipelinePosition.PerCall;
            RetrieveTenantIdPipelinePolicy kvcPolicy = new RetrieveTenantIdPipelinePolicy();
            keyClientOptions.AddPolicy(kvcPolicy, httpPipelinePosition);

            TokenCredential creds = new EmptyTokenCredential();
            KeyClient keyClient = new KeyClient(keyVaultUriProperties.KeyVaultUri, creds, keyClientOptions);

            try
            {
                await keyClient.GetKeyAsync(keyVaultUriProperties.KeyName, keyVaultUriProperties.KeyVersion);

                // the pipeline policy configured above helps out in parsing in the 401 Response and
                // sets the tenant ID and sends a 200 OK Response back to prevent an exception.
                tenantId = kvcPolicy.TenantId;
            }
            catch (Exception ex)
            {
                throw new KeyVaultAccessException(
                           HttpStatusCode.NotFound,
                           KeyVaultErrorCode.KeyVaultServiceUnavailable,
                           "GetTenantIdAsync Failed to retreive Tenant ID for the KeyVaultKey Uri",
                           ex);
            }

            return tenantId;
        }

        private sealed class EmptyTokenCredential : TokenCredential
        {
            private static readonly AccessToken EmptyToken = new AccessToken(string.Empty, DateTimeOffset.UtcNow + TimeSpan.FromDays(1));

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return EmptyTokenCredential.EmptyToken;
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return new ValueTask<AccessToken>(EmptyTokenCredential.EmptyToken);
            }
        }
    }
}