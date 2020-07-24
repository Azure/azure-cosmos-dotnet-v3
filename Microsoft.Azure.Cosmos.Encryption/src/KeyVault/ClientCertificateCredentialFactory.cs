//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
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
        private readonly AsyncCache<string, ClientCertificateCredential> clientCertCredCache;
        private readonly AsyncCache<string, string> tenantIdMap;

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
            this.clientCertCredCache = new AsyncCache<string, ClientCertificateCredential>();
            this.tenantIdMap = new AsyncCache<string, string>();
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
            KeyVaultUriProperties uriparser = new KeyVaultUriProperties(keyVaultKeyUri);
            uriparser.TryParseUri();

            // build a keyvaultkey Uri and Tenant ID map
            try
            {
                tenantId = await this.tenantIdMap.GetAsync(
                           key: uriparser.KeyValtName,
                           obsoleteValue: null,
                           singleValueInitFunc: async () =>
                           {
                               return await this.GetTenantIdAsync(keyVaultKeyUri, cancellationToken);
                           },
                           cancellationToken: cancellationToken);
            }
            catch
            {
                throw;
            }

            // The idea is to get the Client Credentials,cache them per Tanant
            return await this.clientCertCredCache.GetAsync(
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
        /// FIXME : Move to SDK call once Azure SDK exposes the required API.
        /// </summary>
        /// <param name="keyVaultKeyUri"> KeyVault-Key URI for which we need the Authority</param>
        /// <param name="cancellationToken"> Cancellation Token </param>
        /// <returns> Tenant ID </returns>
        private async Task<string> GetTenantIdAsync(Uri keyVaultKeyUri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string tenantId;

            KeyVaultUriProperties uriparser = new KeyVaultUriProperties(keyVaultKeyUri);
            uriparser.TryParseUri();

            // key client options and set the pipeline policy.
            KeyClientOptions keyClientOptions = new KeyClientOptions();
            HttpPipelinePosition httpPipelinePosition = HttpPipelinePosition.PerCall;
            KeyVaultClientPipelinePolicy kvcPolicy = new KeyVaultClientPipelinePolicy();
            keyClientOptions.AddPolicy(kvcPolicy, httpPipelinePosition);

            TokenCredential creds = new EmptyTokenCredential();
            KeyClient keyClient = new KeyClient(uriparser.KeyVaultUri, creds, keyClientOptions);

            try
            {
                await keyClient.GetKeyAsync(uriparser.KeyName, uriparser.KeyVersion);

                // the pipeline policy configured above helps out in parsing in the 401 Response and
                // sets the tenant ID and sends a 200 OK Response back to prevent an exception.
                tenantId = kvcPolicy.TenantID;
                if (!string.IsNullOrEmpty(tenantId))
                {
                    return tenantId;
                }
                else
                {
                    throw new KeyVaultAccessException(
                           HttpStatusCode.NotFound,
                           KeyVaultErrorCode.KeyVaultServiceUnavailable,
                           "GetTenantIdAsync Failed to retreive Tenant ID for the KeyVaultKey Uri");
                }
            }
            catch (Exception)
            {
                throw new KeyVaultAccessException(
                           HttpStatusCode.NotFound,
                           KeyVaultErrorCode.KeyVaultServiceUnavailable,
                           "GetTenantIdAsync Failed to retreive Tenant ID for the KeyVaultKey Uri");
            }
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