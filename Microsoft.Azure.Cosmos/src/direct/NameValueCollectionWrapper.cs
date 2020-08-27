//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Collections
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    /// <summary>
    /// NameValueCollectionWrapper provides an implementation of INameValueCollection and maintains the behavior of NameValueCollection type.
    /// All operations are delegated to an instance of NameValueCollection internally.
    /// </summary>
    internal class NameValueCollectionWrapper : INameValueCollection
    {
        NameValueCollection collection;

        /// <summary>
        /// 
        /// </summary>
        public NameValueCollectionWrapper()
        {
            this.collection = new NameValueCollection();
        }

        public NameValueCollectionWrapper(int capacity)
        {
            this.collection = new NameValueCollection(capacity);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="comparer"></param>
        public NameValueCollectionWrapper(StringComparer comparer)
        {
            this.collection = new NameValueCollection(comparer);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="values"></param>
        public NameValueCollectionWrapper(NameValueCollectionWrapper values)
        {
            this.collection = new NameValueCollection(values.collection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        public NameValueCollectionWrapper(NameValueCollection collection)
        {
            this.collection = new NameValueCollection(collection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        public NameValueCollectionWrapper(INameValueCollection collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            this.collection = new NameValueCollection();
            foreach (string key in collection)
            {
                this.collection.Add(key, collection[key]);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static NameValueCollectionWrapper Create(NameValueCollection collection)
        {
            NameValueCollectionWrapper wrapper = new NameValueCollectionWrapper();
            wrapper.collection = collection;
            return wrapper;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string this[string key]
        {
            get
            {
                return this.collection[key];
            }

            set
            {
                this.collection[key] = value;
            }
        }

        public void Add(INameValueCollection c)
        {
            if (c == null)
            {
                throw new ArgumentNullException(nameof(c));
            }

            NameValueCollectionWrapper nvc = c as NameValueCollectionWrapper;
            if (nvc != null)
            {
                this.collection.Add(nvc.collection);
            }
            else
            {
                foreach (string key in c)
                {
                    foreach (string value in c.GetValues(key))
                    {
                        this.collection.Add(key, value);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, string value)
        {
            this.collection.Add(key, value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public INameValueCollection Clone()
        {
            return new NameValueCollectionWrapper(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Get(string key)
        {
            return this.collection.Get(key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator()
        {
            return this.collection.GetEnumerator();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string[] GetValues(string key)
        {
            return this.collection.GetValues(key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        public void Remove(string key)
        {
            this.collection.Remove(key);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            this.collection.Clear();
        }

        public int Count()
        {
            return this.collection.Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, string value)
        {
            this.collection.Set(key, value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string[] AllKeys()
        {
            return this.collection.AllKeys;
        }

        public IEnumerable<string> Keys()
        {
            foreach (string key in this.collection.Keys)
            {
                yield return key;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public NameValueCollection ToNameValueCollection()
        {
            return this.collection;
        }
    }
}
