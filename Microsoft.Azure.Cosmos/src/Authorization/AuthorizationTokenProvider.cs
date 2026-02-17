//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Security.AccessControl;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using HdrHistogram.Encoding;
    using Microsoft.Azure.Cosmos.Authorization;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using static Microsoft.Azure.Cosmos.Query.Core.Metrics.ServerSideMetricsTokenizer;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should match first type name
    public sealed class CosmosAuthorizationTokenProvider : IDisposable
    {
        private CosmosAuthorizationTokenProvider provider;

        private CosmosAuthorizationTokenProvider()
        {
        }

        public static CosmosAuthorizationTokenProvider FromAzureKeyCredential(AzureKeyCredential authKeyOrResourceTokenCredential)
        {
            return new CosmosAuthorizationTokenProvider()
            {
                authorizationTokenProvider = new AzureKeyCredentialAuthorizationTokenProvider(authKeyOrResourceTokenCredential)
            };
        }

        public static CosmosAuthorizationTokenProvider FromTokenCredential(string accountEndpoint, 
            TokenCredential tokenCredential, 
            TimeSpan? tokenCredentialBackgroundRefreshInterval = null)
        {
            return new CosmosAuthorizationTokenProvider()
            {
                authorizationTokenProvider = new AuthorizationTokenProviderTokenCredential(tokenCredential, new Uri(accountEndpoint), tokenCredentialBackgroundRefreshInterval)
            };
        }

        internal AuthorizationTokenProvider authorizationTokenProvider { get; set; }

        public void Update(CosmosAuthorizationTokenProvider tokenProvider)
        {
            if (tokenProvider == null) throw new ArgumentNullException(nameof(tokenProvider));

            Volatile.Write(ref this.provider, tokenProvider);
        }

        public void Dispose()
        {
            if (this.authorizationTokenProvider != null)
            {
                this.authorizationTokenProvider?.Dispose();
                this.authorizationTokenProvider = null;
            }
        }
    }

    internal class ComposedAuthorizationTokenProvider : AuthorizationTokenProvider
    {
        private CosmosAuthorizationTokenProvider authorizationTokenProvider;

        public ComposedAuthorizationTokenProvider(CosmosAuthorizationTokenProvider authorizationTokenProvider)
        {
            if (authorizationTokenProvider?.authorizationTokenProvider == null) throw new ArgumentNullException(nameof(authorizationTokenProvider));

            this.authorizationTokenProvider = authorizationTokenProvider;
        }

        public override ValueTask AddAuthorizationHeaderAsync(INameValueCollection headersCollection, Uri requestAddress, string verb, AuthorizationTokenType tokenType)
        {
            return this.authorizationTokenProvider.authorizationTokenProvider.AddAuthorizationHeaderAsync(headersCollection, requestAddress, verb, tokenType);
        }

        public override void Dispose()
        {
            if (this.authorizationTokenProvider == null)
            {
                this.authorizationTokenProvider.Dispose();
                this.authorizationTokenProvider = null;
            }
        }

        public override ValueTask<(string token, string payload)> GetUserAuthorizationAsync(string resourceAddress, string resourceType, string requestVerb, INameValueCollection headers, AuthorizationTokenType tokenType)
        {
            return this.authorizationTokenProvider.authorizationTokenProvider.GetUserAuthorizationAsync(resourceAddress, resourceType, requestVerb, headers, tokenType);
        }

        public override ValueTask<string> GetUserAuthorizationTokenAsync(string resourceAddress, string resourceType, string requestVerb, INameValueCollection headers, AuthorizationTokenType tokenType, ITrace trace)
        {
            return this.authorizationTokenProvider.authorizationTokenProvider.GetUserAuthorizationTokenAsync(resourceAddress, resourceType, requestVerb, headers, tokenType, trace);
        }

        public override void TraceUnauthorized(DocumentClientException dce, string authorizationToken, string payload)
        {
            this.authorizationTokenProvider.authorizationTokenProvider.TraceUnauthorized(dce, authorizationToken, payload);
        }
    }
#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    internal abstract class AuthorizationTokenProvider : ICosmosAuthorizationTokenProvider, IAuthorizationTokenProvider, IDisposable
    {
        private readonly DateTime creationTime = DateTime.UtcNow;

        public async Task AddSystemAuthorizationHeaderAsync(
            DocumentServiceRequest request, 
            string federationId, 
            string verb, 
            string resourceId)
        {
            request.Headers[HttpConstants.HttpHeaders.XDate] = Rfc1123DateTimeCache.UtcNow();

            request.Headers[HttpConstants.HttpHeaders.Authorization] = (await this.GetUserAuthorizationAsync(
                resourceId ?? request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                verb,
                request.Headers,
                request.RequestAuthorizationTokenType)).token;
        }

        public abstract ValueTask AddAuthorizationHeaderAsync(
            INameValueCollection headersCollection,
            Uri requestAddress,
            string verb,
            AuthorizationTokenType tokenType);

        public abstract ValueTask<(string token, string payload)> GetUserAuthorizationAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType);

        public abstract ValueTask<string> GetUserAuthorizationTokenAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType,
            ITrace trace);

        public abstract ValueTask AddInferenceAuthorizationHeaderAsync(
            INameValueCollection headersCollection,
            Uri requestAddress,
            string verb,
            AuthorizationTokenType tokenType);

        public abstract void TraceUnauthorized(
            DocumentClientException dce,
            string authorizationToken,
            string payload);

        public virtual TimeSpan GetAge()
        {
            return DateTime.UtcNow.Subtract(this.creationTime);
        }

        public static AuthorizationTokenProvider CreateWithResourceTokenOrAuthKey(string authKeyOrResourceToken)
        {
            if (string.IsNullOrEmpty(authKeyOrResourceToken))
            {
                throw new ArgumentNullException(nameof(authKeyOrResourceToken));
            }

            if (AuthorizationHelper.IsResourceToken(authKeyOrResourceToken))
            {
                return new AuthorizationTokenProviderResourceToken(authKeyOrResourceToken);
            }
            else
            {
                return new AuthorizationTokenProviderMasterKey(authKeyOrResourceToken);
            }
        }

        public abstract void Dispose();
    }
}
