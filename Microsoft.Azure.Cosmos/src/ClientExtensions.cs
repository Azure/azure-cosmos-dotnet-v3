//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using Newtonsoft.Json;

#if !NETSTANDARD16
    using System.Diagnostics;
#endif

    internal static class ClientExtensions
    {
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client,
            Uri uri,
            INameValueCollection additionalHeaders = null,
            CancellationToken cancellationToken = default(CancellationToken))
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
                        if (GatewayStoreModel.IsAllowedRequestHeader(header))
                        {
                            requestMessage.Headers.TryAddWithoutValidation(header, additionalHeaders[header]);
                        }
                    }
                }
                return await client.SendHttpAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
        }

        public static async Task<DocumentServiceResponse> ParseResponseAsync(HttpResponseMessage responseMessage, JsonSerializerSettings serializerSettings = null, bool throwOnKnownClientErrorCodes = true)
        {
            using (responseMessage)
            {
                if ((int)responseMessage.StatusCode < 400)
                {
                    MemoryStream bufferedStream = new MemoryStream();

                    await responseMessage.Content.CopyToAsync(bufferedStream);

                    bufferedStream.Position = 0;

                    INameValueCollection headers = ClientExtensions.ExtractResponseHeaders(responseMessage);
                    return new DocumentServiceResponse(bufferedStream, headers, responseMessage.StatusCode, serializerSettings);
                }
                else if (!throwOnKnownClientErrorCodes && (responseMessage.StatusCode == HttpStatusCode.NotFound || responseMessage.StatusCode == HttpStatusCode.PreconditionFailed || responseMessage.StatusCode == HttpStatusCode.Conflict))
                {
                    INameValueCollection headers = ClientExtensions.ExtractResponseHeaders(responseMessage);
                    return new DocumentServiceResponse(null, headers, responseMessage.StatusCode, serializerSettings);
                }
                else
                {
                    throw await ClientExtensions.CreateDocumentClientException(responseMessage);
                }
            }
        }

        public static async Task<DocumentServiceResponse> ParseMediaResponseAsync(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((int)responseMessage.StatusCode < 400)
            {
                INameValueCollection headers = ClientExtensions.ExtractResponseHeaders(responseMessage);
                MediaStream mediaStream = new MediaStream(responseMessage, await responseMessage.Content.ReadAsStreamAsync());
                return new DocumentServiceResponse(mediaStream, headers, responseMessage.StatusCode);
            }
            else
            {
                throw await ClientExtensions.CreateDocumentClientException(responseMessage);
            }
        }

        private static async Task<DocumentClientException> CreateDocumentClientException(HttpResponseMessage responseMessage)
        {
            Stream readStream = await responseMessage.Content.ReadAsStreamAsync();

            Error error = CosmosResource.LoadFrom<Error>(readStream);

            // ensure there is no local ActivityId, since in Gateway mode ActivityId
            // should always come from message headers
            Trace.CorrelationManager.ActivityId = Guid.Empty;

            bool isNameBased = false;
            bool isFeed = false;
            string resourceTypeString;
            string resourceIdOrFullName;

            string resourceLink = responseMessage.RequestMessage.RequestUri.LocalPath;
            if (!PathsHelper.TryParsePathSegments(resourceLink, out isFeed, out resourceTypeString, out resourceIdOrFullName, out isNameBased))
            {
                // if resourceLink is invalid - we will not set resourceAddress in exception.
            }

            return new DocumentClientException(error,
                responseMessage.Headers, responseMessage.StatusCode)
            {
                StatusDescription = responseMessage.ReasonPhrase,
                ResourceAddress = resourceIdOrFullName
            };
        }

        private static INameValueCollection ExtractResponseHeaders(HttpResponseMessage responseMessage)
        {
            INameValueCollection headers = new StringKeyValueCollection();

            foreach (KeyValuePair<string, IEnumerable<string>> headerPair in responseMessage.Headers)
            {
                if (string.Compare(headerPair.Key, HttpConstants.HttpHeaders.OwnerFullName, StringComparison.Ordinal) == 0)
                {
                    foreach (string val in headerPair.Value)
                    {
                        headers.Add(headerPair.Key, Uri.UnescapeDataString(val));
                    }
                }
                else
                {
                    foreach (string val in headerPair.Value)
                    {
                        headers.Add(headerPair.Key, val);
                    }
                }
            }

            if (responseMessage.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> headerPair in responseMessage.Content.Headers)
                {
                    if (string.Compare(headerPair.Key, HttpConstants.HttpHeaders.OwnerFullName, StringComparison.Ordinal) == 0)
                    {
                        foreach (string val in headerPair.Value)
                        {
                            headers.Add(headerPair.Key, Uri.UnescapeDataString(val));
                        }
                    }
                    else
                    {
                        foreach (string val in headerPair.Value)
                        {
                            headers.Add(headerPair.Key, val);
                        }
                    }
                }
            }

            return headers;
        }
    }
}
