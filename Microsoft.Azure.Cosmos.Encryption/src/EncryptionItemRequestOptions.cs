//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    public class EncryptionItemRequestOptions : ItemRequestOptions
    {
        public EncryptionOptions EncryptionOptions { get; set; }
    }
}
