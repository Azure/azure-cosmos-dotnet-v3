//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Disable CA1812 to pass the compilation analysis check, as this is only used for deserialization.
#pragma warning disable CA1812
using System.Collections.Generic;

namespace Microsoft.Azure.Cosmos.Encryption
{
    internal sealed class JsonWebKey
    {
        public JsonWebKey(string Kid, string Kty, IReadOnlyList<string> Key_ops,
                            string N, string E)
        {
            this.Kid = Kid;
            this.Kty = Kty;
            this.Key_ops = Key_ops;
            this.N = N;
            this.E = E;
        }
        public string Kid { get; } // Key identifier.

        public string Kty { get; } // JsonWebKey Key Type (kty),

        public IReadOnlyList<string> Key_ops { get; } // Supported key operations.

        public string N { get; } // RSA modulus.

        public string E { get; } // RSA public exponent.
    }
}