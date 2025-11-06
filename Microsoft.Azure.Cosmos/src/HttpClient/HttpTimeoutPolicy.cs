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
        public abstract int TotalRetryCount { get; }
        public abstract IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> GetTimeoutEnumerator();

        public abstract bool ShouldRetryBasedOnResponse(HttpMethod requestHttpMethod, HttpResponseMessage responseMessage);

        public virtual bool ShouldThrow503OnTimeout => false;

        public bool IsSafeToRetry(DocumentServiceRequest documentServiceRequest)
        {
            if (this is HttpTimeoutPolicyNoRetry)
            {
                return (this as HttpTimeoutPolicyNoRetry).IsSafeToRetry();
            }
            if (documentServiceRequest != null)
            {
                //Query Plan Requests
                if (documentServiceRequest.ResourceType == ResourceType.Document
                    && documentServiceRequest.OperationType == OperationType.QueryPlan)
                {
                    return true;
                }

                //Get Partition Key Range Requests
                if (documentServiceRequest.ResourceType == ResourceType.PartitionKeyRange
                    && documentServiceRequest.OperationType == OperationType.ReadFeed)
                {
                    return true;
                }

                //Get Addresses Requests
                if (documentServiceRequest.ResourceType == ResourceType.Address)
                {
                    return true;
                }

                //Meta Data Read
                if (HttpTimeoutPolicy.IsMetaData(documentServiceRequest) && documentServiceRequest.IsReadOnlyRequest)
                {
                    return true;
                }

                //Data Plane Operations
                if (!HttpTimeoutPolicy.IsMetaData(documentServiceRequest))
                {
                    if (documentServiceRequest.IsReadOnlyRequest)
                    {
                        if (this is HttpTimeoutPolicyForThinClient)
                        {
                            return (this as HttpTimeoutPolicyForThinClient).shouldRetry;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return false;
        }

        public static HttpTimeoutPolicy GetTimeoutPolicy(
           DocumentServiceRequest documentServiceRequest,
           bool isPartitionLevelFailoverEnabled = false,
           bool isThinClientEnabled = false)
        {
            //Query Plan Requests
            if (documentServiceRequest.ResourceType == ResourceType.Document
                && documentServiceRequest.OperationType == OperationType.QueryPlan)
            {
                return HttpTimeoutPolicyControlPlaneRetriableHotPath.InstanceShouldThrow503OnTimeout;
            }

            //Get Partition Key Range Requests
            if (documentServiceRequest.ResourceType == ResourceType.PartitionKeyRange
                && documentServiceRequest.OperationType == OperationType.ReadFeed)
            {
                return HttpTimeoutPolicyControlPlaneRetriableHotPath.InstanceShouldThrow503OnTimeout;
            }

            //Get Addresses Requests
            if (documentServiceRequest.ResourceType == ResourceType.Address)
            {
                return HttpTimeoutPolicyControlPlaneRetriableHotPath.InstanceShouldThrow503OnTimeout;
            }

            //Data Plane Operations
            if (!HttpTimeoutPolicy.IsMetaData(documentServiceRequest))
            {
                if (isThinClientEnabled)
                {
                    return documentServiceRequest.IsReadOnlyRequest
                        ? HttpTimeoutPolicyForThinClient.InstanceShouldRetryAndThrow503OnTimeout
                        : HttpTimeoutPolicyForThinClient.InstanceShouldNotRetryAndThrow503OnTimeout;
                }
                // Data Plane Reads.
                else if (documentServiceRequest.IsReadOnlyRequest)
                {
                    return isPartitionLevelFailoverEnabled
                        ? HttpTimeoutPolicyForPartitionFailover.InstanceShouldThrow503OnTimeout
                        : HttpTimeoutPolicyDefault.InstanceShouldThrow503OnTimeout;
                }
            }

            //Meta Data Read
            if (HttpTimeoutPolicy.IsMetaData(documentServiceRequest) && documentServiceRequest.IsReadOnlyRequest)
            {
                return HttpTimeoutPolicyControlPlaneRetriableHotPath.InstanceShouldThrow503OnTimeout;
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
