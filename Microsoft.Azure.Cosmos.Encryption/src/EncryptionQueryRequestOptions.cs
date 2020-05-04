//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    public class EncryptionQueryRequestOptions : QueryRequestOptions
    {
        public EncryptionOptions EncryptionOptions { get; set; }
    }
}
