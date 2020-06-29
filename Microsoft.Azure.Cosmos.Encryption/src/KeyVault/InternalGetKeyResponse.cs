//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Disable CA1812 to pass the compilation analysis check, as this is only used for deserialization.
#pragma warning disable CA1812
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal sealed class InternalGetKeyResponse
    {
        public InternalGetKeyResponse(JsonWebKey Key, KeyAttributes Attributes)
        {
            this.Key = Key;
            this.Attributes = Attributes;
        }
        public JsonWebKey Key { get; }

        public KeyAttributes Attributes { get; }
    }
}