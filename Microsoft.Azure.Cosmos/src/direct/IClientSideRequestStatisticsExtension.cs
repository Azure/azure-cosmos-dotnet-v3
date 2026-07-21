//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Extends <see cref="IClientSideRequestStatistics"/> with thread-safe mutation of the contacted/failed
    /// replica and region collections.
    /// </summary>
    /// <remarks>
    /// These methods are intentionally declared on a separate, derived interface rather than on
    /// <see cref="IClientSideRequestStatistics"/> itself so that alternate implementations - notably the V3
    /// SDK's ClientSideRequestStatisticsTraceDatum, which lives in a separate repository / submodule - are not
    /// forced to implement the new contract in lock-step. Keeping <see cref="IClientSideRequestStatistics"/>
    /// unchanged avoids an OSS submodule update (and the additional review rounds it triggers).
    ///
    /// The collections exposed by <see cref="IClientSideRequestStatistics"/> are read (e.g. during diagnostics
    /// serialization) on a different thread than the store-reader paths that populate them under cross-region
    /// request hedging. Implementations guard these mutations so concurrent readers, which observe the
    /// collections through a defensive snapshot, never enumerate a collection mid-mutation.
    /// </remarks>
    internal interface IClientSideRequestStatisticsExtension : IClientSideRequestStatistics
    {
        void AppendContactedReplica(TransportAddressUri contactedReplica);

        void AppendContactedReplicas(IReadOnlyList<TransportAddressUri> contactedReplicas);

        void RecordContactedReplicas(IReadOnlyList<TransportAddressUri> contactedReplicas);

        void AppendFailedReplica(TransportAddressUri failedReplica);

        void AppendRegionContacted(string regionName, Uri locationEndpoint);
    }
}
