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
    internal sealed class CosmosMessageHeadersInternal : INameValueCollection
    {
        private readonly Lazy<Dictionary<string, string>> headers = new Lazy<Dictionary<string, string>>(CosmosMessageHeadersInternal.CreateDictionary);

        private readonly Dictionary<string, CosmosCustomHeader> knownHeaders;

        public CosmosMessageHeadersInternal(Dictionary<string, CosmosCustomHeader> knownHeaders)
        {
            this.knownHeaders = knownHeaders;
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

            CosmosCustomHeader knownHeader;
            if (this.knownHeaders.TryGetValue(headerName, out knownHeader))
            {
                knownHeader.Set(value);
                return;
            }

            this.headers.Value.Add(headerName, value);
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

            CosmosCustomHeader knownHeader;
            if (this.knownHeaders.TryGetValue(headerName, out knownHeader))
            {
                value = knownHeader.Get();
                return true;
            }

            value = null;
            return this.headers.IsValueCreated && this.headers.Value.TryGetValue(headerName, out value);
        }

        public void Remove(string headerName)
        {
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            CosmosCustomHeader knownHeader;
            if (this.knownHeaders.TryGetValue(headerName, out knownHeader))
            {
                knownHeader.Set(null);
                return;
            }

            this.headers.Value.Remove(headerName);
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

            CosmosCustomHeader knownHeader;
            if (this.knownHeaders.TryGetValue(key, out knownHeader))
            {
                knownHeader.Set(value);
                return;
            }

            this.headers.Value.Remove(key);
            this.headers.Value.Add(key, value);
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
            foreach (KeyValuePair<string, CosmosCustomHeader> knownHeader in this.knownHeaders)
            {
                knownHeader.Value.Set(null);
            }

            if (this.headers.IsValueCreated)
            {
                this.headers.Value.Clear();
            }
        }

        public int Count()
        {
            return this.CountHeaders() + this.CountKnownHeaders();
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
            return this.knownHeaders.Where(header => !string.IsNullOrEmpty(header.Value.Get()))
                                        .Select(header => header.Key)
                                        .Concat(this.headers.Value.Keys).ToArray();
        }

        public IEnumerable<string> Keys()
        {
            foreach (KeyValuePair<string, CosmosCustomHeader> knownHeader in this.knownHeaders.Where(header => !string.IsNullOrEmpty(header.Value.Get())))
            {
                yield return knownHeader.Key;
            }

            foreach (string key in this.headers.Value.Keys)
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
            using (Dictionary<string, CosmosCustomHeader>.Enumerator customHeaderIterator = this.knownHeaders.GetEnumerator())
            {
                while (customHeaderIterator.MoveNext())
                {
                    string customValue = customHeaderIterator.Current.Value.Get();
                    if (!string.IsNullOrEmpty(customValue))
                    {
                        yield return customHeaderIterator.Current.Key;
                    }
                }
            }

            if (!this.headers.IsValueCreated)
            {
                yield break;
            }

            using (IEnumerator<string> headerIterator = this.headers.Value.Select(x => x.Key).GetEnumerator())
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

        internal static KeyValuePair<string, PropertyInfo>[] GetHeaderAttributes<T>()
        {
            IEnumerable<PropertyInfo> knownHeaderProperties = typeof(T)
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttributes(typeof(CosmosKnownHeaderAttribute), false).Any());

            return knownHeaderProperties.Select(
                knownProperty =>
                new KeyValuePair<string, PropertyInfo>(
                    ((CosmosKnownHeaderAttribute)knownProperty.GetCustomAttributes(typeof(CosmosKnownHeaderAttribute), false).First()).HeaderName,
                    knownProperty)).ToArray();
        }

        private static Dictionary<string, string> CreateDictionary()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private int CountHeaders()
        {
            if (!this.headers.IsValueCreated)
            {
                return 0;
            }

            return this.headers.Value.Count;
        }

        private int CountKnownHeaders()
        {
            return this.knownHeaders.Where(customHeader => !string.IsNullOrEmpty(customHeader.Value.Get())).Count();
        }
    }
}