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
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Internal header class with priority access for known headers and support for dictionary-based access to other headers.
    /// </summary>
    internal class CosmosMessageHeadersInternal : INameValueCollection
    {
        private const int HeadersDefaultCapacity = 10;

        private readonly Dictionary<string, string> headers = new Dictionary<string, string>(
            CosmosMessageHeadersInternal.HeadersDefaultCapacity,
            StringComparer.OrdinalIgnoreCase);

        private string activityId = null;
        private bool isActivityIdSet = false;

        private string date = null;
        private bool isDateSet = false;

        private string partitionkey = null;
        private bool isPartitionkeySet = false;

        private string authorization = null;
        private bool isAuthorizationSet = false;

        public CosmosMessageHeadersInternal()
        {
        }

        public void Add(string headerName, string value)
        {
            if (headerName == null || value == null)
            {
                throw new ArgumentNullException($"{nameof(headerName)}: {headerName ?? "null"}; {nameof(value)}: {value ?? "null"}");
            }

            if (this.TrySetWellKnownHeader(
                headerName,
                value))
            {
                return;
            }

            this.headers.Add(headerName, value);
        }

        public bool TryGetValue(string headerName, out string value)
        {
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            if (this.TryGetWellKnownHeader(
                headerName,
                out bool isSet,
                out value))
            {
                return isSet;
            }

            return this.headers.TryGetValue(headerName, out value);
        }

        public void Remove(string headerName)
        {
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            if (this.TrySetWellKnownHeader(
                headerName,
                default,
                false))
            {
                return;
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

            if (this.TrySetWellKnownHeader(
                key,
                value))
            {
                return;
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
            return this.headers.Count;
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

        private bool TryGetWellKnownHeader(
            string key,
            out bool isSet,
            out string value)
        {
            switch (key.Length)
            {
                case 9:
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key))
                    {
                        value = this.date;
                        isSet = this.isDateSet;
                        return true;
                    }

                    break;

                case 13:
                    if (string.Equals(HttpConstants.HttpHeaders.Authorization, key))
                    {
                        value = this.authorization;
                        isSet = this.isAuthorizationSet;
                        return true;
                    }

                    break;

                case 16:
                    if (string.Equals(HttpConstants.HttpHeaders.ActivityId, key))
                    {
                        value = this.activityId;
                        isSet = this.isActivityIdSet;
                        return true;
                    }

                    break;
                case 28:
                    if (string.Equals(HttpConstants.HttpHeaders.PartitionKey, key))
                    {
                        value = this.partitionkey;
                        isSet = this.isPartitionkeySet;
                        return true;
                    }

                    break;
            }

            isSet = false;
            value = null;
            return false;
        }

        private bool TrySetWellKnownHeader(
            string key,
            string value,
            bool isSet = true)
        {
            switch (key.Length)
            {
                case 9:
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key))
                    {
                        this.date = value;
                        this.isDateSet = isSet;
                        return true;
                    }

                    break;

                case 13:
                    if (string.Equals(HttpConstants.HttpHeaders.Authorization, key))
                    {
                        this.authorization = value;
                        this.isAuthorizationSet = isSet;
                        return true;
                    }

                    break;

                case 16:
                    if (string.Equals(HttpConstants.HttpHeaders.ActivityId, key))
                    {
                        this.activityId = value;
                        this.isActivityIdSet = isSet;
                        return true;
                    }

                    break;
                case 28:
                    if (string.Equals(HttpConstants.HttpHeaders.PartitionKey, key))
                    {
                        this.partitionkey = value;
                        this.isPartitionkeySet = isSet;
                        return true;
                    }

                    break;
            }

            return false;
        }
    }
}