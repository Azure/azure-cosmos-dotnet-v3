//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLVariableRefScalarExpression : ClientQLScalarExpression
    {
        public ClientQLVariable Variable { get; set; }
    }

}