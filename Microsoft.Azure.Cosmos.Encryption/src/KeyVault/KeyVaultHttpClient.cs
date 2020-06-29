//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{

    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Helper Class to Build and Send HTTP Request Utilizing BackOffRetry Utility
    /// </summary>
    internal sealed class KeyVaultHttpClient
    {
        private const string HttpConstantsHttpHeadersAccept = "Accept";
        private const string RuntimeConstantsMediaTypesJson = "application/json";
        private readonly HttpClient httpClient;

        public KeyVaultHttpClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }
        /// <summary>
        /// Helper Method for Building and Sending ASyc HTTP Request.
        /// </summary>
        public async Task<HttpResponseMessage> ExecuteHttpRequestAsync(
            HttpMethod methodtype,
            string KeyVaultRequestUri,
            string accessToken = null,
            string bytesInBase64 = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using HttpRequestMessage request = new HttpRequestMessage(methodtype, KeyVaultRequestUri + "?" + KeyVaultConstants.ApiVersionQueryParameters);
            {
                request.Headers.Add(HttpConstantsHttpHeadersAccept, RuntimeConstantsMediaTypesJson);

                if (!String.IsNullOrEmpty(accessToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue(KeyVaultConstants.Bearer, accessToken);

                if (!String.IsNullOrEmpty(bytesInBase64))
                {
                    InternalWrapUnwrapRequest keyVaultRequest = new InternalWrapUnwrapRequest
                    {
                        Alg = KeyVaultConstants.RsaOaep256,
                        Value = bytesInBase64.TrimEnd('=').Replace('+', '-').Replace('/', '_') // Format base 64 encoded string for http transfer
                    };

                    request.Content = new StringContent(
                        JsonConvert.SerializeObject(keyVaultRequest),
                        Encoding.UTF8,
                        RuntimeConstantsMediaTypesJson);
                }

                string correlationId = Guid.NewGuid().ToString();
                DefaultTrace.TraceInformation("ExecuteHttpRequestAsync: request correlationId {0}.", correlationId);

                request.Headers.Add(
                    KeyVaultConstants.CorrelationId,
                    correlationId);

                WebExceptionRetryPolicy webex = new WebExceptionRetryPolicy();
                try
                {
                    return await BackoffRetryUtility<HttpResponseMessage>.ExecuteAsync(
                                            () =>
                                            {
                                                 return this.httpClient.SendAsync(request, cancellationToken);
                                            },
                                            webex,
                                            cancellationToken);
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceInformation("ExecuteHttpRequestAsync: caught exception while trying to send http request: {0}.", ex.ToString());
                    throw new KeyVaultAccessException(
                        HttpStatusCode.ServiceUnavailable,
                        KeyVaultErrorCode.KeyVaultServiceUnavailable,
                        ex.ToString());
                }

            }

        }

    }
}

