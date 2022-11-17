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
    internal class NameValueCollectionWrapperFactory : INameValueCollectionFactory
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public INameValueCollection CreateNewNameValueCollection()
        {
            return new NameValueCollectionWrapper();
        }

        public INameValueCollection CreateNewNameValueCollection(int capacity)
        {
            return new NameValueCollectionWrapper(capacity);
        }

        public INameValueCollection CreateNewNameValueCollection(INameValueCollection collection)
        {
            return new NameValueCollectionWrapper(collection);
        }

        public INameValueCollection CreateNewNameValueCollection(NameValueCollection collection)
        {
            return NameValueCollectionWrapper.Create(collection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public INameValueCollection CreateNewNameValueCollection(StringComparer comparer)
        {
            return new NameValueCollectionWrapper(comparer);
        }
    }
}
