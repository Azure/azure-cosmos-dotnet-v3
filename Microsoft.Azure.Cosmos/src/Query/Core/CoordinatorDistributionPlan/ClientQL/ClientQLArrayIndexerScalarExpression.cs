﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLArrayIndexerScalarExpression : ClientQLScalarExpression
    {
        public ClientQLArrayIndexerScalarExpression(ClientQLScalarExpression expression, long index) 
            : base(ClientQLScalarExpressionKind.ArrayIndexer)
        {
            this.Expression = expression;
            this.Index = index;
        }

        public ClientQLScalarExpression Expression { get; }
        
        public long Index { get; }
    }

}