//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net.Http.Headers;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class HttpResponseHeadersWrapper : CosmosMessageHeadersInternal
    {
        private readonly HttpResponseHeaders httpResponseHeaders;
        private readonly HttpContentHeaders httpContentHeaders;
        private readonly Lazy<DictionaryNameValueCollection> dictionaryNameValueCollection;

        /// <summary>
        /// HttpResponse can have 2 headers. These headers have restrictions on what values are allowed.
        /// This optimizes to combine the 2 headers without iterating overall of them to duplicate it into a new
        /// header object.
        /// </summary>
        /// <param name="responseHeaders"></param>
        /// <param name="httpContentHeaders"></param>
        public HttpResponseHeadersWrapper(
            HttpResponseHeaders responseHeaders,
            HttpContentHeaders httpContentHeaders)
        {
            if (responseHeaders.TryGetValues(HttpConstants.HttpHeaders.OwnerFullName, out IEnumerable<string> values))
            {
                responseHeaders.Remove(HttpConstants.HttpHeaders.OwnerFullName);
                foreach (string val in values)
                {
                    responseHeaders.Add(HttpConstants.HttpHeaders.OwnerFullName, Uri.UnescapeDataString(val));
                }
            }

            this.httpResponseHeaders = responseHeaders;
            this.httpContentHeaders = httpContentHeaders;
            this.dictionaryNameValueCollection = new Lazy<DictionaryNameValueCollection>(() => new DictionaryNameValueCollection());
        }

        public override void Add(string key, string value)
        {
            if (this.httpResponseHeaders.TryAddWithoutValidation(key, value))
            {
                return;
            }

            if (this.httpContentHeaders != null && this.httpContentHeaders.TryAddWithoutValidation(key, value))
            {
                return;
            }

            this.dictionaryNameValueCollection.Value.Add(key, value);
        }

        public override void Add(INameValueCollection collection)
        {
            this.dictionaryNameValueCollection.Value.Add(collection);
        }

        public override void Clear()
        {
            this.httpResponseHeaders.Clear();

            if (this.httpContentHeaders != null)
            {
                this.httpContentHeaders.Clear();
            }
            
            if (this.dictionaryNameValueCollection.IsValueCreated)
            {
                this.dictionaryNameValueCollection.Value.Clear();
            }
        }

        public override bool TryGetValue(string headerName, out string value)
        {
            value = this.Get(headerName);
            return value != null;
        }

        public override string[] AllKeys()
        {
            return this.Keys().ToArray();
        }

        public override INameValueCollection Clone()
        {
            INameValueCollection headers = new DictionaryNameValueCollection();

            foreach (KeyValuePair<string, IEnumerable<string>> headerPair in this.AllItems())
            {
                foreach (string val in headerPair.Value)
                {
                    headers.Add(headerPair.Key, val);
                }
            }

            return headers;
        }

        public override int Count()
        {
            int count = 0;
            if (this.dictionaryNameValueCollection.IsValueCreated)
            {
                count = this.dictionaryNameValueCollection.Value.Count();
            }

            if (this.httpContentHeaders != null)
            {
                count += this.httpContentHeaders.Count();
            }

            return this.httpResponseHeaders.Count() + count;
        }

        public override string Get(string key)
        {
            if (!this.httpResponseHeaders.TryGetValues(key, out IEnumerable<string> result))
            {
                if (this.httpContentHeaders == null || !this.httpContentHeaders.TryGetValues(key, out result))
                {
                    if (this.dictionaryNameValueCollection.IsValueCreated)
                    {
                        return this.dictionaryNameValueCollection.Value.Get(key);
                    }
                }
            }

            return this.JoinHeaders(result);
        }

        public override IEnumerator<string> GetEnumerator()
        {
            return this.Keys().GetEnumerator();
        }

        private IEnumerable<KeyValuePair<string, IEnumerable<string>>> AllItems()
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in this.httpResponseHeaders)
            {
                yield return header;
            }

            if (this.httpContentHeaders != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in this.httpContentHeaders)
                {
                    yield return header;
                }
            }

            if (this.dictionaryNameValueCollection.IsValueCreated)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in this.dictionaryNameValueCollection.Value)
                {
                    yield return header;
                }
            }
        }

        public override string[] GetValues(string key)
        {
            return this.httpResponseHeaders.GetValues(key).ToArray();
        }

        public override IEnumerable<string> Keys()
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in this.AllItems())
            {
                yield return header.Key;
            }
        }

        public override void Remove(string key)
        {
            // HttpRepsonseMessageHeaders will throw an exception if it is invalid header
            if (this.httpResponseHeaders.TryGetValues(key, out _))
            {
                this.httpResponseHeaders.Remove(key);
                return;
            }

            if (this.httpContentHeaders != null && this.httpContentHeaders.Remove(key))
            {
                return;
            }

            if (this.dictionaryNameValueCollection.IsValueCreated)
            {
                this.dictionaryNameValueCollection.Value.Remove(key);
            }
        }

        public override void Set(string key, string value)
        {
            this.Remove(key);
            this.Add(key, value);
        }

        public override NameValueCollection ToNameValueCollection()
        {
            NameValueCollection nameValueCollection = new NameValueCollection();
            foreach (KeyValuePair<string, IEnumerable<string>> header in this.AllItems())
            {
                nameValueCollection.Add(header.Key, this.JoinHeaders(header.Value));
            }

            return nameValueCollection;
        }

        private string JoinHeaders(IEnumerable<string> headerValues)
        {
            return headerValues == null ? null : string.Join(",", headerValues);
        }
    }
}
