//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientDistributionPlan.QL
{
    internal class QLMSingletonLiteral : QLLiteral
    {
        public QLMSingletonLiteral(Kind singletonKind)
            : base(QLLiteralKind.MSingleton)
        {
            this.SingletonKind = singletonKind;
        }

        public new enum Kind
        {
            MaxKey,
            MinKey,
            Undefined
        }

        public Kind SingletonKind { get; }
    }
}