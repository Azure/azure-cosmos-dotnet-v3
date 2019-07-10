//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    
    /// <summary>
    /// Tag matching for LINQ
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// Tag matching for LINQ
        /// </summary>
        /// <param name="dataTags"></param>
        /// <param name="queryTags"></param>
        /// <returns>throws Exception</returns>
        public static bool Match(object dataTags, IEnumerable<string> queryTags) => throw new Exception("Tags.Match is only for linq expressions");

        /// <summary>
        /// Tag matching for LINQ
        /// </summary>
        /// <param name="dataTags"></param>
        /// <param name="queryTags"></param>
        /// <param name="supportDocumentRequiredTags"></param>
        /// <returns>throws Exception</returns>
        public static bool Match(object dataTags, IEnumerable<string> queryTags, bool supportDocumentRequiredTags) => throw new Exception("Tags.Match is only for linq expressions");
    }
}