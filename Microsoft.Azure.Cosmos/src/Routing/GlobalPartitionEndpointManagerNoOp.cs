//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using Microsoft.Azure.Documents;

    internal sealed class GlobalPartitionEndpointManagerNoOp : GlobalPartitionEndpointManager
    {
        public static readonly GlobalPartitionEndpointManager Instance = new GlobalPartitionEndpointManagerNoOp();

        private GlobalPartitionEndpointManagerNoOp()
        {
        }

        public override void TryAddCurrentLocationForPartitionKeyRange(DocumentServiceRequest request, Uri currentLocation)
        {
            throw new NotImplementedException();
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
    }
}
