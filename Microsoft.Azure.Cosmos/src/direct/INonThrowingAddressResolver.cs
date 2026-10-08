//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// Opt-in exceptionless counterpart to <see cref="IAddressResolver"/>.
    /// Implementations resolve physical addresses returning <see cref="Res{T}"/>
    /// instead of throwing, so that address-resolution failures under sustained
    /// backend pressure do not propagate as exceptions through the store pipeline.
    /// Only the implementations on the amplification-sensitive path implement this;
    /// callers should probe for it and fall back to wrapping
    /// <see cref="IAddressResolver.ResolveAsync"/> when it is not implemented.
    /// </summary>
    internal interface INonThrowingAddressResolver
    {
        Task<Res<PartitionAddressInformation>> NonThrowingResolveAsync(
            DocumentServiceRequest request,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken);
    }
}
