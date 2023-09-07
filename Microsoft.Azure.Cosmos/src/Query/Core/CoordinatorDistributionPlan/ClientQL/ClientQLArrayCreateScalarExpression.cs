//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLArrayCreateScalarExpression : ClientQLScalarExpression
    {
        public ClientQLArrayCreateScalarExpression(ClientQLArrayKind arrayKind, IReadOnlyList<ClientQLScalarExpression> items) 
            : base(ClientQLScalarExpressionKind.ArrayCreate)
        {
            this.ArrayKind = arrayKind;
            this.Items = items;
        }

        public ClientQLArrayKind ArrayKind { get; }
        
        public IReadOnlyList<ClientQLScalarExpression> Items { get; }
    }

}