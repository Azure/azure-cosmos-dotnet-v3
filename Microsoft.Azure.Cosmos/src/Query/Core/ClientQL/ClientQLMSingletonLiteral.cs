//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ClientQL
{
    internal class ClientQLMSingletonLiteral : ClientQLLiteral
    {
        public enum Kind
            {
                MaxKey,
                MinKey,
                Undefined
            };

        public Kind SingletonKind { get; set; }
    }
}