//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using Microsoft.Azure.Cosmos.Pagination;

    internal sealed class InMemoryCollectionState : State
    {
        public InMemoryCollectionState(long resourceIdentifier)
        {
            this.ResourceIdentifier = resourceIdentifier;
        }

        public long ResourceIdentifier { get; }
    }
}
