//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    public class EncryptionTransactionalBatchItemRequestOptions : TransactionalBatchItemRequestOptions
    {
        /// <summary>
        /// Options to encrypt properties of the item.
        /// </summary>
        public EncryptionOptions EncryptionOptions { get; set; }
    }
}
