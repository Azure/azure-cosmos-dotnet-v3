//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    public class EncryptionQueryRequestOptions : QueryRequestOptions
    {
        public EncryptionOptions EncryptionOptions { get; set; }
    }
}
