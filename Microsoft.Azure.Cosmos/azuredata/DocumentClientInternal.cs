//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Data.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal partial class DocumentClient : IDisposable, IAuthorizationTokenProvider, IDocumentClient, IDocumentClientInternal
    {
        /// <summary>
        /// Read the <see cref="AccountProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        public Task<AccountProperties> GetDatabaseAccountAsync()
        {
            return TaskHelper.InlineIfPossible(() => this.GetDatabaseAccountPrivateAsync(this.ReadEndpoint), this.ResetSessionTokenRetryPolicy.GetRequestPolicy());
        }

        /// <summary>
        /// Read the <see cref="AccountProperties"/> as an asynchronous operation
        /// given a specific reginal endpoint url.
        /// </summary>
        /// <param name="serviceEndpoint">The reginal url of the serice endpoint.</param>
        /// <param name="cancellationToken">The CancellationToken</param>
        /// <returns>
        /// A <see cref="AccountProperties"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        Task<AccountProperties> IDocumentClientInternal.GetDatabaseAccountInternalAsync(Uri serviceEndpoint, CancellationToken cancellationToken)
        {
            return this.GetDatabaseAccountPrivateAsync(serviceEndpoint, cancellationToken);
        }

        private async Task<AccountProperties> GetDatabaseAccountPrivateAsync(Uri serviceEndpoint, CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.EnsureValidClientAsync();
            GatewayStoreModel gatewayModel = this.gatewayStoreModel as GatewayStoreModel;
            if (gatewayModel != null)
            {
                using (HttpRequestMessage request = new HttpRequestMessage())
                {
                    INameValueCollection headersCollection = new DictionaryNameValueCollection();
                    string xDate = DateTime.UtcNow.ToString("r");
                    headersCollection.Add(HttpConstants.HttpHeaders.XDate, xDate);
                    request.Headers.Add(HttpConstants.HttpHeaders.XDate, xDate);

                    // Retrieve the CosmosAccountSettings from the gateway.
                    string authorizationToken;

                    if (this.hasAuthKeyResourceToken)
                    {
                        authorizationToken = HttpUtility.UrlEncode(this.authKeyResourceToken);
                    }
                    else
                    {
                        authorizationToken = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                            HttpConstants.HttpMethods.Get,
                            serviceEndpoint,
                            headersCollection,
                            this.authKeyHashFunction);
                    }

                    request.Headers.Add(HttpConstants.HttpHeaders.Authorization, authorizationToken);

                    request.Method = HttpMethod.Get;
                    request.RequestUri = serviceEndpoint;

                    AccountProperties databaseAccount = await gatewayModel.GetDatabaseAccountAsync(request);

                    this.useMultipleWriteLocations = this.connectionPolicy.UseMultipleWriteLocations && databaseAccount.EnableMultipleWriteLocations;

                    return databaseAccount;
                }
            }

            return null;
        }
    }
}
