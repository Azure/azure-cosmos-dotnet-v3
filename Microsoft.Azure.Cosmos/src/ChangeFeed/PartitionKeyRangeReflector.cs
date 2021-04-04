//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal class PartitionKeyRangeReflector
    {
        private readonly ContainerInternal container;
        private readonly CosmosQueryClient queryClient;

        public PartitionKeyRangeReflector(ContainerInternal container, CosmosQueryClient queryClient)
        {
            this.container = container;
            this.queryClient = queryClient;
        }

        //public List<PartitionKeyRange> GetOverlappingRanges()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
