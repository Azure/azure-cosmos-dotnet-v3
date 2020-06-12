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
        private readonly Dictionary<string, string> headers = CosmosMessageHeadersInternal.CreateDictionary();

        public CosmosMessageHeadersInternal()
        {
        }

        public void Add(string headerName, string value)
        {
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this.headers.Add(headerName, value);
        }

        public void Add(string headerName, IEnumerable<string> values)
        {
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            this.Add(headerName, string.Join(",", values));
        }

        public bool TryGetValue(string headerName, out string value)
        {
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            value = null;
            return this.headers.TryGetValue(headerName, out value);
        }

        public void Remove(string headerName)
        {
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
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.headers.Remove(key);
            this.headers.Add(key, value);
        }

        public string Get(string key)
        {
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
            return this.CountHeaders();
        }

        public INameValueCollection Clone()
        {
            return new DictionaryNameValueCollection(this);
        }

        public void Add(INameValueCollection collection)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public IEnumerator<string> GetEnumerator()
        {
            using (IEnumerator<string> headerIterator = this.headers.Select(x => x.Key).GetEnumerator())
            {
                while (headerIterator.MoveNext())
                {
                    yield return headerIterator.Current;
                }
            }
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

        private static Dictionary<string, string> CreateDictionary()
        {
            return new Dictionary<string, string>(16, StringComparer.OrdinalIgnoreCase);
        }

        private int CountHeaders()
        {
            return this.headers.Count;
        }
    }
}