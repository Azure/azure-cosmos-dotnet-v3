//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    /// <summary>
    /// Specifies the JSON processing implementation used by the encryption layer when
    /// encrypting and decrypting documents.
    /// </summary>
    /// <remarks>
    /// A processor can be selected per request through the request options property bag,
    /// or configured as a container-wide default when the encryption container is created
    /// via <c>WithEncryptor(container, encryptor, defaultJsonProcessor)</c>. A per-request
    /// selection always overrides the container default.
    /// </remarks>
    public enum JsonProcessor
    {
        /// <summary>
        /// Newtonsoft.Json based (JObject) processing. This is the default.
        /// </summary>
        Newtonsoft,

#if NET8_0_OR_GREATER
        /// <summary>
        /// System.Text.Json based (Utf8JsonReader/Utf8JsonWriter, stream oriented) processing.
        /// </summary>
        /// <remarks>Available with the .NET 8.0 package only; supported for the MDE encryption algorithm.</remarks>
        Stream,
#endif
    }
}