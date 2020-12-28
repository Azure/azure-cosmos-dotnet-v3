//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http.Headers;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class HttpHeadersNameValueCollectionWrapper : INameValueCollection
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
        public HttpHeadersNameValueCollectionWrapper(
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

        public string this[string key]
        {
            get => this.Get(key);
            set => this.Set(key, value);
        }

        public void Add(string key, string value)
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

        public void Add(INameValueCollection collection)
        {
            this.dictionaryNameValueCollection.Value.Add(collection);
        }

        public string[] AllKeys()
        {
            return this.Keys().ToArray();
        }

        public void Clear()
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

        public INameValueCollection Clone()
        {
            INameValueCollection headers = new DictionaryNameValueCollection();

            foreach (KeyValuePair<string, IEnumerable<string>> headerPair in this.httpResponseHeaders)
            {
                foreach (string val in headerPair.Value)
                {
                    headers.Add(headerPair.Key, val);
                }
            }

            if (this.httpContentHeaders != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> headerPair in this.httpContentHeaders)
                {
                    foreach (string val in headerPair.Value)
                    {
                        headers.Add(headerPair.Key, val);
                    }
                }
            }

            if (this.dictionaryNameValueCollection.IsValueCreated)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> headerPair in this.dictionaryNameValueCollection.Value)
                {
                    foreach (string val in headerPair.Value)
                    {
                        headers.Add(headerPair.Key, val);
                    }
                }
            }

            return headers;
        }

        public int Count()
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

        public string Get(string key)
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

            return result == null ? null : string.Join(",", result);
        }

        public IEnumerator GetEnumerator()
        {
            return this.AllItems().GetEnumerator();
        }

        private IEnumerable AllItems()
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

        public string[] GetValues(string key)
        {
            return this.httpResponseHeaders.GetValues(key).ToArray();
        }

        public IEnumerable<string> Keys()
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in this.AllItems())
            {
                yield return header.Key;
            }
        }

        public void Remove(string key)
        {
            if (this.httpResponseHeaders.Remove(key))
            {
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

        public void Set(string key, string value)
        {
            this.Remove(key);
            this.Add(key, value);
        }

        public NameValueCollection ToNameValueCollection()
        {
            throw new NotImplementedException();
        }
    }
}
