﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLLiteralScalarExpression : ClientQLScalarExpression
    {
        public ClientQLLiteralScalarExpression(ClientQLLiteral literal)
           : base(ClientQLScalarExpressionKind.Literal)
        {
            this.Literal = literal;
        }

        public ClientQLLiteral Literal { get; }
    }

}