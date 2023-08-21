//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System.Collections.Generic;

    internal class ClientQLDistinctEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLDistinctEnumerableExpression(ClientQLEnumerableExpression sourceExpression, ClientQLVariable declaredVariable, IReadOnlyList<ClientQLScalarExpression> vecExpression) 
            : base(ClientQLEnumerableExpressionKind.Distinct)
        {
            this.SourceExpression = sourceExpression;
            this.DeclaredVariable = declaredVariable;
            this.VecExpression = vecExpression;
        }

        public ClientQLEnumerableExpression SourceExpression { get; }

        public ClientQLVariable DeclaredVariable { get; }
        
        public IReadOnlyList<ClientQLScalarExpression> VecExpression { get; }
    }

}