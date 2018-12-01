
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Common
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;

    internal interface ICollectionRoutingMapCache
    {
        Task<CollectionRoutingMap> TryLookupAsync(
            string collectionRid,
            CollectionRoutingMap previousValue,
            CancellationToken cancellationToken,
            bool forceRefreshCollectionRoutingMap = false);
    }
}
