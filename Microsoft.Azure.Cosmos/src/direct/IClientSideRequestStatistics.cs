//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;

    internal interface IClientSideRequestStatistics
    {
        IReadOnlyList<TransportAddressUri> ContactedReplicas { get; }

        IReadOnlyCollection<TransportAddressUri> FailedReplicas { get; }

        IReadOnlyCollection<(string, Uri)> RegionsContacted { get; }

        bool? IsCpuHigh { get; }

        bool? IsCpuThreadStarvation { get; }

        void RecordRequest(DocumentServiceRequest request);

        // The following methods provide thread-safe mutation of the contacted/failed replica and
        // region collections. They exist because the collections above are read (e.g. during diagnostics
        // serialization) on a different thread than the store-reader paths that populate them under
        // cross-region request hedging. The implementations swap immutable snapshots so concurrent
        // readers never observe a collection mid-mutation.
        void RecordContactedReplica(TransportAddressUri contactedReplica);

        void RecordContactedReplicas(IReadOnlyList<TransportAddressUri> contactedReplicas);

        void RecordFailedReplica(TransportAddressUri failedReplica);

        void RecordRegionContacted(string regionName, Uri locationEndpoint);

        // Note - storeResult may be disposed before use here
        void RecordResponse(
            DocumentServiceRequest request,
            StoreResult storeResult,
            DateTime startTimeUtc,
            DateTime endTimeUtc);

        void RecordException(
            DocumentServiceRequest request,
            Exception exception,
            DateTime startTimeUtc,
            DateTime endTimeUtc);

        string RecordAddressResolutionStart(Uri targetEndpoint);

        void RecordAddressResolutionEnd(string identifier);

        TimeSpan? RequestLatency { get; }

        void AppendToBuilder(StringBuilder stringBuilder);

        void RecordHttpResponse(HttpRequestMessage request,
                                HttpResponseMessage response,
                                ResourceType resourceType,
                                DateTime requestStartTimeUtc);

        void RecordHttpException(HttpRequestMessage request,
                                Exception exception,
                                ResourceType resourceType,
                                DateTime requestStartTimeUtc);
    }
}

