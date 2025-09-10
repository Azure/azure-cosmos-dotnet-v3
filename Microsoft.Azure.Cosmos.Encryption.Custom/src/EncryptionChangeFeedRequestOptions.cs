//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    public class EncryptionChangeFeedRequestOptions : ChangeFeedRequestOptions
    {
        public EncryptionOptions EncryptionOptions { get; set; }
    }
}
