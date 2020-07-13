//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Disable CA1812 to pass the compilation analysis check, as this is only used for deserialization.
#pragma warning disable CA1812
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal sealed class InternalGetKeyResponse
    {
        public InternalGetKeyResponse(global::Azure.Security.KeyVault.Keys.JsonWebKey Key, global::Azure.Security.KeyVault.Keys.KeyProperties Properties)
        {            
            this.Key = Key;
            this.Properties = Properties;
        }
        public global::Azure.Security.KeyVault.Keys.JsonWebKey Key { get; }

        public global::Azure.Security.KeyVault.Keys.KeyProperties Properties { get; }
    }
}