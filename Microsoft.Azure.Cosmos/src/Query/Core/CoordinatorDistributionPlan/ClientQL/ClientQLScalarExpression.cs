//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLScalarExpression : ClientQLExpression
    {
        public ClientQLScalarExpressionKind Kind { get; set; }
    }
}