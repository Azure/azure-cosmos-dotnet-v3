//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Collections
{
    using System;
    using System.Collections.Specialized;

    /// <summary>
    /// 
    /// </summary>
    internal class DictionaryNameValueCollectionFactory : INameValueCollectionFactory
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public INameValueCollection CreateNewNameValueCollection()
        {
            return new DictionaryNameValueCollection();
        }

        public INameValueCollection CreateNewNameValueCollection(int capacity)
        {
            return new DictionaryNameValueCollection(capacity);
        }

        public INameValueCollection CreateNewNameValueCollection(INameValueCollection collection)
        {
            return new DictionaryNameValueCollection(collection);
        }

        public INameValueCollection CreateNewNameValueCollection(NameValueCollection collection)
        {
            return new DictionaryNameValueCollection(collection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public INameValueCollection CreateNewNameValueCollection(StringComparer comparer)
        {
            return new DictionaryNameValueCollection(comparer);
        }
    }
}
