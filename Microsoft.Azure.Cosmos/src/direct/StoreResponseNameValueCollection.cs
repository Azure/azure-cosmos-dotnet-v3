//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Collections
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;

    /// <summary>
    /// The StoreResponseNameValueCollection is intended to be used in the case where we're reading from an RNTBD response.
    /// In this case we know there's a 1 header to 1 value mapping of headers and therefore we do not need to incur the cost
    /// of handling multi-valued headers and so we simply use a Dictionary{string, string} over the DictionaryNameValueCollection
    /// which has the cost of allocating a List per value.
    /// </summary>
    internal sealed class StoreResponseNameValueCollection: INameValueCollection
    {
        private static readonly StringComparer DefaultStringComparer = StringComparer.OrdinalIgnoreCase;
        private readonly Dictionary<string, Lazy<string>> dictionary;

        // The INameValueCollection interface is expected to be a replacement for NameValueCollection across the projects.
        // However, there are a few public API with NameValueCollection as return type, e.g. DocumentServiceResponse.ResponseHeaders and
        // DocumentClientException.ResponseHeaders. 
        // 
        // As a hybrid approach in those cases, we maintain the headers internally as an instance of the new INameValueCollection and create 
        // a NameValueCollection for the above public APIs. Keeping the NameValueCollection and the internal INameValueCollection in sync is 
        // not only cumbersome, it may also defeat the purpose of the new dictionary-based type. 
        //
        // Therefore, we want to keep the NameValueCollection consistent within the ResponseHeaders APIs call. In other words,
        // once invoked, the ResponseHeaders will return the same NameValueCollection.
        private NameValueCollection nvc = null;

        public StoreResponseNameValueCollection()
        {
            this.dictionary = new Dictionary<string, Lazy<string>>(StoreResponseNameValueCollection.DefaultStringComparer);
        }

        public StoreResponseNameValueCollection(int capacity)
        {
            this.dictionary = new Dictionary<string, Lazy<string>>(capacity, StoreResponseNameValueCollection.DefaultStringComparer);
        }

        public void Add(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this.dictionary.Add(key, new Lazy<string>(() => value));
        }

        public void AddLazy(string key, Lazy<string> value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this.dictionary.Add(key, value);
        }

        public void Add(INameValueCollection c)
        {
            if (c == null)
            {
                throw new ArgumentNullException(nameof(c));
            }

            foreach (string key in c)
            {
                foreach (string value in c.GetValues(key))
                {
                    this.Add(key, value);
                }
            }
        }

        public void Set(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this.dictionary[key] = new Lazy<string>(() => value);
        }

        public void SetLazy(string key, Lazy<string> value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this.dictionary[key] = value;
        }

        public string Get(string key)
        {
            if(this.dictionary.TryGetValue(key, out Lazy<string> lazyValue))
            {
                return lazyValue.Value;
            }

            return null;
        }

        public string[] GetValues(string key)
        {
            if (this.dictionary.TryGetValue(key, out Lazy<string> lazyValue))
            {
                return new string[] { lazyValue.Value };
            }

            return null;
        }

        public void Remove(string key)
        {
            this.dictionary.Remove(key);
        }

        public void Clear()
        {
            this.dictionary.Clear();
        }

        public IEnumerable<string> Keys => this.dictionary.Keys;

        public int Count()
        {
            return this.dictionary.Count;
        }

        public string this[string key]
        {
            get => this.Get(key);
            set => this.Set(key, value);
        }

        public IEnumerator GetEnumerator()
        {
            return this.Keys.GetEnumerator();
        }

        public INameValueCollection Clone()
        {
            return new DictionaryNameValueCollection(this);
        }

        public string[] AllKeys()
        {
            return this.dictionary.Keys.ToArray();
        }

        IEnumerable<string> INameValueCollection.Keys()
        {
            return this.Keys;
        }

        public NameValueCollection ToNameValueCollection()
        {
            // Note: See comment on line 34 of this file for the implementation. We need to respect current contracts
            // for Backend Gateway.
            if (this.nvc == null)
            {
                lock (this)
                {
                    if (this.nvc == null)
                    {
                        this.nvc = new NameValueCollection(this.dictionary.Count, (StringComparer)this.dictionary.Comparer);
                        foreach (string key in this)
                        {
                            string value = this.Get(key);
                            this.nvc.Add(key, value);
                        }
                    }
                }
            }

            return this.nvc;
        }
    }
}
