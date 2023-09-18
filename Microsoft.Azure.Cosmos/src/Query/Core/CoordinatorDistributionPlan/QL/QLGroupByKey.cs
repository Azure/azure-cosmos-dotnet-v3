//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLGroupByKey
    {
        public QLGroupByKey(QLType type)
        {
            this.Type = type;
        }

        public QLType Type { get; }
    }
}