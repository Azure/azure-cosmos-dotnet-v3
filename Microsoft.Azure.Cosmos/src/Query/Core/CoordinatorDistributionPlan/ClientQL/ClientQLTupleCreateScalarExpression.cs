﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLTupleCreateScalarExpression : ClientQLScalarExpression
    {
        public ClientQLTupleCreateScalarExpression(IReadOnlyList<ClientQLScalarExpression> items) 
            : base(ClientQLScalarExpressionKind.TupleCreate)
        {
            this.Items = items;
        }

        public IReadOnlyList<ClientQLScalarExpression> Items { get; }
    }

}