//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLLiteralScalarExpression : ClientQLScalarExpression
    {
        public ClientQLLiteral Literal { get; set; }
    }

}