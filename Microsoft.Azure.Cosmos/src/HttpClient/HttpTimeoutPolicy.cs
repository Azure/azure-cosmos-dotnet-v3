//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Microsoft.Azure.Documents;

    internal abstract class HttpTimeoutPolicy
    {
        public abstract string TimeoutPolicyName { get; }
        public abstract TimeSpan MaximumRetryTimeLimit { get; }
        public abstract int TotalRetryCount { get; }
        public abstract IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator();
        public abstract bool IsSafeToRetry(HttpMethod httpMethod);

        public static HttpTimeoutPolicy GetTimeoutPolicy(
           DocumentServiceRequest documentServiceRequest)
        {
            if (documentServiceRequest.ResourceType == ResourceType.Document
                && documentServiceRequest.OperationType == OperationType.QueryPlan)
            {
                return HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance;
            }

            if (documentServiceRequest.ResourceType == ResourceType.PartitionKeyRange)
            {
                return HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance;
            }

            return HttpTimeoutPolicyDefault.Instance;
        }
    }
}
