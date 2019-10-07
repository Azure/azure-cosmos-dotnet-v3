//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    /// <summary>
    /// Enum of the type of distinct queries.
    /// </summary>
    internal enum DistinctQueryType
    {
        /// <summary>
        /// This means that the query does not have DISTINCT.
        /// </summary>
        None,

        /// <summary>
        /// This means that the query has DISTINCT, but it's not ordered perfectly.
        /// </summary>
        /// <example>SELECT DISTINCT VALUE c.name FROM c</example>
        /// <example>SELECT DISTINCT VALUE c.name FROM c ORDER BY c.age</example>
        /// <example>SELECT DISTINCT c.name FROM c ORDER BY c.name</example>
        Unordered,

        /// <summary>
        /// This means that the query has DISTINCT, and it is ordered perfectly.
        /// </summary>
        /// <example>SELECT DISTINCT VALUE c.name FROM c ORDER BY c.name</example>
        Ordered
    }
}