// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    internal class ReencryptionOperationResponse<T>
    {
        public T Item { get; set; }

        public double RequestUnitsConsumed { get; set; } = 0;

        public bool IsSuccessful { get; set; }

        public Exception CosmosException { get; set; }
    }
}