//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using Microsoft.Azure.Documents;

    internal sealed class StoreResponseStatistics : CosmosDiagnosticsInternal
    {
        public readonly DateTime? RequestStartTime;
        public readonly DateTime RequestResponseTime;
        public readonly ResourceType RequestResourceType;
        public readonly OperationType RequestOperationType;
        public readonly Uri LocationEndpoint;
        public readonly bool IsSupplementalResponse;
        public readonly StoreResultStatistics StoreResultStatistics;

        public StoreResponseStatistics(
            DateTime? requestStartTime,
            DateTime requestResponseTime,
            StoreResult storeResult,
            ResourceType resourceType,
            OperationType operationType,
            Uri locationEndpoint)
        {
            this.RequestStartTime = requestStartTime;
            this.RequestResponseTime = requestResponseTime;
            this.RequestResourceType = resourceType;
            this.RequestOperationType = operationType;
            this.LocationEndpoint = locationEndpoint;
            this.IsSupplementalResponse = operationType == OperationType.Head || operationType == OperationType.HeadFeed;

            if (storeResult != null)
            {
                this.StoreResultStatistics = new StoreResultStatistics(
                    exception: storeResult.Exception,
                    statusCode: storeResult.StatusCode,
                    subStatusCode: storeResult.SubStatusCode,
                    partitionKeyRangeId: storeResult.PartitionKeyRangeId,
                    lsn: storeResult.LSN,
                    requestCharge: storeResult.RequestCharge,
                    isValid: storeResult.IsValid,
                    storePhysicalAddress: storeResult.StorePhysicalAddress,
                    globalCommittedLSN: storeResult.GlobalCommittedLSN,
                    itemLSN: storeResult.ItemLSN,
                    sessionToken: storeResult.SessionToken,
                    usingLocalLSN: storeResult.UsingLocalLSN,
                    activityId: storeResult.ActivityId);
            }
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
