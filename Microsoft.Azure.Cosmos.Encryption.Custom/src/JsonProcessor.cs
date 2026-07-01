//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    /// <summary>
    /// Selects the JSON processing engine used to encrypt and decrypt documents in client-side-encryption
    /// operations (item CRUD, query, change feed, read-many, and LINQ-sourced iterators).
    /// </summary>
    /// <remarks>
    /// The default is <see cref="Newtonsoft"/> on all target frameworks. The lower-allocation
    /// <c>Stream</c> processor is available only on the net8.0 package and is supported for the MDE
    /// encryption algorithm. Choose the processor per call with
    /// <c>requestOptions.WithEncryptionJsonProcessor(...)</c> (or the LINQ overloads that accept a
    /// <see cref="JsonProcessor"/>) or through the request options property bag, or configure a
    /// container-wide default via <c>WithEncryptor(container, encryptor, defaultJsonProcessor)</c>.
    /// A per-request selection always overrides the container default.
    /// </remarks>
    internal enum JsonProcessor
    {
        /// <summary>
        /// Newtonsoft.Json based (JObject) processing. The default on all target frameworks.
        /// </summary>
        Newtonsoft = 0,

#if NET8_0_OR_GREATER
        /// <summary>
        /// System.Text.Json streaming processor (<see cref="System.Text.Json.Utf8JsonReader"/> /
        /// <see cref="System.Text.Json.Utf8JsonWriter"/>). Available on the net8.0 package only;
        /// supported for the MDE encryption algorithm and reduces allocations on the decrypt path.
        /// </summary>
        Stream = 1,
#endif
    }
}