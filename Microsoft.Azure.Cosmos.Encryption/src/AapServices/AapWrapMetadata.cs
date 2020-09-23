//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Metadata used by AAP EncryptionKeyStoreProvider to wrap (encrypt) and unwrap (decrypt) keys.
    /// </summary>
    public sealed class AapWrapMetadata : EncryptionKeyWrapMetadata
    {
        private static readonly string TypeConstant = "aap";

        /// <summary>
        /// Creates a new instance of metadata that the Aap Wrap Meta Data can use to wrap and unwrap keys.
        /// </summary>
        /// <param name="path"> Path of the Key </param>
        /// <param name="name"> Name of the Key </param>
        public AapWrapMetadata(string path, string name)
            : base(type: AapWrapMetadata.TypeConstant, value: path, name: name, null)
        {
        }
    }
}
