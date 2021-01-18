// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    internal sealed class EncryptionQueryDefinition : QueryDefinition
    {
        internal Container Container { get; }

        internal EncryptionQueryDefinition(
            string queryText,
            Container container)
            : base(queryText)
        {
            this.Container = container ?? throw new ArgumentNullException(nameof(container));
        }
    }
}
