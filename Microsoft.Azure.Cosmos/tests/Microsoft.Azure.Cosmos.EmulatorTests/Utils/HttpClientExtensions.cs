//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal static class HttpClientExtensions
    {
        #region HttpContent Extensions

        public static Task<T> ToResourceAsync<T>(this HttpContent httpContent)
            where T : JsonSerializable, new()
        {
            Task<Stream> readStreamTask = httpContent.ReadAsStreamAsync();
            return readStreamTask.ContinueWith(delegate
            {
                using (httpContent)
                {
                    if (readStreamTask.Exception != null)
                    {
                        throw readStreamTask.Exception.InnerException;
                    }
                    return Resource.LoadFrom<T>(readStreamTask.Result);
                }
            });
        }

        public static HttpContent AsHttpContent<T>(this T resource, NameValueCollection requestHeaders = null)
            where T : Resource, new()
        {
            Stream requestStream = null;

            requestStream = new MemoryStream();
            resource.SaveTo(requestStream);
            requestStream.Seek(0, SeekOrigin.Begin);

            // HttpContent is IDisposable, users need to clean up.
            HttpContent requestContent = new StreamContent(requestStream);

            if (requestHeaders != null)
            {
                foreach (string key in requestHeaders.Keys)
                {
                    if (HttpClientExtensions.IsAllowedRequestHeader(key))
                    {
                        requestContent.Headers.TryAddWithoutValidation(key, requestHeaders[key]);
                    }
                }
            }
            return requestContent;
        }

        public static async Task<ICollection<T>> ListAllAsync<T>(this HttpClient client,
            Uri collectionUri,
            INameValueCollection headers = null) where T : Resource, new()
        {
            Collection<T> responseCollection = new Collection<T>();
            string responseContinuation = null;

            if (headers == null)
            {
                headers = new DictionaryNameValueCollection();
            }

            do
            {
                if (responseContinuation != null)
                {
                    headers[HttpConstants.HttpHeaders.Continuation] = responseContinuation;
                }

                HttpResponseMessage responseMessage;
                foreach (string header in headers.AllKeys())
                {
                    client.DefaultRequestHeaders.Add(header, headers[header]);
                }

                responseMessage = await client.GetAsync(collectionUri, HttpCompletionOption.ResponseHeadersRead);

                FeedResource<T> feedResource = await responseMessage.ToResourceAsync<FeedResource<T>>();

                foreach (T resource in feedResource)
                {
                    responseCollection.Add(resource);
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.Continuation,
                    out IEnumerable<string> continuationToken))
                {
                    responseContinuation = continuationToken.SingleOrDefault();
                }
                else
                {
                    responseContinuation = null;
                }
            } while (!string.IsNullOrEmpty(responseContinuation));

            return responseCollection;
        }
        #endregion

        #region HttpResponseMessage
        public static async Task<T> ToResourceAsync<T>(this HttpResponseMessage responseMessage)
            where T : Resource, new()
        {
            if (responseMessage.StatusCode == HttpStatusCode.NoContent ||
                responseMessage.StatusCode == HttpStatusCode.NotModified)
            {
                responseMessage.Dispose();
                return default(T);
            }

            if ((int)responseMessage.StatusCode < 400)
            {
                return await responseMessage.Content.ToResourceAsync<T>();
            }

            Error error = await responseMessage.Content.ToResourceAsync<Error>();
            throw new DocumentClientException(
                error,
                responseMessage.Headers,
                null);
        }
        #endregion

        #region HTTP Verbs Extensions       

        public static Task<HttpResponseMessage> DeleteAsync(this HttpClient client,
            Uri uri,
            NameValueCollection additionalHeaders = null)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            // GetAsync doesn't let clients to pass in additional headers. So, we are
            // internally using SendAsync and add the additional headers to requestMessage. 
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Delete, uri);

            if (additionalHeaders != null)
            {
                foreach (string header in additionalHeaders.AllKeys)
                {
                    requestMessage.Headers.Add(header, additionalHeaders[header]);
                }
            }

            return client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
        }
        #endregion

        #region helper methods

        private static bool IsAllowedRequestHeader(string headerName)
        {
            if (!headerName.StartsWith("x-ms", StringComparison.OrdinalIgnoreCase))
            {
                switch (headerName)
                {
                    //Just flow the header which are settable at RequestMessage level and the one we care.
                    case HttpConstants.HttpHeaders.Authorization:
                    case HttpConstants.HttpHeaders.Host:
                    case HttpConstants.HttpHeaders.IfMatch:
                    case HttpConstants.HttpHeaders.IfModifiedSince:
                    case HttpConstants.HttpHeaders.IfNoneMatch:
                    case HttpConstants.HttpHeaders.IfRange:
                    case HttpConstants.HttpHeaders.IfUnmodifiedSince:
                    case HttpConstants.HttpHeaders.UserAgent:
                        return true;

                    default:
                        return false;
                }
            }
            return true;
        }

        internal static string BuildFilterQuery(NameValueCollection predicates)
        {
            if (predicates != null && predicates.Count != 0)
            {
                StringBuilder queryBuilder = new StringBuilder();
                bool firstQuery = true;

                foreach (string key in predicates.AllKeys)
                {
                    string value = predicates[key];

                    if (!firstQuery)
                    {
                        queryBuilder.Append(" and ");
                    }
                    else
                    {
                        firstQuery = false;
                    }
                    queryBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0} eq '{1}'", key, value);
                }

                return HttpConstants.QueryStrings.Filter + "=" + queryBuilder.ToString();
            }
            return null;
        }
        #endregion
    }
}
