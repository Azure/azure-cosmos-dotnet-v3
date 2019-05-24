//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;

    /// <summary>
    /// <para>
    /// InitializationInfo is a data structure to capture how the DocumentProducers are initialized
    /// once we start a cross-partition OrderBy query execution from a continuation token.
    /// </para>
    /// <para>
    /// Specifically, the data-structure captures the "filter" condition for all the partitions that
    /// need to be visited as a part of the query. Please see the description of "filters" in the 
    /// OrderByContinuationToken class. 
    /// </para>
    /// </summary>
    internal struct RangeFilterInitializationInfo
    {
        /// <summary>
        /// Initializes a new instance of the RangeFilterInitializationInfo struct.
        /// </summary>
        /// <param name="filter">The filter to apply to the partitions.</param>
        /// <param name="startIndex">The start index of the partitions.</param>
        /// <param name="endIndex">The end index of the partitions.</param>
        public RangeFilterInitializationInfo(
            string filter,
            int startIndex,
            int endIndex)
        {
            if (filter == null)
            {
                throw new ArgumentNullException("filter");
            }

            this.Filter = filter;
            this.StartIndex = startIndex;
            this.EndIndex = endIndex;
        }

        /// <summary>
        /// Gets the filter itself. 
        /// </summary>
        /// <example>
        /// <para>
        /// For an order by query "select * from root order by root.key ASC", a filter string could be "root.key > 2",
        /// considering "key" is an integer field. 
        /// </para>
        /// <para>
        /// The filter simply indicates that the order by query have already delivered all the root.key with "less than equal to 2".
        /// </para>
        /// </example>
        public string Filter
        {
            get;
        }

        /// <summary>
        /// Gets the start index.
        /// <para>
        /// Assuming that, at the beginning of the query execution, all the partitions, that needs to be visited, are ordered from 0 to n, 
        /// startIndex refers to the starting point of the contiguous block of partitions that requires the "filter" to be applied.
        /// </para>
        /// <para>
        /// Typically, there would three such blocks
        /// * one before the target range (Please study the OrderByContinuationToken class to understand what a target range is)
        /// * the target range itself
        /// * one after the target range. 
        /// Each of these block may have different filter conditions. 
        /// For example, if
        /// (1) the target range has filter condition "root.key >= 2", then 
        /// (2) the preceding block will have condition "root.key > 2" and
        /// (3) the succeeding block will have filter condition "root.key >= 2". 
        /// </para>
        /// <para>
        /// However, there could be more than one target ranges, in case of query execution across split, each leading to one more 
        /// blocks (typically containing one partition).
        /// </para>
        /// </summary>
        public int StartIndex
        {
            get;
        }

        /// <summary>
        /// Gets the end index.
        /// Assuming that, at the beginning of the query execution, all the partitions, that needs to be visited, are ordered from 0 to n, 
        /// EndIndex refers to the end point of the contiguous block of partitions that requires the "filter" to be applied.
        /// </summary>
        public int EndIndex
        {
            get;
        }
    }
}
