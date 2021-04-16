//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using Microsoft.Azure.Documents;

    internal sealed class GlobalPartitionFailoverEndpointManagerNoOp : GlobalPartitionFailoverEndpointManager
    {
        public static readonly GlobalPartitionFailoverEndpointManager Instance = new GlobalPartitionFailoverEndpointManagerNoOp();

        private GlobalPartitionFailoverEndpointManagerNoOp()
        {
        }

        public override bool TryAddPartitionLevelLocationOverride(
            DocumentServiceRequest request)
        {
            return false;
        }

        public override bool TryMarkEndpointUnavailableForPartitionKeyRange(
            DocumentServiceRequest request,
            Uri failedLocation)
        {
            return false;
        }
    }
}
