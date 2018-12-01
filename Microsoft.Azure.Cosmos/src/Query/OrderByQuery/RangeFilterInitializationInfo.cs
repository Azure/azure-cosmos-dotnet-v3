//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
namespace Microsoft.Azure.Cosmos.Query
{
    /// <summary>
    /// InitializationInfo is a data structure to capture how the DocumentProducers are initialized
    /// once we start a cross-partition OrderBy query execution from a continutaion token.
    /// 
    /// Specifically, the data-structure captures the "filter" condition for all the partitions that
    /// need to be visited as a part of the query. Please see the description of "filters" in the 
    /// OrderByContinuationToken class. 
    /// </summary>
    internal sealed class RangeFilterInitializationInfo
    {
        public RangeFilterInitializationInfo(
            string filter,
            int startIndex,
            int endIndex)
        {
            if (filter == null)
            {
                throw new ArgumentNullException("filter");
            }

            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException("startIndex");
            }

            this.Filter = filter;
            this.StartIndex = startIndex;
            this.EndIndex = endIndex;
        }

        /// <summary>
        /// Specifies the filter itself. 
        /// </summary>
        /// <example>
        /// For an order by query "select * from root order by root.key ASC", a filter string could be "root.key > 2",
        /// cosindering "key" is an integer field. 
        /// 
        /// The filter simply indicates that the order by query have already delivered all the root.key with "less than equal to 2". 
        /// </example>
        public string Filter
        {
            get;
            private set;
        }

        /// <summary>
        /// Assuming that, at the begining of the query execution, all the partitions, that needs to be visited, are ordered from 0 to n, 
        /// startIndex referes to the starting point of the contiguous block of partitions that requires the "filter" to be applied.
        /// 
        /// Typically, there would three such blocks, one before the target range (Please study the OrderByContinuationToken class 
        /// to understand what a target range is), one the target range itself, and one after the target range. Each of these block 
        /// may have different filter conditions. For example, if (1) the target range has filter codition "root.key >= 2", then (2) preceeding
        /// block will have condition "root.key > 2" and (3) the succeeding block will have filter condition "root.key >= 2". 
        /// 
        /// However, there could be more than one target ranges, in case of query execution across split, each leading to one more 
        /// blocks (typically containg one partition).
        /// </summary>
        public int StartIndex
        {
            get;
            private set;
        }

        /// <summary>
        /// Assuming that, at the bigining of the query execution, all the partitions, that needs to be visited, are ordered from 0 to n, 
        /// EndIndex referes to the end point of the contiguous block of partitions that requires the "filter" to be applied.
        /// </summary>
        public int EndIndex
        {
            get;
            private set;
        }
    }
}
