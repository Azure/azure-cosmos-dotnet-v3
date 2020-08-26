//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Integration
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;

    internal interface IContainerFactory
    {
        public abstract Task<IDocumentContainer> CreateNonPartitionedContainerAsync();
        public abstract Task<IDocumentContainer> CreateSinglePartitionContainerAsync();
        public abstract Task<IDocumentContainer> CreateMultiPartitionContainerAsync();
    }
}
