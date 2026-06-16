//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    /// <summary>
    /// Selects the JSON processing engine used to decrypt the results of client-side-encryption feed
    /// operations (query, change feed, read-many, and LINQ-sourced iterators).
    /// </summary>
    /// <remarks>
    /// The default is <see cref="Newtonsoft"/> on all target frameworks. The lower-allocation
    /// <c>Stream</c> processor is available only on the net8.0 package. Choose per call with
    /// <c>requestOptions.WithEncryptionJsonProcessor(...)</c> (or the LINQ overloads that accept a
    /// <see cref="JsonProcessor"/>), or per container with
    /// <c>EncryptionContainerExtensions.UseStreamingJsonProcessingByDefault(...)</c>.
    /// </remarks>
#if NET8_0_OR_GREATER
    public enum JsonProcessor
#else
    internal enum JsonProcessor
#endif
    {
        /// <summary>
        /// Newtonsoft.Json. The default on all target frameworks.
        /// </summary>
        Newtonsoft = 0,

#if NET8_0_OR_GREATER
        /// <summary>
        /// System.Text.Json streaming processor (<see cref="System.Text.Json.Utf8JsonReader"/> /
        /// <see cref="System.Text.Json.Utf8JsonWriter"/>). Available on the net8.0 package only;
        /// reduces allocations on the decrypt path.
        /// </summary>
        Stream = 1,
#endif
    }
}