//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Collections
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    internal sealed class DictionaryNameValueCollection: INameValueCollection
    {
        private static StringComparer defaultStringComparer = StringComparer.OrdinalIgnoreCase;
        private readonly Dictionary<string, CompositeValue> dictionary;
        private CompositeValue nullValue;

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

        public DictionaryNameValueCollection()
        {
            this.dictionary = new Dictionary<string, CompositeValue>(defaultStringComparer);
        }

        public DictionaryNameValueCollection(StringComparer comparer)
        {
            this.dictionary = new Dictionary<string, CompositeValue>(comparer);
        }

        public DictionaryNameValueCollection(int capacity) : this(capacity, defaultStringComparer) { }

        private DictionaryNameValueCollection(int capacity, StringComparer comparer)
        {
            this.dictionary = new Dictionary<string, CompositeValue>(capacity, comparer == null ? defaultStringComparer : comparer);
        }

        public DictionaryNameValueCollection(INameValueCollection c) : this(c.Count())
        {
            if (c == null)
            {
                throw new ArgumentNullException(nameof(c));
            }

            this.Add(c);
        }

        public DictionaryNameValueCollection(NameValueCollection c) : this(c.Count)
        {
            if (c == null)
            {
                throw new ArgumentNullException(nameof(c));
            }

            foreach (string key in c)
            {
                string[] values = c.GetValues(key);
                if (values != null)
                {
                    foreach (string value in values)
                    {
                        this.Add(key, value);
                    }
                }
                else
                {
                    this.Add(key, null);
                }
            }
        }

        public void Add(string key, string value)
        {
            if (key == null)
            {
                if (this.nullValue == null)
                {
                    this.nullValue = new CompositeValue(value);
                }
                else
                {
                    this.nullValue.Add(value);
                }
                return;
            }

            CompositeValue compositeValue;
            this.dictionary.TryGetValue(key, out compositeValue);
            if (compositeValue != null)
            {
                compositeValue.Add(value);
            }
            else
            {
                this.dictionary.Add(key, new CompositeValue(value));
            }
        }

        public void Add(INameValueCollection c)
        {
            if (c == null)
            {
                throw new ArgumentNullException(nameof(c));
            }

            DictionaryNameValueCollection dictionaryNvc = c as DictionaryNameValueCollection;
            if (dictionaryNvc != null)
            {
                foreach (string key in dictionaryNvc.dictionary.Keys)
                {
                    if (!this.dictionary.ContainsKey(key))
                    {
                        this.dictionary[key] = new CompositeValue();
                    }
                    this.dictionary[key].Add(dictionaryNvc.dictionary[key]);
                }
                if (dictionaryNvc.nullValue != null)
                {
                    if (this.nullValue == null)
                    {
                        this.nullValue = new CompositeValue();
                    }
                    this.nullValue.Add(dictionaryNvc.nullValue);
                }
            }
            else
            {
                foreach (string key in c)
                {
                    foreach (string value in c.GetValues(key))
                    {
                        this.Add(key, value);
                    }
                }
            }
        }

        public void Set(string key, string value)
        {
            if (key == null)
            {
                if (this.nullValue == null)
                {
                    this.nullValue = new CompositeValue(value);
                } else
                {
                    this.nullValue.Reset(value);
                }
                return;
            }

            CompositeValue compositeValue;
            this.dictionary.TryGetValue(key, out compositeValue);
            if (compositeValue != null)
            {
                compositeValue.Reset(value);
            }
            else
            {
                this.dictionary.Add(key, new CompositeValue(value));
            }
        }

        public string Get(string key)
        {
            CompositeValue value = null;
            if (key == null)
            {
                value = nullValue;
            }
            else
            {
                this.dictionary.TryGetValue(key, out value);
            }

            return value == null ? null : value.Value;
        }

        public string[] GetValues(string key)
        {
            CompositeValue value = null;
            if (key == null)
            {
                value = nullValue;
            }
            else
            {
                this.dictionary.TryGetValue(key, out value);
            }

            return value == null ? null : value.Values;
        }

        public void Remove(string key)
        {
            if (key == null)
            {
                nullValue = null;
            }
            else
            {
                this.dictionary.Remove(key);
            }
        }

        public void Clear()
        {
            nullValue = null;
            this.dictionary.Clear();
        }

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (string key in this.dictionary.Keys)
                {
                    yield return key;
                }
                if (nullValue != null)
                {
                    yield return null;
                }
            }
        }

        public int Count()
        {
            return this.dictionary.Count + (nullValue != null ? 1 : 0);
        }

        public string this[string key]
        {
            get
            {
                return this.Get(key);
            }
            set
            {
                this.Set(key, value);
            }
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
            string[] keys = new string[this.Count()];
            int keyIndex = 0;
            foreach (string key in this.dictionary.Keys)
            {
                keys[keyIndex++] = key;
            }
            if (this.nullValue != null)
            {
                keys[keyIndex++] = null;
            }
            return keys;
        }

        IEnumerable<string> INameValueCollection.Keys()
        {
            return this.Keys;
        }

        public NameValueCollection ToNameValueCollection()
        {
            if (this.nvc == null)
            {
                lock (this)
                {
                    if (this.nvc == null)
                    {
                        this.nvc = new NameValueCollection(this.dictionary.Count, (StringComparer)this.dictionary.Comparer);
                        foreach (string key in this)
                        {
                            string[] values = this.GetValues(key);
                            if (values == null)
                            {
                                this.nvc.Add(key, null);
                            }
                            else
                            {
                                foreach (string value in this.GetValues(key))
                                {
                                    this.nvc.Add(key, value);
                                }
                            }
                        }
                    }
                }
            }
            return this.nvc;
        }

        /// <summary>
        /// This class represent the value of the key-value entry.
        /// It can represent a null value as well as multiple values for a single key.
        /// </summary>
        private class CompositeValue
        {
            private List<string> values;

            internal CompositeValue()
            {
                this.values = new List<string>();
            }

            private static string Convert(List<string> values)
            {
                return string.Join(",", values);
            }

            public CompositeValue(string value) : this()
            {
                this.Add(value);
            }

            public void Add(string value)
            {
                if (value == null)
                {
                    return;
                }

                this.values.Add(value);
            }

            public void Reset(string value)
            {
                this.values.Clear();

                this.Add(value);
            }

            public string[] Values
            {
                get
                {
                    return this.values.Count > 0 ?
                        this.values.ToArray() :
                        null;
                }
            }

            public string Value
            {
                get
                {
                    return this.values.Count > 0 ?
                        Convert(this.values) :
                        null;
                }
            }

            public void Add(CompositeValue cv)
            {
                this.values.AddRange(cv.values);
            }
        }
    }
}
