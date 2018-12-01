//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;

    internal sealed class PartitionKeyAndResourceTokenPair
    {
        public PartitionKeyAndResourceTokenPair(PartitionKeyInternal partitionKey, string resourceToken)
        {
            this.PartitionKey = partitionKey;
            this.ResourceToken = resourceToken;
        }

        public PartitionKeyInternal PartitionKey { get; private set; }

        public string ResourceToken { get; private set; }
    }
}
