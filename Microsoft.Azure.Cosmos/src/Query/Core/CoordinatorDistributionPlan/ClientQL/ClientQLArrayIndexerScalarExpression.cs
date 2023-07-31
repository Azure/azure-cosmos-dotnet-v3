//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLArrayIndexerScalarExpression : ClientQLScalarExpression
    {
        public ClientQLScalarExpression Expression { get; set; }
        
        public int Index { get; set; }
    }

}