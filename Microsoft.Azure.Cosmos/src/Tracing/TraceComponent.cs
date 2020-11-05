// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    internal enum TraceComponent
    {
        Routing,
        Pagination,
        ReadFeed,
        Transport,
        Query,
        Unknown,
    }
}
