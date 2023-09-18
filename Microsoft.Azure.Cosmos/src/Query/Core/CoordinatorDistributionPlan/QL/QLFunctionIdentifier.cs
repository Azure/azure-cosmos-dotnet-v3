//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.QL
{
    internal class QLFunctionIdentifier
    {
        public QLFunctionIdentifier(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}