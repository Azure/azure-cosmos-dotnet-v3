//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    internal abstract class HttpTimeoutPolicy
    {
        public abstract string TimeoutPolicyName { get; }
        public abstract TimeSpan MaximumRetryTimeLimit { get; }
        public abstract int TotalRetryCount { get; }
        public abstract IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> TimeoutEnumerator { get; }

        public static HttpTimeoutPolicy GetTimeoutPolicy(
           DocumentServiceRequest documentServiceRequest)
        {
            if (documentServiceRequest.ResourceType == ResourceType.Document
                && documentServiceRequest.OperationType == OperationType.QueryPlan)
            {
                return HttpTimeoutPolicyControlPlaneHotPath.Instance;
            }

            if (documentServiceRequest.ResourceType == ResourceType.PartitionKeyRange)
            {
                return HttpTimeoutPolicyControlPlaneHotPath.Instance;
            }

            return HttpTimeoutPolicyDefault.Instance;
        }
    }
}
