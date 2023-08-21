//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLWhereEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLWhereEnumerableExpression(ClientQLEnumerableExpression sourceExpression, ClientQLDelegate clientQLDelegate)
            : base(ClientQLEnumerableExpressionKind.Where)
        {
            this.SourceExpression = sourceExpression;
            this.Delegate = clientQLDelegate;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }
        
        public ClientQLDelegate Delegate { get; }
    }

}