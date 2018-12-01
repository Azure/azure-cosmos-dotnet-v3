//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// Execuing an OrderBy query for a partitioned collection, say, "select * from root order by root.key ASC", 
    /// boils down to solving a version of k-way merge sort, where, each of the k paratitions produces a sorted stream of values. 
    /// 
    /// Now if a query requires multiple ExecuteNextAsync, we return a serialized version of OrderByContinuationToken, to the users
    /// so that they can resume the query from where they left off at a later point in time. Below we describe the components of
    /// OrderByContinutaion in deatils. 
    /// 
    /// A key notion of a OrderByContinutaion is that of the "Target Partition", which is effectively 
    /// the partition from whose stream the last value was consumed during the execution of the query. 
    /// We construct our continutaion token composing the information of the target partition and 
    /// the metadata related to the last value seen from the target partition.  
    /// 
    /// One key difference in our version of the k-way merge sort from the classical version of the k-way merge sort is that, in 
    /// our case there is a partial order on the 2-tuple consisting of {partition, value seen from that partition}. 
    /// For example, (P1, 2) less than (P2, 2) if P1 is less than P2 (i.e., P1.MinRange is less than P2.MinRange). This difference allowed 
    /// us to shorten the continuation token (i.e., we only need to know the state of the target range), 
    /// but at the cost of performance penalties in pathological cases (e.g., Partition 0 is heavily throttled 
    /// but still servering value "2", while partition 1 has a large number of "2"s which can't be served
    /// to the user).
    /// 
    /// Considering the above fact, three important points to note here are:
    ///     1. If the latest value seen at the target partition is X, then we have exhaused all value less than X (for ASC order),
    ///     all partitions that have smaller Range.Min than that of the target partition. 
    ///     2. All partitions, that have greater Range.Min than that of the target partition, have exhausted all values less than or equal to X 
    ///     
    /// Given this background, below is an exmple of order by continutaion token. The class members below explains the diffrent 
    /// component/states of the continuation token.
    /// </summary>
    /// <example>
    /// Order by continuation token example.
    /// <![CDATA[
    ///  {"compositeToken":{"token":"+RID:OpY0AN-mFAACAAAAAAAABA==#RT:1#TRC:1#RTD:qdTAEA==","range":{"min":"05C1D9CD673398","max":"05C1E399CD6732"}},"orderByItems"[{"item":2}],"rid":"OpY0AN-mFAACAAAAAAAABA==","skipCount":0,"filter":"r.key > 1"}
    /// ]]>
    /// </example>
    internal sealed class OrderByContinuationToken
    {
        /// <summary>
        /// Target partition states, including backend continuation and partition key range information. 
        /// </summary>
        /// <example>
        /// <![CDATA[
        ///  {"compositeToken":{"token":"+RID:OpY0AN-mFAACAAAAAAAABA==#RT:1#TRC:1#RTD:qdTAEA==","range":{"min":"05C1D9CD673398","max":"05C1E399CD6732"}}
        /// ]]>
        /// </example>
        [JsonProperty("compositeToken")]
        public CompositeContinuationToken CompositeToken
        {
            get;
            set;
        }

        /// <summary>
        /// Values in the top most OrderByQueryResult from the target partition.
        /// orderByItems is used for filtering after we resume.
        /// </summary>
        /// <example>
        /// Here, the item 2 means that, it was an ordeBy by integer field, and when the query paused,
        /// the lastest value seen from the corresponding partition was 2. 
        /// <![CDATA[
        ///  "orderByItems"[{"item":2}]
        /// ]]>
        /// </example>
        /// <remarks>
        /// Right now, we don't support orderBy by multiple fileds, so orderByItems is an array of one element. 
        /// </remarks>>
        [JsonProperty("orderByItems")]
        public QueryItem[] OrderByItems
        {
            get;
            set;
        }

        /// <summary>
        /// Rid in the top most OrderByQueryResult from the target partition.
        /// Rid is used for filtering after we resume, when orderByItems have the same value.
        /// 
        /// Note that, Rid is just a marker from the backend's point of view, and the
        /// doccument with the Rid might not exist upon resuming a query (due to deletion or
        /// other reasons). The backend will just return the next available result logically 
        /// succeeding the marker. 
        /// </summary>
        /// <example>
        /// <![CDATA[
        ///  "rid":"OpY0AN-mFAACAAAAAAAABA=="
        /// ]]>
        /// </example>
        [JsonProperty("rid")]
        public string Rid
        {
            get;
            set;
        }

        /// <summary>
        /// Skip count is necessary for JOIN queries to resume. Azure Cosmos DB's joins are much diffrent from standard 
        /// SQL joins. While standard SQL joins happen across two tables, Azure Cosmos DB joins happens over a single collection 
        /// (think single table with each row having dynamic number of columns). While executing a join query, 
        /// each Azure Cosmos DB document (i.e, each row), though, can generate multiple result iteams. You can look up the documentation 
        /// online to understand this better. 
        /// 
        /// This behavor has implications on how pagination work for documnetDB queries, especially for order by queries across
        /// multiple partition. 
        /// 
        /// To understand complexity, let's take an example. Let's say that there is only 1 partition in a collection, and the collection
        /// has 2 documents. And each document generate 6 results on a hypothetical join query. Now, if someone issues the query with a page size
        /// of 10, while fetching the second page of the query (as it has already exhausted the results produced by the join on the first document) 
        /// needs to resume from the second document and skip the first 6 - ((2 * 6) - 10) = 4 results. 
        /// 
        /// The skip count keeps track of that information. 
        /// </summary> 
        [JsonProperty("skipCount")]
        public int SkipCount
        {
            get;
            set;
        }

        /// <summary>
        /// We use the filter to rewrite the OrderBy query when resuming from a continuation token. 
        /// </summary>
        /// <example>
        /// In this example snippet below the filter string indicates that the query was an OrderBy query 
        /// and when the query was paused it had already output all the values value greater than 1. 
        /// And when the query resumes it only needs to fetch value greater than 1. 
        /// 
        /// Note that, if any value less than 1 that was inserted after the query started won't be delivered as a 
        /// part of the result. 
        /// <![CDATA[
        ///  "filter":"r.key > 1"
        /// ]]>
        /// </example>
        [JsonProperty("filter")]
        public string Filter
        {
            get;
            set;
        }

        public object ShallowCopy()
        {
            return this.MemberwiseClone();
        }
    }
}
