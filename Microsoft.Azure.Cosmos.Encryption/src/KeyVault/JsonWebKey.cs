//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Disable CA1812 to pass the compilation analysis check, as this is only used for deserialization.
#pragma warning disable CA1812
namespace Microsoft.Azure.Cosmos.Encryption
{
    internal sealed class JsonWebKey
    {
        public string Kid { get; set; } // Key identifier.

        public string Kty { get; set; } // JsonWebKey Key Type (kty),

        public string[] Key_ops { get; set; } // Supported key operations.

        public string N { get; set; } // RSA modulus.

        public string E { get; set; } // RSA public exponent.
    }
}