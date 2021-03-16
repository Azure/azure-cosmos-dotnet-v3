//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Common
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal interface ICollectionRoutingMapCache
    {
        Task<CollectionRoutingMap> TryLookupAsync(
            string collectionRid,
            CollectionRoutingMap previousValue,
            DocumentServiceRequest request,
            CancellationToken cancellationToken,
            ITrace trace);
    }
}
