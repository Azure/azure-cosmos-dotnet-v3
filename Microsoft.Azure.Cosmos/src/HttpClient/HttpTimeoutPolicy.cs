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

        public abstract bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage);

        public virtual bool ShouldThrow503OnTimeout => false;

        public static HttpTimeoutPolicy GetTimeoutPolicy(
           DocumentServiceRequest documentServiceRequest)
        {
            //Query Plan Requests
            if (documentServiceRequest.ResourceType == ResourceType.Document
                && documentServiceRequest.OperationType == OperationType.QueryPlan)
            {
                return HttpTimeoutPolicyControlPlaneRetriableHotPath.InstanceShouldThrow503OnTimeout;
            }

            //Partition Key Requests
            if (documentServiceRequest.ResourceType == ResourceType.PartitionKeyRange)
            {
                return HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance;
            }

            //Data Plane Read & Write
            if (!HttpTimeoutPolicy.IsMetaData(documentServiceRequest))
            {
                return HttpTimeoutPolicyDefault.InstanceShouldThrow503OnTimeout;
            }

            //Meta Data Read
            if (HttpTimeoutPolicy.IsMetaData(documentServiceRequest) && documentServiceRequest.IsReadOnlyRequest)
            {
                return HttpTimeoutPolicyDefault.InstanceShouldThrow503OnTimeout;
            }

            //Default behavior
            return HttpTimeoutPolicyDefault.Instance;
        }

        private static bool IsMetaData(DocumentServiceRequest request)
        {
            return (request.OperationType != Documents.OperationType.ExecuteJavaScript && request.ResourceType == ResourceType.StoredProcedure) ||
                request.ResourceType != ResourceType.Document;

        }
    }
}
