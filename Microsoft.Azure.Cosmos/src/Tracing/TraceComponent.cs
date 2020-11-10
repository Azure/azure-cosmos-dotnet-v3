// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    /// <summary>
    /// The component that governs an <see cref="ITrace"/>.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif 
        enum TraceComponent
    {
        /// <summary>
        /// Component that handles JSON reading, writing, navigating, serialization, and deserialization.
        /// </summary>
        Json, 

        /// <summary>
        /// Component that handles paging results within and across partitions from the service.
        /// </summary>
        Pagination, 

        /// <summary>
        /// Component that handles client side aggregation of distributed query results.
        /// </summary>
        Query, 

        /// <summary>
        /// Component that handles aggregating ReadFeed results across multiple pages and partitons.
        /// </summary>
        ReadFeed, 

        /// <summary>
        /// Component that handles routing requests to physical partitons and maintaining physical partition topology.
        /// </summary>
        Routing, 

        /// <summary>
        /// Component that handles sending requests over the wire (along with selecting the correct replica set for consistency).
        /// </summary>
        Transport, 

        /// <summary>
        /// Component is yet to be categorized.
        /// </summary>
        Unknown
    }
}
