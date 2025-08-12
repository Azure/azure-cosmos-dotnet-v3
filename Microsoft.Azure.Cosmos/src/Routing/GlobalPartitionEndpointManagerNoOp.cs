//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal sealed class GlobalPartitionEndpointManagerNoOp : GlobalPartitionEndpointManager
    {
        public static readonly GlobalPartitionEndpointManager Instance = new GlobalPartitionEndpointManagerNoOp();

        private GlobalPartitionEndpointManagerNoOp()
        {
        }

        public override void SetBackgroundConnectionPeriodicRefreshTask(
            Func<Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>>, Task> backgroundConnectionInitTask)
        {
            return;
        }

        public override bool TryAddPartitionLevelLocationOverride(
            DocumentServiceRequest request)
        {
            return false;
        }

        public override bool TryMarkEndpointUnavailableForPartitionKeyRange(
            DocumentServiceRequest request)
        {
            return false;
        }

        public override bool IsRequestEligibleForPartitionLevelCircuitBreaker(DocumentServiceRequest request)
        {
            return false;
        }

        public override bool IsRequestEligibleForPerPartitionAutomaticFailover(DocumentServiceRequest request)
        {
            return false;
        }

        public override bool IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(
            DocumentServiceRequest request)
        {
            return false;
        }
    }
}
