//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    using System;

    internal class ClientQLSelectManyEnumerableExpression : ClientQLEnumerableExpression
    {
        public ClientQLEnumerableExpression SourceExpression { get; set; }

        public ClientQLVariable DeclaredVariable { get; set; }
        
        public ClientQLEnumerableExpression SelectorExpression { get; set; }
    }

}