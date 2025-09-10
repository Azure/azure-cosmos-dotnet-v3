//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    public class EncryptionPatchItemRequestOptions : PatchItemRequestOptions
    {
        public EncryptionOptions EncryptionOptions { get; set; }
    }
}
