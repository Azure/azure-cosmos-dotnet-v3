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
        List<TransportAddressUri> ContactedReplicas { get; set; }

        HashSet<TransportAddressUri> FailedReplicas { get;}

        HashSet<(string, Uri)> RegionsContacted { get;}

        bool? IsCpuHigh { get; }

        bool? IsCpuThreadStarvation { get; }

        void RecordRequest(DocumentServiceRequest request);

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

