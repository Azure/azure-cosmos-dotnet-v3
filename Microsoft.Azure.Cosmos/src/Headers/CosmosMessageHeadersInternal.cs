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
    using System.Reflection;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Internal header class with priority access for known headers and support for dictionary-based access to other headers.
    /// </summary>
    internal class CosmosMessageHeadersInternal : INameValueCollection
    {
        public static readonly List<Dictionary<string, int>> headerCountList = new List<Dictionary<string, int>>();
        private static readonly int HeadersDefaultCapacity = 16;
        private readonly Dictionary<string, string> headers;
        private readonly Dictionary<string, int> headerCount;

        public CosmosMessageHeadersInternal()
            : this(HeadersDefaultCapacity)
        {
        }

        public CosmosMessageHeadersInternal(int capacity)
        {
            this.headerCount = new Dictionary<string, int>();
            headerCountList.Add(this.headerCount);
            this.headers = new Dictionary<string, string>(
                capacity,
                StringComparer.OrdinalIgnoreCase);
        }

        private void AddCount(string headerName, string op)
        {
            string key = headerName + op;
            if (this.headerCount.ContainsKey(key))
            {
                this.headerCount[key] += 1;
            }
            else
            {
                this.headerCount[key] = 1;
            }
        }

        public void Add(string headerName, string value)
        {
            this.Set(headerName, value);
        }

        public bool TryGetValue(string headerName, out string value)
        {
            this.AddCount(headerName, nameof(TryGetValue));
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            return this.headers.TryGetValue(headerName, out value);
        }

        public void Remove(string headerName)
        {
            this.AddCount(headerName, nameof(Remove));
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            this.headers.Remove(headerName);
        }

        public string this[string headerName]
        {
            get
            {
                string value;
                if (!this.TryGetValue(headerName, out value))
                {
                    return null;
                }

                return value;
            }
            set
            {
                this.Set(headerName, value);
            }
        }

        public void Set(string key, string value)
        {
            this.AddCount(key, nameof(Set));
            if (key == null)
            {
                throw new ArgumentNullException($"{nameof(key)}; {nameof(value)}: {value ?? "null"}");
            }

            if (value == null)
            {
                this.headers.Remove(key);
            }

            this.headers[key] = value;
        }

        public string Get(string key)
        {
            this.AddCount(key, nameof(Get));
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return this[key];
        }

        public void Clear()
        {
            this.headers.Clear();
        }

        public int Count()
        {
            this.AddCount("", nameof(Count));
            return this.headers.Count;
        }

        public INameValueCollection Clone()
        {
            this.AddCount("", nameof(Clone));
            CosmosMessageHeadersInternal headersClone = new CosmosMessageHeadersInternal(this.headers.Count);
            foreach (KeyValuePair<string, string> header in this.headers)
            {
                headersClone.Add(header.Key, header.Value);
            }

            return headersClone;
        }

        public void Add(INameValueCollection collection)
        {
            this.AddCount("INameValueCollection", nameof(Add));
            foreach (string key in collection.Keys())
            {
                this.Add(key, collection[key]);
            }
        }

        public string[] GetValues(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            string value = this[key];
            if (value == null)
            {
                return null;
            }

            return new string[1] { this[key] };
        }

        public string[] AllKeys()
        {
            this.AddCount("", nameof(AllKeys));
            return this.headers.Keys.ToArray();
        }

        public IEnumerable<string> Keys()
        {
            foreach (string key in this.headers.Keys)
            {
                yield return key;
            }
        }

        public NameValueCollection ToNameValueCollection()
        {
            throw new NotImplementedException(nameof(this.ToNameValueCollection));
        }

        public IEnumerator<string> GetEnumerator()
        {
            this.AddCount("", nameof(GetEnumerator));
            return this.headers.Select(x => x.Key).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public T GetHeaderValue<T>(string key)
        {
            string value = this[key];

            if (string.IsNullOrEmpty(value))
            {
                return default(T);
            }

            if (typeof(T) == typeof(double))
            {
                return (T)(object)double.Parse(value, CultureInfo.InvariantCulture);
            }

            return (T)(object)value;
        }
    }
}