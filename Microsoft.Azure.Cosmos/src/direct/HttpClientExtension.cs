//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class HttpClientExtension
    {
        internal static void AddUserAgentHeader(this HttpClient httpClient, UserAgentContainer userAgent)
        {
            // As per RFC 2616, Section 14.43
            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.UserAgent, userAgent.UserAgent);
        }

        internal static void AddApiTypeHeader(this HttpClient httpClient, ApiType apitype)
        {
            if (!apitype.Equals(ApiType.None))
            {
                httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.ApiType, apitype.ToString());
            }
        }

        internal static void AddSDKSupportedCapabilitiesHeader(this HttpClient httpClient, ulong capabilities)
        {
            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.SDKSupportedCapabilities, capabilities.ToString());
        }

        internal static Task<HttpResponseMessage> SendHttpAsync(this HttpClient httpClient, HttpRequestMessage requestMessage, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                return httpClient.SendAsync(requestMessage, cancellationToken);
            }
            catch (HttpRequestException requestException)
            {
                throw new ServiceUnavailableException(requestException);
            }
        }

        internal static Task<HttpResponseMessage> SendHttpAsync(this HttpClient httpClient, HttpRequestMessage requestMessage, HttpCompletionOption options, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                return httpClient.SendAsync(requestMessage, options, cancellationToken);
            }
            catch (HttpRequestException requestException)
            {
                throw new ServiceUnavailableException(requestException);
            }
        }

        internal static Task<HttpResponseMessage> GetHttpAsync(this HttpClient httpClient, Uri serviceEndpoint, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                return httpClient.GetAsync(serviceEndpoint, cancellationToken);
            }
            catch (HttpRequestException requestException)
            {
                throw new ServiceUnavailableException(requestException);
            }
        }

    }
}
