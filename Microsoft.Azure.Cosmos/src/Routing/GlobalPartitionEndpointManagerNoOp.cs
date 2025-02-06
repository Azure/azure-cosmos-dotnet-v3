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

        public override List<Tuple<PartitionKeyRange, Uri>> GetTuples()
        {
            throw new NotImplementedException();
        }

        public override bool IncrementRequestFailureCounterAndCheckIfPartitionCanFailover(DocumentServiceRequest request)
        {
            return false;
        }

        public override void SetBackgroundConnectionInitTask(Func<List<Tuple<PartitionKeyRange, Uri>>, Task<bool>> backgroundConnectionInitTask)
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
