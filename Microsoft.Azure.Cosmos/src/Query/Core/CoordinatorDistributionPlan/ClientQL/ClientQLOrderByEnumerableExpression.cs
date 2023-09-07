//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLOrderByEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLOrderByEnumerableExpression(ClientQLEnumerableExpression sourceExpression, ClientQLVariable declaredVariable, IReadOnlyList<ClientQLOrderByItem> items)
            : base(ClientQLEnumerableExpressionKind.OrderBy)
        {
            this.SourceExpression = sourceExpression;
            this.DeclaredVariable = declaredVariable;
            this.Items = items;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }

        public ClientQLVariable DeclaredVariable { get; }
        
        public IReadOnlyList<ClientQLOrderByItem> Items { get; }
    }

}