//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLOrderByEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLOrderByEnumerableExpression(ClientQLEnumerableExpression sourceExpression, ClientQLVariable declaredVariable, IReadOnlyList<ClientQLOrderByItem> vecItems)
            : base(ClientQLEnumerableExpressionKind.OrderBy)
        {
            this.SourceExpression = sourceExpression;
            this.DeclaredVariable = declaredVariable;
            this.VecItems = vecItems;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }

        public ClientQLVariable DeclaredVariable { get; }
        
        public IReadOnlyList<ClientQLOrderByItem> VecItems { get; }
    }

}