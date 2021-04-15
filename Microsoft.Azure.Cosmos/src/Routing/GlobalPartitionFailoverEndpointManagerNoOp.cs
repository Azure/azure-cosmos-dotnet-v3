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
        public override bool TryAddPartitionLevelLocationOverride(
            DocumentServiceRequest request)
        {
            return false;
        }

        public override bool TryMarkEndpointUnavailableForPartitionKeyRange(
            PartitionKeyRange partitionKeyRange,
            Uri failedLocation)
        {
            return false;
        }
    }
}
