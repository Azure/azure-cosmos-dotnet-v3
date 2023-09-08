//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLFunctionIdentifier
    {
        public ClientQLFunctionIdentifier(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}