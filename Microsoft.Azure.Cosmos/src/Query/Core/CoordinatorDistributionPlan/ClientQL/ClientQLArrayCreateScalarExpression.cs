//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLArrayCreateScalarExpression : ClientQLScalarExpression
    {
        public ClientQLArrayCreateScalarExpression(ClientQLArrayKind arrayKind, IReadOnlyList<ClientQLScalarExpression> vecItems) 
            : base(ClientQLScalarExpressionKind.ArrayCreate)
        {
            this.ArrayKind = arrayKind;
            this.VecItems = vecItems;
        }

        public ClientQLArrayKind ArrayKind { get; }
        
        public IReadOnlyList<ClientQLScalarExpression> VecItems { get; }
    }

}