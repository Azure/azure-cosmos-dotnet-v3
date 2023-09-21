//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    using System;
    using System.Collections.Generic;

    internal class QLArrayCreateScalarExpression : QLScalarExpression
    {
        public QLArrayCreateScalarExpression(QLArrayKind arrayKind, IReadOnlyList<QLScalarExpression> items) 
            : base(QLScalarExpressionKind.ArrayCreate)
        {
            this.ArrayKind = arrayKind;
            this.Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public QLArrayKind ArrayKind { get; }
        
        public IReadOnlyList<QLScalarExpression> Items { get; }
    }
}