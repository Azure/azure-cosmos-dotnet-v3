//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLTupleCreateScalarExpression : ClientQLScalarExpression
    {
        public ClientQLTupleCreateScalarExpression(IReadOnlyList<ClientQLScalarExpression> vecItems) 
            : base(ClientQLScalarExpressionKind.TupleCreate)
        {
            this.VecItems = vecItems;
        }

        public IReadOnlyList<ClientQLScalarExpression> VecItems { get; }
    }

}