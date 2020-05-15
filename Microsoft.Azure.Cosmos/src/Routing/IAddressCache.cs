//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Monads;
    using Microsoft.Azure.Documents;

    internal interface IAddressCache
    {
        /// <summary>
        /// Resolves physical addresses by either PartitionKeyRangeIdentity or by ServiceIdentity.
        /// Client SDK would resolve by PartitionKeyRangeIdentity. Gateway would resolve by ServiceIdentity.
        /// </summary>
        /// <param name="request">
        /// Request is needed only by GatewayAddressCache in the only case when request is name based and user has name based auth token.
        /// Neither PartitionkeyRangeIdentity nor ServiceIdentity can be used to locate auth token in this case.
        /// </param>
        /// <param name="partitionKeyRangeIdentity">This parameter will be supplied in both client SDK and Gateway. In Gateway it will be absent only in case <see cref="DocumentServiceRequest.ServiceIdentity"/> is not <c>null</c>.</param>
        /// <param name="serviceIdentity">This parameter will be only supplied in Gateway. FabricAddressCache ignores <paramref name="partitionKeyRangeIdentity"/>.</param>
        /// <param name="forceRefreshPartitionAddresses">Whether addresses need to be refreshed as previously resolved addresses were determined to be outdated.</param>
        /// <param name="cancellationToken">Instance of <see cref="CancellationToken"/>.</param>
        /// <returns>Physical addresses.</returns>
        Task<TryCatch<PartitionAddressInformation>> TryGetAddressesAsync(
            DocumentServiceRequest request,
            PartitionKeyRangeIdentity partitionKeyRangeIdentity,
            ServiceIdentity serviceIdentity,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken);
    }
}
