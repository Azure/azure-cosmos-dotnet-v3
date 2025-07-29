//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal static class ClientExtensions
    {
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client,
            Uri uri,
            INameValueCollection additionalHeaders = null,
            CancellationToken cancellationToken = default)
        {
            if (uri == null) throw new ArgumentNullException("uri");

            // GetAsync doesn't let clients to pass in additional headers. So, we are
            // internally using SendAsync and add the additional headers to requestMessage. 
            using (HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                if (additionalHeaders != null)
                {
                    foreach (string header in additionalHeaders)
                    {
                        if (GatewayStoreClient.IsAllowedRequestHeader(header))
                        {
                            requestMessage.Headers.TryAddWithoutValidation(header, additionalHeaders[header]);
                        }
                    }
                }
                return await client.SendHttpAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
        }

        public static Task<DocumentServiceResponse> ParseResponseAsync(HttpResponseMessage responseMessage, JsonSerializerOptions serializerSettings = null, DocumentServiceRequest request = null)
        {
            return GatewayStoreClient.ParseResponseAsync(responseMessage, serializerSettings, request);
        }

        public static async Task<DocumentServiceResponse> ParseMediaResponseAsync(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((int)responseMessage.StatusCode < 400)
            {
                INameValueCollection headers = GatewayStoreClient.ExtractResponseHeaders(responseMessage);
                MediaStream mediaStream = new MediaStream(responseMessage, await responseMessage.Content.ReadAsStreamAsync());
                return new DocumentServiceResponse(mediaStream, headers, responseMessage.StatusCode);
            }
            else
            {
                throw await GatewayStoreClient.CreateDocumentClientExceptionAsync(
                    responseMessage: responseMessage,
                    requestStatistics: null);
            }
        }
    }
}
