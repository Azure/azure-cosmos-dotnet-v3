//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.CoordinatorDistributionPlan.ClientQL
{
    internal class ClientQLMSingletonLiteral : ClientQLLiteral
    {
        public new enum Kind
        {
            MaxKey,
            MinKey,
            Undefined
        }

        public Kind SingletonKind { get; set; }
    }
}