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
    internal interface INameValueCollectionFactory
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        INameValueCollection CreateNewNameValueCollection();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        INameValueCollection CreateNewNameValueCollection(int capacity);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        INameValueCollection CreateNewNameValueCollection(StringComparer comparer);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        INameValueCollection CreateNewNameValueCollection(NameValueCollection collection);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        INameValueCollection CreateNewNameValueCollection(INameValueCollection collection);
    }
}
