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
        ///<param name="Kid"> Key identifier.</param>
        ///<param name="Kty"> JsonWebKey Key Type.</param>
        ///<param name="Key_ops"> Supported key operations.</param>
        ///<param name="N"> RSA modulus.</param>
        ///<param name="E"> RSA public exponent.</param>
        public JsonWebKey(string Kid, string Kty, IReadOnlyList<string> Key_ops,
                            string N, string E)
        {
            this.Kid = Kid;
            this.Kty = Kty;
            this.Key_ops = Key_ops;
            this.N = N;
            this.E = E;
        }
        public string Kid { get; } 

        public string Kty { get; } 

        public IReadOnlyList<string> Key_ops { get; } 

        public string N { get; }

        public string E { get; }
    }
}