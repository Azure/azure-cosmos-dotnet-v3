//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Aggregation
{
    internal interface IAggregator
    {
        void Aggregate(object item);

        object GetResult();
    }
}
