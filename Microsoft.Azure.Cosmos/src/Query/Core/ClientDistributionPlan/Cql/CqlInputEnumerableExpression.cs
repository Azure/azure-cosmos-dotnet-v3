//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.Cql
{
    using System;

    internal class CqlInputEnumerableExpression : CqlEnumerableExpression
    {
        public CqlInputEnumerableExpression(string name) 
            : base(CqlEnumerableExpressionKind.Input)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
    }
}