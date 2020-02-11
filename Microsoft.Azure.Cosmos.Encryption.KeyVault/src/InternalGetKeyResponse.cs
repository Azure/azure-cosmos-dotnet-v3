//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Disable CA1812 to pass the compilation analysis check, as this is only used for deserialization.
#pragma warning disable CA1812
namespace Microsoft.Azure.Cosmos.Encryption.KeyVault
{
    internal sealed class InternalGetKeyResponse
    {
        public JsonWebKey Key { get; set; }

        public KeyAttributes Attributes { get; set; }
    }
}