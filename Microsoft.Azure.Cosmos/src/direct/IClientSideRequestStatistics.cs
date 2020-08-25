//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal interface IClientSideRequestStatistics
    {
        List<Uri> ContactedReplicas { get; set; }

        HashSet<Uri> FailedReplicas { get;}

        HashSet<Uri> RegionsContacted { get;}

        bool IsCpuOverloaded { get; }

        void RecordRequest(DocumentServiceRequest request);

        void RecordResponse(DocumentServiceRequest request, StoreResult storeResult);

        string RecordAddressResolutionStart(Uri targetEndpoint);

        void RecordAddressResolutionEnd(string identifier);

        TimeSpan RequestLatency { get; }

        void AppendToBuilder(StringBuilder stringBuilder);
    }
}

