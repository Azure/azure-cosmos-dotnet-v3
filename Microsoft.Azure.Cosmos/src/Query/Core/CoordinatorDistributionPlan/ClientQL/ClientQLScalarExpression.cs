//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal abstract class ClientQLScalarExpression : ClientQLExpression
    {
        public ClientQLScalarExpression(ClientQLScalarExpressionKind kind)
        {
            this.Kind = kind;
        }

        public ClientQLScalarExpressionKind Kind { get; }
    }
}