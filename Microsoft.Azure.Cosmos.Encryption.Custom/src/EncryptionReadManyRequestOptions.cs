//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    public class EncryptionReadManyRequestOptions : ReadManyRequestOptions
    {
        public EncryptionOptions EncryptionOptions { get; set; }
    }
}
