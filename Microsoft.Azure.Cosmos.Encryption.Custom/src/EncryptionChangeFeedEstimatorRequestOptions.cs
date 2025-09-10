//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    public class EncryptionChangeFeedEstimatorRequestOptions : ChangeFeedEstimatorRequestOptions
    {
        public EncryptionOptions EncryptionOptions { get; set; }
    }
}
