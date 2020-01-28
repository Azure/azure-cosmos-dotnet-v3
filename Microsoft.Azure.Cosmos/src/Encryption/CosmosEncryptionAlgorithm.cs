//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
#if PREVIEW
    public
#else
    internal
#endif
    enum CosmosEncryptionAlgorithm
    {
        AEAD_AES_256_CBC_HMAC_SHA_256_RANDOMIZED = 1
    }
}
