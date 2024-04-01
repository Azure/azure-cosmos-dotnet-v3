// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    using System;

    internal sealed class ReEncryptionOperationResponse<T>
    {
        public T Item { get; set; }

        public double RequestUnitsConsumed { get; set; } = 0;

        public bool IsSuccessful { get; set; }

        public Exception CosmosException { get; set; }
    }
}